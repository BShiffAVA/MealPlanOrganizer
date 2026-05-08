using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MealPlanOrganizer.Functions.Services
{
    public static class TagHelper
    {
        private static readonly Regex ValidChars = new Regex(@"[^a-z0-9\-]", RegexOptions.Compiled);

        /// <summary>
        /// Normalizes a tag: strips # prefix, lowercases, trims whitespace,
        /// replaces spaces with hyphens, removes invalid characters, truncates to 50 chars.
        /// Returns null if the result is empty.
        /// </summary>
        public static string? Normalize(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;

            var normalized = tag.Trim()
                .TrimStart('#')
                .ToLowerInvariant()
                .Replace(' ', '-');

            normalized = ValidChars.Replace(normalized, string.Empty);
            normalized = normalized.Trim('-');

            if (normalized.Length == 0) return null;
            return normalized.Length > 50 ? normalized[..50] : normalized;
        }

        /// <summary>
        /// Normalizes a list of tags, deduplicates, and removes nulls/empty.
        /// </summary>
        public static List<string> NormalizeAll(IEnumerable<string>? tags)
        {
            if (tags == null) return new List<string>();
            return tags
                .Select(Normalize)
                .Where(t => t != null)
                .Distinct()
                .Cast<string>()
                .ToList();
        }
    }
}
