using System.Collections.Generic;
using System.Text;

namespace LoneFighter.Debugging.Console
{
    /// <summary>
    /// Tab-completion for the developer console. Given the current input, finds all registered
    /// commands whose name starts with the leading token and returns the longest common prefix
    /// (LCP) of the matches. If there is exactly one match the full command name is returned.
    /// </summary>
    public static class ConsoleAutoComplete
    {
        /// <summary>
        /// Attempts to autocomplete <paramref name="input"/>. If the user has already typed a space
        /// (i.e. they are editing arguments) the input is returned unchanged.
        /// </summary>
        /// <param name="input">Current input field text.</param>
        /// <param name="matches">All command names that share the typed prefix (case-insensitive).</param>
        /// <returns>Completed input with the command portion replaced by the LCP.</returns>
        public static string Complete(string input, out List<string> matches)
        {
            matches = new List<string>();
            if (string.IsNullOrEmpty(input)) return input;

            // Don't autocomplete past the first token — arguments are command-specific.
            int firstSpace = input.IndexOf(' ');
            if (firstSpace >= 0) return input;

            string prefix = input;
            var all = ConsoleCommandRegistry.AllNames();
            for (int i = 0; i < all.Count; i++)
            {
                if (StartsWithCI(all[i], prefix)) matches.Add(all[i]);
            }

            if (matches.Count == 0) return input;
            if (matches.Count == 1) return matches[0];

            string lcp = LongestCommonPrefix(matches);
            // Only extend the input if the LCP is at least as long as what was typed.
            if (lcp.Length > prefix.Length) return lcp;
            return prefix;
        }

        private static bool StartsWithCI(string s, string prefix)
        {
            if (s == null || prefix == null) return false;
            if (s.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
            {
                if (char.ToLowerInvariant(s[i]) != char.ToLowerInvariant(prefix[i])) return false;
            }
            return true;
        }

        private static string LongestCommonPrefix(List<string> names)
        {
            if (names.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            string first = names[0];
            for (int i = 0; i < first.Length; i++)
            {
                char c = char.ToLowerInvariant(first[i]);
                for (int j = 1; j < names.Count; j++)
                {
                    if (i >= names[j].Length || char.ToLowerInvariant(names[j][i]) != c)
                        return sb.ToString();
                }
                // Preserve the casing from the first match — registry stores canonical names.
                sb.Append(first[i]);
            }
            return sb.ToString();
        }
    }
}
