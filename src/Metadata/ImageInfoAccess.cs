using System;
using System.Collections.Generic;
using System.Linq;
using ImageTagger.Core;
using ImageTagger.Database;

namespace ImageTagger.Metadata
{
    internal sealed class ImageInfoAccess
    {
        private static ImageInfo currentImageInfo = null;

        internal static ImageInfo GetCurrentImageInfo()
        {
            return currentImageInfo;
        }

        internal static void SetCurrentImageInfo(string hash)
        {
            var info = GetImageInfo(hash);
            currentImageInfo = info;
        }

        internal static string GetCurrentHash()
        {
            return currentImageInfo?.Hash ?? string.Empty;
        }

        internal static ImageInfo GetImageInfo(string hash)
        {
            if (currentImageInfo?.Hash.Equals(hash, System.StringComparison.InvariantCultureIgnoreCase) ?? false)
                return currentImageInfo;
            return DatabaseAccess.FindImageInfo(hash);
        }

        internal static ImageInfo[] GetImageInfo(HashSet<string> hashes)
        {
            var query = DatabaseAccess.GetImageInfoQuery();
            query = query?.Where(x => hashes.Contains(x.Hash));
            return query?.ToArray() ?? Array.Empty<ImageInfo>();
        }

        internal static string[] GetImportIds(string hash)
        {
            var info = GetImageInfo(hash);
            if (info?.Imports.Count == 0) return Array.Empty<string>();
            return info.Imports.ToArray();
        }

        internal static void AddRating(string[] hashes, string rating, int value)
        {
            var _hashes = new HashSet<string>(hashes);
            var infos = GetImageInfo(_hashes);

            foreach (var info in infos)
            {
                info.Ratings[rating] = value;
                info.RatingSum = info.Ratings.Values.Sum();
                info.RatingAvg = info.RatingSum / info.Ratings.Count;
            }

            if (_hashes.Contains(currentImageInfo?.Hash))
            {
                currentImageInfo.Ratings[rating] = value;
                currentImageInfo.RatingSum = currentImageInfo.Ratings.Values.Sum();
                currentImageInfo.RatingAvg = currentImageInfo.RatingSum / currentImageInfo.Ratings.Count;
            }

            DatabaseAccess.UpdateImageInfo(infos);
        }

        internal static int GetRating(string hash, string rating)
        {
            var info = GetImageInfo(hash);
            if (info?.Ratings.TryGetValue(rating, out int result) ?? false)
                return result;
            return 0;
        }

        // for things like this, need to look into whether it is faster to convert to hashset or if I should just use array
        internal static void AddTags(string[] hashes, string[] tags)
        {
            if (hashes.Length == 0 || tags.Length == 0) return;
            var _hashes = new HashSet<string>(hashes);

            if (currentImageInfo != null && _hashes.Contains(currentImageInfo.Hash))
                currentImageInfo.Tags.UnionWith(tags);

            var infos = GetImageInfo(_hashes);
            foreach (var info in infos)
                info.Tags.UnionWith(tags);

            DatabaseAccess.UpdateImageInfo(infos);
        }

        internal static void RemoveTags(string[] hashes, string[] tags)
        {
            if (hashes.Length == 0 || tags.Length == 0) return;
            var _hashes = new HashSet<string>(hashes);

            if (currentImageInfo != null && _hashes.Contains(currentImageInfo.Hash))
                currentImageInfo.Tags.ExceptWith(tags);

            var infos = GetImageInfo(_hashes);
            foreach (var info in infos)
                info.Tags.ExceptWith(tags);

            DatabaseAccess.UpdateImageInfo(infos);
        }
    }
}