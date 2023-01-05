using System;
using System.Security.Cryptography;

namespace ImageTagger
{
    public enum Error {  OK, GENERIC, DATABASE, DICTIONARY, IO, PYTHON }
    public enum ImportStatus {  SUCCESS, DUPLICATE, IGNORED, FAILED }

    public enum ImageType { JPEG, PNG, APNG, GIF, WEBP, OTHER=15, ERROR=-1 }
    public enum Order { ASCENDING, DESCENDING }
    public enum Sort { HASH, PATH, NAME, SIZE, UPLOAD, CREATION, LAST_WRITE, LAST_EDIT, DIMENSIONS, WIDTH, HEIGHT, TAG_COUNT, QUALITY, APPEAL, ART_STYLE,
        RED, GREEN, BLUE, ALPHA, LIGHT, DARK, YELLOW, CYAN, FUSCHIA, LIGHT_RED, DARK_RED, LIGHT_GREEN, DARK_GREEN, LIGHT_BLUE, DARK_BLUE, LIGHT_YELLOW,
        DARK_YELLOW, LIGHT_CYAN, DARK_CYAN, LIGHT_FUSCHIA, DARK_FUSCHIA, RANDOM }
    public enum SortSimilarity { AVERAGED, AVERAGE, DIFFERENCE, WAVELET }

    public enum TabType { DEFAULT, SIMILARITY }
    public enum ExpressionType { ALL, ANY, NONE }

    public sealed class Global
    {
        public const string ALL = "All";
        public const int MAX_PATH_LENGTH = 256, THUMBNAIL_SIZE = 256;

        public static Settings Settings = Settings.LoadFromJsonFile();

        private static readonly byte[] _bitCounts =
        {
            0,1,1,2,1,2,2,3, 1,2,2,3,2,3,3,4, 1,2,2,3,2,3,3,4, 2,3,3,4,3,4,4,5,
            1,2,2,3,2,3,3,4, 2,3,3,4,3,4,4,5, 2,3,3,4,3,4,4,5, 3,4,4,5,4,5,5,6,
            1,2,2,3,2,3,3,4, 2,3,3,4,3,4,4,5, 2,3,3,4,3,4,4,5, 3,4,4,5,4,5,5,6,
            2,3,3,4,3,4,4,5, 3,4,4,5,4,5,5,6, 3,4,4,5,4,5,5,6, 4,5,5,6,5,6,6,7,
            1,2,2,3,2,3,3,4, 2,3,3,4,3,4,4,5, 2,3,3,4,3,4,4,5, 3,4,4,5,4,5,5,6,
            2,3,3,4,3,4,4,5, 3,4,4,5,4,5,5,6, 3,4,4,5,4,5,5,6, 4,5,5,6,5,6,6,7,
            2,3,3,4,3,4,4,5, 3,4,4,5,4,5,5,6, 3,4,4,5,4,5,5,6, 4,5,5,6,5,6,6,7,
            3,4,4,5,4,5,5,6, 4,5,5,6,5,6,6,7, 4,5,5,6,5,6,6,7, 5,6,6,7,6,7,7,8,
        };

        public static float CalcHammingSimilarity(ulong hash1, ulong hash2)
        {
            int hammingDistance = 0;
            ulong temp = hash1 ^ hash2;
            for (; temp > 0; temp >>= 8)
                hammingDistance += _bitCounts[temp & 0xff];
            return (64 - hammingDistance) * 1.5625f; // 100/64
        }

        public static string GetMetadataPath()
        {
            return (Settings.UseDefaultMetadataPath)
                ? Settings.DefaultMetadataPath
                : Settings.MetadataPath;
        }

        public static string GetThumbnailPath()
        {
            return (Settings.UseDefaultThumbnailPath)
                ? Settings.DefaultThumbnailPath
                : Settings.ThumbnailPath;
        }

        private string GetRandomId(int numBytes)
        {
            byte[] bytes = new byte[numBytes];
            var rng = new RNGCryptoServiceProvider();
            rng.GetBytes(bytes);
            rng?.Dispose();
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }
        public string CreateImportId() { return $"I{GetRandomId(8)}"; }
        public string CreateTabId() { return $"T{GetRandomId(8)}"; }
        public string CraeteGroupId() { return $"G{GetRandomId(8)}"; }
        public string CreateSectionId() { return $"S{GetRandomId(8)}"; }
    }
}