using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using LiteDB;
using ImageTagger.Core;

namespace ImageTagger.Database
{
    public sealed class SimilarityQueryResult
    {
        public string Hash { get; set; }
        public ulong Difference { get; set; }
        public ulong Average { get; set; }
        public ulong Wavelet { get; set; }
        public ulong Perceptual { get; set; }
        public float Similarity { get; set; }
    }

    public sealed class QueryInfo
    {
        public string Id { get; set; }

        public Filters Groups { get; set; }
        public Filters Creators { get; set; }
        public Filters Copyrights { get; set; }
        public Filters Subjects { get; set; }
        public Filters Descriptive { get; set; }
        public Filters Folders { get; set; }

        public TabType QueryType { get; set; }
        public Sort Sort { get; set; }
        public Order Order { get; set; }
        public SortSimilarity SortSimilarity { get; set; }
        public bool Filtered { get; set; }
        public Colors[] Colors { get; set; }

        public string SimilarityHash { get; set; }
        public ulong AverageHash { get; set; }
        public ulong DifferenceHash { get; set; }
        public ulong WaveletHash { get; set; }
        public ulong PerceptualHash { get; set; }
        public float MinSimilarity { get; set; }
        public int BucketPrecision { get; set; }

        public int MinWidth { get; set; }
        public int MaxWidth { get; set; }
        public int MinHeight { get; set; }
        public int MaxHeight { get; set; }
        public long MinSize { get; set; }
        public long MaxSize { get; set; }

        public long MinCreationTime { get; set; }
        public long MaxCreationTime { get; set; }
        public long MinUploadTime { get; set; }
        public long MaxUploadTime { get; set; }
        public long MinLastWriteTime { get; set; }
        public long MaxLastWriteTime { get; set; }
        public long MinLastEditTime { get; set; }
        public long MaxLastEditTime { get; set; }

        public int MinTagCount { get; set; }
        public int MaxTagCount { get; set; }
        public int MinRatingSum { get; set; }
        public int MaxRatingSum { get; set; }
        public float MinRatingAvg { get; set; }
        public float MaxRatingAvg { get; set; }

        public int Success { get; set; }
        public ILiteQueryable<ImageInfo> Query { get; set; }
        public ILiteQueryableResult<string> Results { get; set; }
        public IEnumerable<string> ResultsRandom { get; set; }
        public int LastQueriedCount { get; set; }

        public QueryInfo()
        {
            Id = string.Empty;

            Folders = new Filters();
            Groups = new Filters();
            Creators = new Filters();
            Copyrights = new Filters();
            Subjects = new Filters();
            Descriptive = new Filters();

            QueryType = TabType.Default;
            Sort = Sort.Hash;
            Order = Order.Ascending;
            SortSimilarity = SortSimilarity.Average;
            Filtered = false;

            MinSimilarity = Global.Settings.MinSimilarity;
            BucketPrecision = 3;
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

        private string CalcHashFromArray(string[] tags)
        {
            if (tags.Length == 0) return string.Empty;
            Array.Sort(tags); // to ensure ["A", "B"] and ["B", "A"] give the same hash
            string text = string.Join("?", tags);
            return CalcHashFromString(text);
        }

        private string CalcHashFromArray(Colors[] colors)
        {
            string[] _colors = new string[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                _colors[i] = colors[i].ToString();
            return CalcHashFromArray(_colors);
        }

        private string CalcHashFromCondition(Dictionary<ExpressionType, string[]> condition)
        {
            string[] arr = new string[]
            {
                // really would like Span<T>, but Godot uses net472
                CalcHashFromArray(condition[ExpressionType.All]),
                CalcHashFromArray(condition[ExpressionType.Any]),
                CalcHashFromArray(condition[ExpressionType.None]),
            };
            return CalcHashFromString(string.Join("?", arr)); // to ensure they stay in correct relative order (instead of CalcHashFromArray() which calls Sort())
        }

        private string CalcHashFromComplexTags(Filters filter)
        {
            if (filter.Complex.Length == 0) return string.Empty;
            var list = new List<string>();

            foreach (var condition in filter.Complex)
                list.Add(CalcHashFromCondition(condition));

            // could also call CalcHashFromArray(list.ToArray())
            list.Sort(); // so order of individual conditions does not matter
            return CalcHashFromString(string.Concat(list));
        }

        private string CalcHashFromFilter(Filters filter)
        {
            string temp = $"{CalcHashFromArray(filter.All)}?{CalcHashFromArray(filter.Any)}?{CalcHashFromArray(filter.None)}?";
            temp += CalcHashFromComplexTags(filter);
            return CalcHashFromString(temp);
        }

        public string CalcId()
        {
            Id = CalcHashFromString(string.Format("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}{11}{12}{13}{14}{15}{16}{17}{18}{19}{20}{21}{22}{23}{24}{25}{26}{27}{28}{29}{30}{31}{32}{33}{34}{35}{36}{37}{38}",
                CalcHashFromFilter(Folders), CalcHashFromFilter(Groups), CalcHashFromFilter(Creators), CalcHashFromFilter(Copyrights), CalcHashFromFilter(Subjects), CalcHashFromFilter(Descriptive),
                QueryType.ToString(), Sort.ToString(), Order.ToString(), SortSimilarity.ToString(), SimilarityHash, AverageHash, DifferenceHash, WaveletHash, PerceptualHash,
                MinWidth, MaxWidth, MinHeight, MaxHeight, MinSize, MaxSize, MinCreationTime, MaxCreationTime, MinUploadTime, MaxUploadTime, MinLastEditTime, Success, CalcHashFromArray(Colors),
                MaxLastEditTime, MinLastWriteTime, MaxLastWriteTime, MinTagCount, MaxTagCount, MinRatingSum, MaxRatingSum, MinRatingAvg, MaxRatingAvg, MinSimilarity, BucketPrecision
            ));
            return Id;
        }

        public QueryInfo Clone()
        {
            QueryInfo queryInfo = this;
            return new QueryInfo
            {
                Id = queryInfo.Id,

                Folders = queryInfo.Folders,
                Groups = queryInfo.Groups,
                Creators = queryInfo.Creators,
                Copyrights = queryInfo.Copyrights,
                Subjects = queryInfo.Subjects,
                Descriptive = queryInfo.Descriptive,

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
                MinSimilarity = queryInfo.MinSimilarity,
                BucketPrecision = queryInfo.BucketPrecision,

                Colors = queryInfo.Colors,

                // numerical

                Success = queryInfo.Success,
                LastQueriedCount = -1,
            };
        }
    }
}
