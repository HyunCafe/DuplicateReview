$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$matching = Get-Content -LiteralPath (Join-Path $project 'Source/Matching.cs') -Raw -Encoding UTF8
$testCSharp = @'
namespace Playnite.SDK.Models
{
    public sealed class GameSource { public string Name { get; set; } }
    public sealed class Platform { public string Name { get; set; } }
    public sealed class Game
    {
        public System.Guid Id { get; set; }
        public string Name { get; set; }
        public bool Hidden { get; set; }
        public bool IsInstalled { get; set; }
        public GameSource Source { get; set; }
        public System.Collections.Generic.List<Platform> Platforms { get; set; }
    }
}
namespace DuplicateReview
{
    public static class MatchingRegressionTests
    {
        private static void Require(bool ok, string message)
        {
            if (!ok) throw new System.Exception(message);
        }
        private static Playnite.SDK.Models.Game Game(string name, bool hidden, bool installed)
        {
            return new Playnite.SDK.Models.Game {
                Id = System.Guid.NewGuid(), Name = name, Hidden = hidden,
                IsInstalled = installed
            };
        }
        public static void Run()
        {
            var games = new System.Collections.Generic.List<Playnite.SDK.Models.Game> {
                Game("Behind the Frame", false, false),
                Game(" BEHIND  the Frame ", false, true),
                Game("Game", false, true),
                Game("Game: Definitive Edition", false, false),
                Game("Another Game", false, true),
                Game("Another Game", true, false),
                Game("Unique", false, true),
                Game("  ", false, true),
                Game(null, false, true)
            };
            var visibleGroups = Matcher.FindGroups(games, false);
            Require(visibleGroups.Count == 1, "Unexpected number of visible duplicate groups");
            Require(visibleGroups[0].Count == 2, "Title matching did not normalize whitespace and case");
            Require(visibleGroups[0][0].IsInstalled, "Installed copies not prioritized");
            Require(Matcher.Key("x  Y") == "x Y", "Normalization changed unexpectedly");
            var allGroups = Matcher.FindGroups(games, true);
            Require(allGroups.Count == 2, "Hidden groups were not included in restoration view");
            Require(allGroups.Any(x => x.Count == 2 && x.Any(g => g.Hidden)), "Missing hidden member");
            Require(Matcher.Source(games[0]) == "Unspecified / local", "Null source not handled");
            Require(Matcher.Platforms(games[0]) == "", "Empty platforms not handled");
            Require(!games[0].Hidden && games[5].Hidden, "Read-only matching mutated game records");
            var hidden = Matcher.FindHiddenDuplicates(games);
            Require(hidden.Count == 1 && hidden[0].Name == "Another Game", "Hidden tab failed to include the only hidden duplicate");
            var extended = new System.Collections.Generic.List<Playnite.SDK.Models.Game>(games);
            extended.Add(Game("Hidden-only group", true, false));
            extended.Add(Game("Hidden-only group", true, false));
            extended.Add(Game("Unique hidden title", true, false));
            var hiddenExtended = Matcher.FindHiddenDuplicates(extended);
            Require(hiddenExtended.Count == 3, "Hidden tab lost fully hidden groups or included non-duplicates");
            Require(hiddenExtended.All(g => g.Hidden), "Hidden tab leaked visible rows");
            Require(hiddenExtended.All(g => g.Name != "Unique hidden title"), "Hidden tab included unrelated hidden singleton");

            Require(Matcher.PossibleMatch("The Witcher 3: Wild Hunt", "Witcher 3 Wild Hunt"), "Article/punctuation alias not detected");
            Require(Matcher.PossibleMatch("Control Ultimate Edition", "Control"), "Edition label not detected");
            Require(Matcher.PossibleMatch("Batman: Arkham Asylum", "Batman Arkham Asylum"), "Punctuation mismatch not detected");
            Require(Matcher.PossibleMatch("Hollow Knigth", "Hollow Knight"), "Minor typo not detected");
            Require(!Matcher.PossibleMatch("Diablo II", "Diablo III"), "Roman numeral sequel conflated");
            Require(!Matcher.PossibleMatch("Final Fantasy X", "Final Fantasy X-2"), "Numbered sequel conflated");
            Require(!Matcher.PossibleMatch("Hollow Knight", "Hollow Knight Silksong"), "Different subtitle conflated");
            var near = new [] {
                Game("The Witcher 3: Wild Hunt", false, true),
                Game("Witcher 3 Wild Hunt", false, false),
                Game("Control", false, true),
                Game("Control Ultimate Edition", true, false),
                Game("Hollow Knight", false, false),
                Game("Hollow Knight Silksong", false, true)
            };
            var pairs = Matcher.FindSimilarPairs(near);
            Require(pairs.Count == 1 && Matcher.MatchesForVisibility(pairs[0].Item1.Name, pairs[0].Item2.Name),
                "Only visible likely matches should appear in suggestions");
            var hiddenNear = Matcher.FindHiddenMatches(near);
            Require(hiddenNear.Count == 1 && hiddenNear[0].Name == "Control Ultimate Edition",
                "Hidden suggestions not recoverable");
            var rowA = new ReviewRow { Name = "A" };
            var rowB = new ReviewRow { Name = "B" };
            var rowC = new ReviewRow { Name = "C" };
            Require(Matcher.SetMarked(new [] { rowA, rowC }, true) == 2,
                "Bulk mark should update two highlighted rows");
            Require(rowA.Marked && !rowB.Marked && rowC.Marked,
                "Bulk marking changed an unselected row");
            Matcher.SetMarked(new [] { rowA }, false);
            Require(!rowA.Marked && rowC.Marked, "Unmark highlighted touched other marked rows");
            System.Console.WriteLine("PASS: duplicate matching, separate hidden-group view, and targeted bulk mark/unmark tests");
        }
    }
}
'@
# Compile exactly the shipped matching implementation with minimal Playnite model stubs.
Add-Type -TypeDefinition ($matching + [Environment]::NewLine + $testCSharp) -ErrorAction Stop
[DuplicateReview.MatchingRegressionTests]::Run()
# Lightweight source-policy checks, not a malware certification.
$plugin = Get-Content -LiteralPath (Join-Path $project 'Source/Plugin.cs') -Raw
$window = Get-Content -LiteralPath (Join-Path $project 'Source/ReviewWindow.cs') -Raw
if (($window -split 'Database.Games.Update\(').Count -ne 2) { throw 'Unexpected count of database update callsites.' }
if ($window -notmatch 'MessageBoxButton.YesNo' -or $window -notmatch 'remaining.Count == 0') { throw 'Required confirmation / last-copy gate absent.' }
if ($window -notmatch 'new TabControl' -or $window -notmatch 'Possible matches' -or $window -notmatch 'Hidden games' -or
    $window -notmatch 'FindHiddenMatches' -or $window -notmatch 'SelectedItems' -or
    $window -notmatch 'SetMarked\(selected, marked\)' -or $window -notmatch 'SelectionMode = DataGridSelectionMode.Extended') {
    throw 'Missing separate Hidden tab or bulk highlight-mark controls.'
}
if (($plugin + $window) -match '(?i)System\.Net|HttpClient|WebClient|Process\.Start|File\.Delete|Directory\.Delete|OnApplicationStarted|OnLibraryUpdated') {
    throw 'Unexpected network, process, filesystem deletion or automatic-execution API in plugin source.'
}
Write-Host 'PASS: narrow permissions policy, bulk selection, separate Hidden tab, and confirmation gates.'
