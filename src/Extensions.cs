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

        internal static bool Contains(this string source, char toCheck)
        {
            return source?.IndexOf(toCheck) >= 0;
        }

        internal static bool Contains(this string source, string toCheck, StringComparison comparer)
        {
            return source?.IndexOf(toCheck, comparer) >= 0;
        }

        internal static bool Contains(this ReadOnlySpan<byte> bytes, byte[] checkBytes)
        {
            if (checkBytes.Length > bytes.Length) return false;
            int ptr = 0;

            for (int i = 0; i < bytes.Length; i++)
            {
                // check if remaining length < length of checkBytes (take into account current progress)
                if (ptr + bytes.Length - i < checkBytes.Length) return false;
                // if scanned checkBytes.Length matching bytes in a row, must be true
                if (ptr == checkBytes.Length) return true;
                // increment ptr if this byte matches
                if (bytes[i] == checkBytes[ptr]) ptr++;
                // reset ptr if this byte does not match
                else ptr = 0;
            }

            // if the final checkBytes.Length bytes were a match for checkBytes, return true
            if (ptr == checkBytes.Length) return true;
            return false;
        }

        public static string ReplaceBulk (this string str, HashSet<char> toReplace, char replace)
        {
            char[] tmp = new char[str.Length];
            int ptr = 0;

            for (int i = 0; i < str.Length; i++)
            {
                if (toReplace.Contains(str[i])) tmp[ptr++] = replace;
                else tmp[ptr++] = str[i];
            }

            return tmp.ToString();
        }

        internal static bool ContainsOrdinalIgnore(this string[] items, string item)
        {
            if (items is null) return false;
            if (items.Length == 0) return false;
            return items.Any(x => x.Equals(item, StringComparison.OrdinalIgnoreCase));
        }

        internal static bool ContainsOrdinalIgnore(this HashSet<string> items, string item)
        {
            if (items is null) return false;
            if (items.Count == 0) return false;
            return items.Any(x => x.Equals(item, StringComparison.OrdinalIgnoreCase));
        }

        internal static bool ContainsKeyOrdinalIgnore<TKey, TValue>(this Dictionary<TKey, TValue> items, TKey item)
        {
            if (items is null) return false;
            if (items.Count == 0) return false;

            if (!(item is string key)) return items.ContainsKey(item);
            if (!(items is Dictionary<string, TValue> _items)) return items.ContainsKey(item);

            return _items.Keys.Any(x => x.Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        internal static unsafe string RemoveCharUnsafe(this string source, char toRemove)
        {
            char* newChars = stackalloc char[source.Length];
            char* currChar = newChars;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c == toRemove) continue;
                *currChar++ = c;
            }

            return new string(newChars, 0, (int)(currChar - newChars));
        }

        internal static string RemoveChar(this string source, char toRemove)
        {
            char[] temp = new char[source.Length];
            int ptr = 0;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c == toRemove) continue;
                temp[ptr++] = c;
            }

            return new string(temp, 0, ptr);
        }
    }
}
