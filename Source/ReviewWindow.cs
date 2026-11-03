using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace DuplicateReview
{
    // Database reads happen only when this window is opened or the user refreshes it.
    // There are no automatic startup hooks, scheduled tasks, downloads or scans.
    internal sealed class ReviewWindow
    {
        private readonly IPlayniteAPI api;
        private Window window;
        private TabControl views;
        private TabItem reviewTab;
        private TabItem hiddenTab;
        private TabItem similarTab;
        private DataGrid reviewGrid;
        private DataGrid hiddenGrid;
        private DataGrid similarGrid;
        private TextBlock reviewSummary;
        private TextBlock hiddenSummary;
        private TextBlock similarSummary;
        private Button hideButton;
        private Button restoreButton;
        private List<ReviewRow> reviewRows = new List<ReviewRow>();
        private List<ReviewRow> hiddenRows = new List<ReviewRow>();
        private List<ReviewRow> similarRows = new List<ReviewRow>();
        private bool isUpdating;

        internal ReviewWindow(IPlayniteAPI api) { this.api = api; }

        private bool IsHiddenView { get { return views.SelectedIndex == 2; } }
        private bool IsSimilarView { get { return views.SelectedIndex == 1; } }
        private DataGrid ActiveGrid { get { return IsHiddenView ? hiddenGrid : (IsSimilarView ? similarGrid : reviewGrid); } }
        private List<ReviewRow> ActiveRows { get { return IsHiddenView ? hiddenRows : (IsSimilarView ? similarRows : reviewRows); } }

        internal void Show()
        {
            window = api.Dialogs.CreateWindow(new WindowCreationOptions { ShowMinimizeButton = false });
            window.Title = "Duplicate Review 0.1.2 — manual library cleanup";
            window.MinWidth = 800;
            window.Width = 1150;
            window.MinHeight = 460;
            window.Height = 710;
            var owner = api.Dialogs.GetCurrentAppWindow();
            if (owner != null && owner != window) window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var main = new DockPanel { Margin = new Thickness(14) };
            var top = new StackPanel { Orientation = Orientation.Vertical };
            DockPanel.SetDock(top, Dock.Top);
            main.Children.Add(top);
            top.Children.Add(new TextBlock
            {
                Text = "Duplicate names in your Playnite library",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5)
            });
            top.Children.Add(new TextBlock
            {
                Text = "Exact duplicates, possible name matches, and hidden copies have separate tabs. Ctrl/Shift-click rows, then Mark highlighted. Similar names can be different games—review them carefully.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var footer = new StackPanel { Orientation = Orientation.Vertical };
            DockPanel.SetDock(footer, Dock.Bottom);
            main.Children.Add(footer);
            var buttons = new WrapPanel { Margin = new Thickness(0, 11, 0, 7) };
            footer.Children.Add(buttons);
            MakeButton("Mark highlighted", buttons, () => MarkHighlighted(true));
            MakeButton("Unmark highlighted", buttons, () => MarkHighlighted(false));
            MakeButton("Clear marks", buttons, () =>
            {
                Matcher.SetMarked(ActiveRows, false);
                ActiveGrid.Items.Refresh();
            });
            hideButton = MakeButton("Hide marked...", buttons, () => Apply(true));
            restoreButton = MakeButton("Restore marked...", buttons, () => Apply(false));
            MakeButton("Refresh", buttons, Refresh);
            MakeButton("Close", buttons, () => window.Close());
            footer.Children.Add(new TextBlock
            {
                Text = "No automatic hiding. Only the Playnite Hidden property changes after confirmation; no records or game files are deleted.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            });

            views = new TabControl();
            reviewTab = new TabItem { Header = "Duplicates" };
            similarTab = new TabItem { Header = "Possible matches" };
            hiddenTab = new TabItem { Header = "Hidden games" };
            reviewGrid = CreateGrid();
            hiddenGrid = CreateGrid();
            similarGrid = CreateGrid();

            var reviewContent = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            reviewSummary = new TextBlock { Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
            DockPanel.SetDock(reviewSummary, Dock.Top);
            reviewContent.Children.Add(reviewSummary);
            reviewContent.Children.Add(reviewGrid);
            reviewTab.Content = reviewContent;

            var hiddenContent = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            hiddenSummary = new TextBlock { Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
            DockPanel.SetDock(hiddenSummary, Dock.Top);
            hiddenContent.Children.Add(hiddenSummary);
            hiddenContent.Children.Add(hiddenGrid);
            hiddenTab.Content = hiddenContent;

            var similarContent = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            similarSummary = new TextBlock { Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
            DockPanel.SetDock(similarSummary, Dock.Top);
            similarContent.Children.Add(similarSummary);
            similarContent.Children.Add(similarGrid);
            similarTab.Content = similarContent;

            views.Items.Add(reviewTab);
            views.Items.Add(similarTab);
            views.Items.Add(hiddenTab);
            views.SelectedIndex = 0;
            views.SelectionChanged += (s, e) =>
            {
                // DataGrid selection events can bubble through this TabControl.
                if (Object.ReferenceEquals(e.Source, views)) UpdateButtonState();
            };
            main.Children.Add(views);
            window.Content = main;
            Refresh();
            UpdateButtonState();
            window.ShowDialog();
        }

        private static Button MakeButton(string label, Panel panel, Action action)
        {
            var button = new Button
            {
                Content = label,
                MinWidth = 104,
                Padding = new Thickness(11, 6, 11, 6),
                Margin = new Thickness(0, 0, 9, 4)
            };
            button.Click += (s, e) => action();
            panel.Children.Add(button);
            return button;
        }

        private static DataGrid CreateGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserReorderColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                SelectionMode = DataGridSelectionMode.Extended,
                SelectionUnit = DataGridSelectionUnit.FullRow
            };
            var markFactory = new FrameworkElementFactory(typeof(CheckBox));
            markFactory.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Marked")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            markFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Mark", Width = 55,
                CellTemplate = new DataTemplate { VisualTree = markFactory }
            });
            AddText(grid, "Game title", "Name", 300);
            AddText(grid, "Library / store", "Source", 165);
            AddText(grid, "Installed", "InstalledLabel", 82);
            AddText(grid, "Hidden", "HiddenLabel", 70);
            AddText(grid, "Platform", "Platforms", 140);
            AddText(grid, "Similar to", "SimilarTo", 230);
            return grid;
        }

        private static void AddText(DataGrid grid, string title, string property, double width)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = title,
                Binding = new Binding(property),
                Width = width,
                IsReadOnly = true
            });
        }

        private static ReviewRow Row(Game game)
        {
            return new ReviewRow
            {
                Id = game.Id,
                Key = Matcher.Key(game.Name),
                Name = game.Name,
                Source = Matcher.Source(game),
                Platforms = Matcher.Platforms(game),
                Installed = game.IsInstalled,
                IsHidden = game.Hidden,
                InstalledLabel = game.IsInstalled ? "Yes" : "No",
                HiddenLabel = game.Hidden ? "Yes" : "No"
            };
        }

        private void UpdateButtonState()
        {
            if (hideButton == null || restoreButton == null || views == null) return;
            hideButton.IsEnabled = !IsHiddenView;
            restoreButton.IsEnabled = IsHiddenView;
        }

        private void Refresh()
        {
            if (isUpdating) return;
            try
            {
                var all = api.Database.Games.ToList();
                var duplicateGroups = Matcher.FindGroups(all, false);
                reviewRows = duplicateGroups.SelectMany(g => g.Select(Row)).ToList();
                var similarPairs = Matcher.FindSimilarPairs(all);
                var similarById = new Dictionary<Guid, ReviewRow>();
                foreach (var pair in similarPairs)
                {
                    foreach (var pairMember in new[] {
                        Tuple.Create(pair.Item1, pair.Item2.Name),
                        Tuple.Create(pair.Item2, pair.Item1.Name) })
                    {
                        ReviewRow existing;
                        if (!similarById.TryGetValue(pairMember.Item1.Id, out existing))
                        {
                            existing = Row(pairMember.Item1);
                            existing.SimilarTo = pairMember.Item2;
                            similarById.Add(existing.Id, existing);
                        }
                        else if (!existing.SimilarTo.Split('|').Any(t =>
                            String.Equals(t.Trim(), pairMember.Item2, StringComparison.OrdinalIgnoreCase)))
                            existing.SimilarTo += " | " + pairMember.Item2;
                    }
                }
                similarRows = similarById.Values.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(r => r.Installed).ToList();
                hiddenRows = Matcher.FindHiddenMatches(all).Select(Row).ToList();

                reviewGrid.ItemsSource = null;
                reviewGrid.ItemsSource = reviewRows;
                similarGrid.ItemsSource = null;
                similarGrid.ItemsSource = similarRows;
                hiddenGrid.ItemsSource = null;
                hiddenGrid.ItemsSource = hiddenRows;

                reviewTab.Header = "Duplicates (" + duplicateGroups.Count + ")";
                similarTab.Header = "Possible matches (" + similarPairs.Count + ")";
                hiddenTab.Header = "Hidden games (" + hiddenRows.Count + ")";
                reviewSummary.Text = duplicateGroups.Count + " duplicate-title groups / " + reviewRows.Count
                    + " visible entries. Only titles with 2+ visible copies appear here. "
                    + "Highlight rows with Ctrl/Shift-click (or Ctrl+A), choose Mark highlighted, then Hide marked. Nothing is marked by default.";
                similarSummary.Text = similarPairs.Count + " possible-match pairs / " + similarRows.Count
                    + " visible entries. Differences in punctuation, edition labels, or small typos are suggestions only. "
                    + "A suggested match can be a different game or edition. Review titles and sources before marking anything.";
                hiddenSummary.Text = hiddenRows.Count + " hidden entries with matching or similar titles. "
                    + "This includes copies previously hidden in Playnite or by other extensions. "
                    + "Highlight rows, choose Mark highlighted, then Restore marked. "
                    + "A hidden entry may appear here even if only one copy remains visible.";
                UpdateButtonState();
            }
            catch (Exception e)
            {
                api.Dialogs.ShowErrorMessage("Could not read duplicate titles: " + e.Message, "Duplicate Review");
            }
        }

        private void MarkHighlighted(bool marked)
        {
            if (isUpdating) return;
            var selected = ActiveGrid.SelectedItems.Cast<object>().OfType<ReviewRow>().ToList();
            if (selected.Count == 0)
            {
                api.Dialogs.ShowMessage("First highlight one or more rows using Ctrl/Shift-click, or Ctrl+A. Then choose Mark highlighted.", "Duplicate Review");
                return;
            }
            Matcher.SetMarked(selected, marked);
            ActiveGrid.Items.Refresh();
        }

        private void Apply(bool hide)
        {
            if (isUpdating) return;
            if (hide == IsHiddenView)
            {
                api.Dialogs.ShowMessage("Use Duplicates or Possible matches to hide games, and Hidden games to restore them.", "Duplicate Review");
                return;
            }
            var selected = ActiveRows.Where(r => r.Marked && r.IsHidden != hide)
                .GroupBy(r => r.Id).Select(group => group.First()).ToList();
            if (selected.Count == 0)
            {
                api.Dialogs.ShowMessage("Mark " + (hide ? "visible" : "hidden") + " entries in this tab first.", "Duplicate Review");
                return;
            }
            // Never apply changes to stale entries. Also refuse to hide the last visible
            // copy (or sole remaining visible installed copy) of any duplicate title.
            var all = api.Database.Games.ToList();
            var current = all.ToDictionary(g => g.Id);
            var valid = new List<Game>();
            foreach (var row in selected)
            {
                Game game;
                if (!current.TryGetValue(row.Id, out game) ||
                    !String.Equals(Matcher.Key(game.Name), row.Key, StringComparison.OrdinalIgnoreCase) ||
                    game.Hidden == hide)
                {
                    api.Dialogs.ShowMessage("The library changed since this review opened. Refresh the list and try again.", "Duplicate Review");
                    Refresh();
                    return;
                }
                valid.Add(game);
            }

            if (hide)
            {
                var selectedIds = new HashSet<Guid>(valid.Select(g => g.Id));
                foreach (var game in valid)
                {
                    var visibleMatches = all.Where(g => !g.Hidden &&
                        Matcher.MatchesForVisibility(game.Name, g.Name)).ToList();
                    var remaining = visibleMatches.Where(g => !selectedIds.Contains(g.Id)).ToList();
                    if (remaining.Count == 0)
                    {
                        api.Dialogs.ShowMessage("Blocked: this would hide the last visible matching copy of '" +
                            game.Name + "'.\nLeave at least one confirmed matching copy visible.", "Duplicate Review");
                        return;
                    }
                    if (visibleMatches.Any(g => g.IsInstalled) && !remaining.Any(g => g.IsInstalled))
                    {
                        api.Dialogs.ShowMessage("Blocked: this would hide the last visible installed matching copy of '" +
                            game.Name + "'.\nKeep an installed copy visible.", "Duplicate Review");
                        return;
                    }
                }
            }

            var preview = String.Join("\n", valid.Take(12).Select(g =>
                "• " + g.Name + " [" + Matcher.Source(g) + "] " + (g.IsInstalled ? "(installed)" : "")));
            if (valid.Count > 12) preview += "\n... and " + (valid.Count - 12) + " more.";
            string verb = hide ? "HIDE" : "RESTORE";
            var result = api.Dialogs.ShowMessage(
                verb + " " + valid.Count + " library entries?\n\n" + preview
                + (IsSimilarView ? "\n\nWARNING: Similar titles may be different games or editions. Verify each entry before confirming." : "")
                + "\n\nOnly the Playnite Hidden property will be changed. Nothing is deleted.",
                "Confirm " + verb, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            int changed = 0;
            try
            {
                isUpdating = true;
                foreach (var game in valid)
                {
                    // Re-fetch in case other UI actions modified the object after preview.
                    var live = api.Database.Games[game.Id];
                    if (live == null || live.Hidden == hide ||
                        !String.Equals(Matcher.Key(live.Name), Matcher.Key(game.Name), StringComparison.OrdinalIgnoreCase))
                        continue;
                    live.Hidden = hide;
                    api.Database.Games.Update(live);
                    changed++;
                }
            }
            catch (Exception e)
            {
                api.Dialogs.ShowErrorMessage("Stopped after " + changed + " changes. No automatic rollback was attempted.\n" + e.Message,
                    "Duplicate Review");
            }
            finally
            {
                isUpdating = false;
                Refresh();
            }
            if (changed > 0)
                api.Dialogs.ShowMessage("Updated " + changed + " game entries. Open the Hidden games tab to restore hidden copies.", "Duplicate Review");
        }
    }
}
