using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Playnite.SDK.Models;

namespace DuplicateReview
{
    internal sealed class ReviewRow
    {
        public Guid Id { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string Source { get; set; }
        public string Platforms { get; set; }
        public string InstalledLabel { get; set; }
        public string HiddenLabel { get; set; }
        public bool Installed { get; set; }
        public bool IsHidden { get; set; }
        public bool Marked { get; set; }
        public string SimilarTo { get; set; }
    }

    internal static class Matcher
    {
        // Exact-title matching, except for case and repeated whitespace. Never strip edition,
        // platform, punctuation, roman numerals, or arbitrary words.
        internal static string Key(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return "";
            return Regex.Replace(name.Trim(), @"\s+", " ");
        }

        internal static List<List<Game>> FindGroups(IEnumerable<Game> games, bool includeHidden)
        {
            return games
                .Where(g => g != null && !String.IsNullOrWhiteSpace(g.Name))
                .GroupBy(g => Key(g.Name), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() >= 2 && (includeHidden || g.Count(x => !x.Hidden) >= 2))
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g
                    .Where(x => includeHidden || !x.Hidden)
                    .OrderByDescending(x => x.IsInstalled)
                    .ThenBy(x => x.Hidden)
                    .ThenBy(x => x.Source == null ? "" : x.Source.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Id)
                    .ToList())
                .ToList();
        }

        // Suggestions only. Similar-looking titles may be separate games, remasters,
        // or editions; never hide a suggestion automatically.
        internal static string LooseKey(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return "";
            var value = Regex.Replace(name.Trim().ToLowerInvariant(), @"[^\p{L}\p{Nd}]+", " ");
            value = Regex.Replace(value, @"\s+", " ").Trim();
            value = Regex.Replace(value, @"^(?:the|a|an)\s+", "");
            return value;
        }

        private static readonly string[] EditionSuffixes = {
            " game of the year edition", " digital deluxe edition", " definitive edition",
            " ultimate edition", " complete edition", " deluxe edition", " enhanced edition",
            " standard edition", " anniversary edition", " remastered edition",
            " directors cut", " director s cut", " goty edition", " gold edition", " special edition",
            " collectors edition", " hd remaster", " remastered", " definitive", " redux", " goty"
        };

        private static string EditionKey(string name)
        {
            var key = LooseKey(name);
            foreach (var suffix in EditionSuffixes.OrderByDescending(x => x.Length))
                if (key.EndsWith(suffix, StringComparison.Ordinal) &&
                    key.Length - suffix.Length >= 5)
                    return key.Substring(0, key.Length - suffix.Length);
            return key;
        }

        // Distinct sequels must not match, even if edit distance is small.
        // Roman numerals are checked as whole words (e.g. Diablo II vs Diablo III).
        private static string NumberSignature(string name)
        {
            var tokens = Regex.Matches(LooseKey(name),
                @"\b(?:\d+|i|ii|iii|iv|v|vi|vii|viii|ix|x|xi|xii|xiii|xiv|xv)\b",
                RegexOptions.IgnoreCase).Cast<Match>().Select(m => m.Value).ToArray();
            return String.Join("|", tokens);
        }

        private static int Distance(string a, string b, int limit)
        {
            var previous = Enumerable.Range(0, b.Length + 1).ToArray();
            var current = new int[b.Length + 1];
            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                int best = current[0];
                for (int j = 1; j <= b.Length; j++)
                {
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1,
                        previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                    best = Math.Min(best, current[j]);
                }
                if (best > limit) return limit + 1;
                var swap = previous;
                previous = current;
                current = swap;
            }
            return previous[b.Length];
        }

        internal static bool PossibleMatch(string first, string second)
        {
            if (String.IsNullOrWhiteSpace(first) || String.IsNullOrWhiteSpace(second) ||
                String.Equals(Key(first), Key(second), StringComparison.OrdinalIgnoreCase))
                return false; // Exact-title matches belong in the Duplicates tab.
            var a = LooseKey(first);
            var b = LooseKey(second);
            if (String.IsNullOrWhiteSpace(a) || String.IsNullOrWhiteSpace(b) || a == b)
                return a.Length >= 4 && a == b && NumberSignature(first) == NumberSignature(second);
            if (NumberSignature(first) != NumberSignature(second)) return false;
            if (EditionKey(first) == EditionKey(second)) return true;
            // Conservative typo matching: same beginning, similar length, <=2 edits.
            if (a.Length < 10 || b.Length < 10 || a.Substring(0, Math.Min(4, a.Length)) !=
                b.Substring(0, Math.Min(4, b.Length))) return false;
            int largest = Math.Max(a.Length, b.Length);
            int allowed = largest >= 12 ? 2 : 1;
            return Math.Abs(a.Length - b.Length) <= allowed && Distance(a, b, allowed) <= allowed;
        }

        internal static bool MatchesForVisibility(string first, string second)
        {
            return String.Equals(Key(first), Key(second), StringComparison.OrdinalIgnoreCase) ||
                   PossibleMatch(first, second);
        }

        internal static List<Tuple<Game, Game>> FindSimilarPairs(IEnumerable<Game> games,
            bool includeHidden = false)
        {
            var list = games.Where(g => g != null && !String.IsNullOrWhiteSpace(g.Name) &&
                    (includeHidden || !g.Hidden))
                .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ThenBy(g => g.Id).ToList();
            // Group by first normalized title token; protects large libraries from all-pairs scans.
            var buckets = list.GroupBy(g => LooseKey(g.Name).Split(' ')[0], StringComparer.OrdinalIgnoreCase);
            var result = new List<Tuple<Game, Game>>();
            foreach (var bucket in buckets)
            {
                var candidates = bucket.ToList();
                for (int i = 0; i < candidates.Count; i++)
                    for (int j = i + 1; j < candidates.Count; j++)
                        if (PossibleMatch(candidates[i].Name, candidates[j].Name))
                            result.Add(Tuple.Create(candidates[i], candidates[j]));
            }
            return result;
        }

        internal static List<Game> FindHiddenMatches(IEnumerable<Game> games)
        {
            var list = games.Where(g => g != null && !String.IsNullOrWhiteSpace(g.Name)).ToList();
            var ids = new HashSet<Guid>(FindHiddenDuplicates(list).Select(g => g.Id));
            foreach (var pair in FindSimilarPairs(list, true))
            {
                if (pair.Item1.Hidden) ids.Add(pair.Item1.Id);
                if (pair.Item2.Hidden) ids.Add(pair.Item2.Id);
            }
            return list.Where(g => g.Hidden && ids.Contains(g.Id))
                .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => Source(g), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Restore view: only hidden entries belonging to actual duplicate-title groups.
        // Includes copies hidden manually in Playnite or by other extensions; this
        // plugin deliberately does not keep a private history of who hid an entry.
        internal static List<Game> FindHiddenDuplicates(IEnumerable<Game> games)
        {
            return games
                .Where(g => g != null && !String.IsNullOrWhiteSpace(g.Name))
                .GroupBy(g => Key(g.Name), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() >= 2)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .SelectMany(group => group.Where(g => g.Hidden)
                    .OrderBy(g => Source(g), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(g => g.Id))
                .ToList();
        }

        // Marking never modifies Playnite data: only the in-memory review rows.
        internal static int SetMarked(IEnumerable<ReviewRow> selected, bool marked)
        {
            int count = 0;
            foreach (var row in selected.Where(row => row != null))
            {
                row.Marked = marked;
                count++;
            }
            return count;
        }

        internal static string Source(Game game)
        {
            return game.Source == null || String.IsNullOrWhiteSpace(game.Source.Name)
                ? "Unspecified / local" : game.Source.Name;
        }

        internal static string Platforms(Game game)
        {
            return game.Platforms == null || game.Platforms.Count == 0
                ? "" : String.Join(", ", game.Platforms.Select(p => p.Name));
        }
    }
}
