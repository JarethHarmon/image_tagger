using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ImageTagger
{
    internal static class Extensions
    {
        // potentially @"\d+" instead
        private static readonly Regex regex = new Regex(@"\d+([\.,]\d)?", RegexOptions.Compiled);

        internal static IEnumerable<T> OrderByNatural<T>(this IEnumerable<T> items, Func<T, string> selector, StringComparer comparer = null, bool desc = false)
        {
            int digits = items
                .SelectMany(i => regex.Matches(selector(i))
                .Cast<Match>()
                .Select(chunk => (int?)chunk.Value.Length))
                .Max() ?? 0;

            return desc
                ? items.OrderByDescending(i => regex
                    .Replace(selector(i), match => match.Value.PadLeft(digits, '0')),
                    comparer ?? StringComparer.CurrentCulture)

                : items.OrderBy(i => regex
                    .Replace(selector(i), match => match.Value.PadLeft(digits, '0')),
                    comparer ?? StringComparer.CurrentCulture);
        }

        internal static bool Contains(this string source, string toCheck, StringComparison comparer)
        {
            return source?.IndexOf(toCheck, comparer) >= 0;
        }
    }
}
