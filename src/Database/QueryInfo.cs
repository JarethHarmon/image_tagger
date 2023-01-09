using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LiteDB;
using ImageTagger.Core;

namespace ImageTagger.Database
{
    internal sealed class SimilarityQueryResult
    {
        public string Hash { get; set; }
        public ulong Difference { get; set; }
        public ulong Average { get; set; }
        public ulong Wavelet { get; set; }
        public ulong Perceptual { get; set; }
        public float Similarity { get; set; }
    }

    internal sealed class QueryInfo
    {
        internal string Id { get; set; }
        internal string ImportId { get; set; }
        internal string GroupId { get; set; }

        internal string[] TagsAll { get; set; }
        internal string[] TagsAny { get; set; }
        internal string[] TagsNone { get; set; }
        internal List<Dictionary<ExpressionType, HashSet<string>>> TagsComplex { get; set; }

        internal TabType QueryType { get; set; }
        internal Sort Sort { get; set; }
        internal Order Order { get; set; }
        internal SortSimilarity SortSimilarity { get; set; }
        internal bool Filtered { get; set; }

        internal string SimilarityHash { get; set; }
        internal ulong AverageHash { get; set; }
        internal ulong DifferenceHash { get; set; }
        internal ulong WaveletHash { get; set; }
        internal ulong PerceptualHash { get; set; }
        internal float MinSimilarity { get; set; }
        internal int BucketPrecision { get; set; }

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

        internal QueryInfo()
        {
            Id = string.Empty;
            ImportId = Global.ALL;
            GroupId = string.Empty;

            QueryType = TabType.DEFAULT;
            Sort = Sort.HASH;
            Order = Order.ASCENDING;
            SortSimilarity = SortSimilarity.AVERAGE;
            Filtered = false;

            MinSimilarity = 81.25f;
            BucketPrecision = 3;

            TagsAll = Array.Empty<string>();
            TagsAny = Array.Empty<string>();
            TagsNone = Array.Empty<string>();

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

        private string CalcHashFromArray(string[] tags)
        {
            if (tags.Length == 0) return string.Empty;
            Array.Sort(tags); // to ensure ["A", "B"] and ["B", "A"] give the same hash
            string text = string.Join("?", tags);
            return CalcHashFromString(text);
        }

        private string CalcHashFromCondition(Dictionary<ExpressionType, HashSet<string>> condition)
        {
            string[] arr = new string[]
            {
                // really would like Span<T>, but Godot uses net472
                CalcHashFromArray(condition[ExpressionType.ALL].ToArray()),
                CalcHashFromArray(condition[ExpressionType.ANY].ToArray()),
                CalcHashFromArray(condition[ExpressionType.NONE].ToArray()),
            };
            return CalcHashFromString(string.Join("?", arr)); // to ensure they stay in correct relative order (instead of CalcHashFromArray() which calls Sort())
        }

        private string CalcHashFromComplexTags()
        {
            if (TagsComplex is null) return string.Empty;
            if (TagsComplex.Count == 0) return string.Empty;
            var list = new List<string>();

            foreach (var condition in TagsComplex)
                list.Add(CalcHashFromCondition(condition));

            // could also call CalcHashFromArray(list.ToArray())
            list.Sort(); // so order of individual conditions does not matter
            return CalcHashFromString(string.Concat(list));
        }

        internal void CalcId()
        {
            Id = "Q" + CalcHashFromString(string.Format("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}{11}{12}{13}{14}{15}{16}{17}{18}{19}{20}{21}{22}{23}{24}{25}{26}{27}{28}{29}{30}{31}{32}{33}{34}{35}{36}{37}",
                ImportId, GroupId, CalcHashFromArray(TagsAll), CalcHashFromArray(TagsAny), CalcHashFromArray(TagsNone), CalcHashFromComplexTags(),
                QueryType.ToString(), Sort.ToString(), Order.ToString(), SortSimilarity.ToString(), SimilarityHash, AverageHash, DifferenceHash, WaveletHash, PerceptualHash,
                MinWidth, MaxWidth, MinHeight, MaxHeight, MinSize, MaxSize, MinCreationTime, MaxCreationTime, MinUploadTime, MaxUploadTime, MinLastEditTime, Success,
                MaxLastEditTime, MinLastWriteTime, MaxLastWriteTime, MinTagCount, MaxTagCount, MinRatingSum, MaxRatingSum, MinRatingAvg, MaxRatingAvg, MinSimilarity, BucketPrecision
            ));
        }
    }
}
