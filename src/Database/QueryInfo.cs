using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using LiteDB;
using ImageTagger.Core;
using ImageTagger.Extension;

namespace ImageTagger.Database
{
    internal sealed class QueryInfo
    {
        internal string Id { get; set; }

        internal Filter Folders { get; set; }
        internal Filter Imports { get; set; }
        internal Filter Groups { get; set; }
        internal Filter Creators { get; set; }
        internal Filter Copyrights { get; set; }
        internal Filter Subjects { get; set; }
        internal Filter Descriptive { get; set; }

        internal string ImportId { get; set; }
        internal string GroupId { get; set; }

        internal string[] TagsAll { get; set; }
        internal string[] TagsAny { get; set; }
        internal string[] TagsNone { get; set; }
        internal Dictionary<FilterType, string[]>[] TagsComplex { get; set; }

        internal TabType QueryType { get; set; }
        internal Sort Sort { get; set; }
        internal Order Order { get; set; }
        internal SortSimilarity SortSimilarity { get; set; }
        internal bool Filtered { get; set; }
        internal Colors[] Colors { get; set; }

        internal string SimilarityHash { get; set; }
        internal ulong AverageHash { get; set; }
        internal ulong DifferenceHash { get; set; }
        internal ulong WaveletHash { get; set; }
        internal ulong PerceptualHash { get; set; }
        internal ulong ColorHash { get; set; }
        internal float MinSimilarity { get; set; }
        internal ushort[] Buckets { get; set; }

        internal int MinWidth { get; set; }
        internal int MaxWidth { get; set; }
        internal int MinHeight { get; set; }
        internal int MaxHeight { get; set; }
        internal long MinSize { get; set; }
        internal long MaxSize { get; set; }

        internal long MinCreationTime { get; set; }
        internal long MaxCreationTime { get; set; }
        internal long MinUploadTime { get; set; }
        internal long MaxUploadTime { get; set; }
        internal long MinLastWriteTime { get; set; }
        internal long MaxLastWriteTime { get; set; }
        internal long MinLastEditTime { get; set; }
        internal long MaxLastEditTime { get; set; }

        internal int MinTagCount { get; set; }
        internal int MaxTagCount { get; set; }
        internal int MinRatingSum { get; set; }
        internal int MaxRatingSum { get; set; }
        internal float MinRatingAvg { get; set; }
        internal float MaxRatingAvg { get; set; }

        internal int Success { get; set; }
        internal ILiteQueryable<ImageInfo> Query { get; set; }
        internal ILiteQueryableResult<string> Results { get; set; }
        internal IEnumerable<string> ResultsRandom { get; set; }
        internal int LastQueriedCount { get; set; }

        private static readonly ushort[] defaultBuckets = new ushort[4];
        internal QueryInfo()
        {
            Id = string.Empty;
            ImportId = Global.ALL;
            GroupId = string.Empty;

            QueryType = TabType.Default;
            Sort = Sort.Hash;
            Order = Order.Ascending;
            SortSimilarity = SortSimilarity.Average;
            Filtered = false;

            MinSimilarity = Global.Settings.MinSimilarity;
            Buckets = defaultBuckets;

            TagsAll = Array.Empty<string>();
            TagsAny = Array.Empty<string>();
            TagsNone = Array.Empty<string>();
            Colors = Array.Empty<Colors>();

            MinWidth = -1;
            MaxWidth = -1;
            MinHeight = -1;
            MaxHeight = -1;
            MinSize = -1;
            MaxSize = -1;

            MinCreationTime = -1;
            MaxCreationTime = -1;
            MinUploadTime = -1;
            MaxUploadTime = -1;
            MinLastWriteTime = -1;
            MaxLastWriteTime = -1;
            MinLastEditTime = -1;
            MaxLastEditTime = -1;

            MinTagCount = -1;
            MaxTagCount = -1;
            MinRatingSum = -1;
            MaxRatingSum = -1;
            MinRatingAvg = -1;
            MaxRatingAvg = -1;
        }

        private string CalcHashFromString(string text)
        {
            var hash = SHA256.Create();
            byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(text));
            var sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("x2"));
            hash.Dispose();
            return sb.ToString();
        }

        private string CalcHashFromArray(string[] members)
        {
            if (members.Length == 0) return string.Empty;
            Array.Sort(members); // to ensure ["A", "B"] and ["B", "A"] give the same hash
            string text = string.Join("?", members);
            return CalcHashFromString(text);
        }

        private string CalcHashFromArray(Colors[] colors)
        {
            string[] _colors = new string[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                _colors[i] = colors[i].ToString();
            return CalcHashFromArray(_colors);
        }

        private string CalcHashFromCondition(Dictionary<FilterType, string[]> condition)
        {
            string[] arr = new string[]
            {
                CalcHashFromArray(condition[FilterType.All]),
                CalcHashFromArray(condition[FilterType.Any]),
                CalcHashFromArray(condition[FilterType.None]),
            };
            return CalcHashFromString(string.Join("?", arr)); // to ensure they stay in correct relative order (instead of CalcHashFromArray() which calls Sort())
        }

        private string CalcHashFromComplexTags()
        {
            if (TagsComplex is null) return string.Empty;
            if (TagsComplex.Length == 0) return string.Empty;
            var list = new List<string>();

            foreach (var condition in TagsComplex)
                list.Add(CalcHashFromCondition(condition));

            // could also call CalcHashFromArray(list.ToArray())
            list.Sort(); // so order of individual conditions does not matter
            return CalcHashFromString(string.Concat(list));
        }

        private string CalcHashFromComplex(List<Dictionary<FilterType, string[]>> complex)
        {
            if (complex.Count == 0) return string.Empty;
            var list = new List<string>();

            foreach (var condition in complex)
                list.Add(CalcHashFromCondition(condition));

            list.Sort(); // so order of individual conditions does not matter
            return CalcHashFromString(string.Concat(list));
        }

        private string CalcHashFromFilter(Filter filter)
        {
            var list = new List<string>
            {
                CalcHashFromArray(filter.All),
                CalcHashFromArray(filter.Any),
                CalcHashFromArray(filter.None),
                CalcHashFromComplex(filter.Complex)
            };
            return CalcHashFromString(string.Concat(list));
        }

        // similarityHash (if set) should be the only perceptualHash needed
        internal string CalcId()
        {
            Id = "Q" + CalcHashFromString(string.Format("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}{11}{12}{13}{14}{15}{16}{17}{18}{19}{20}{21}{22}{23}{24}{25}{26}{27}{28}{29}{30}{31}{32}{33}{34}{35}{36}{37}{38}{39}",
                ImportId, GroupId, CalcHashFromArray(TagsAll), CalcHashFromArray(TagsAny), CalcHashFromArray(TagsNone), CalcHashFromComplexTags(),
                QueryType.ToString(), Sort.ToString(), Order.ToString(), SortSimilarity.ToString(), SimilarityHash, AverageHash, DifferenceHash, WaveletHash, PerceptualHash, ColorHash,
                MinWidth, MaxWidth, MinHeight, MaxHeight, MinSize, MaxSize, MinCreationTime, MaxCreationTime, MinUploadTime, MaxUploadTime, MinLastEditTime, Success, CalcHashFromArray(Colors),
                MaxLastEditTime, MinLastWriteTime, MaxLastWriteTime, MinTagCount, MaxTagCount, MinRatingSum, MaxRatingSum, MinRatingAvg, MaxRatingAvg, MinSimilarity, Global.Settings.BucketVariance
            ));
            return Id;
        }

        internal QueryInfo Clone()
        {
            QueryInfo queryInfo = this;
            return new QueryInfo
            {
                Id = queryInfo.Id,
                ImportId = queryInfo.ImportId,
                GroupId = queryInfo.GroupId,

                TagsAll = queryInfo.TagsAll,
                TagsAny = queryInfo.TagsAny,
                TagsNone = queryInfo.TagsNone,
                TagsComplex = queryInfo.TagsComplex,

                QueryType = queryInfo.QueryType,
                Sort = queryInfo.Sort,
                Order = queryInfo.Order,
                SortSimilarity = queryInfo.SortSimilarity,
                Filtered = queryInfo.Filtered,

                SimilarityHash = queryInfo.SimilarityHash,
                AverageHash = queryInfo.AverageHash,
                DifferenceHash = queryInfo.DifferenceHash,
                PerceptualHash = queryInfo.PerceptualHash,
                WaveletHash = queryInfo.WaveletHash,
                ColorHash = queryInfo.ColorHash,
                MinSimilarity = queryInfo.MinSimilarity,

                Colors = queryInfo.Colors,
                Buckets = queryInfo.Buckets,
                // numerical

                Success = queryInfo.Success,
                LastQueriedCount = -1,
            };
        }

        private static readonly ulong[] PerceptualHashes = new ulong[5];
        internal ulong[] GetPerceptualHashes()
        {
            PerceptualHashes[0] = AverageHash;
            PerceptualHashes[1] = ColorHash;
            PerceptualHashes[2] = DifferenceHash;
            PerceptualHashes[3] = PerceptualHash;
            PerceptualHashes[4] = WaveletHash;
            return PerceptualHashes;
        }
    }
}
