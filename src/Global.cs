using Godot;
using ImageTagger.Database;
using ImageTagger.Importer;
using System;
using System.Security.Cryptography;

namespace ImageTagger
{
    public enum Error {  OK, GENERIC, DATABASE, DICTIONARY, IO, PYTHON }
    public enum ImportStatus {  SUCCESS, DUPLICATE, IGNORED, FAILED }

    public enum ImageType { JPEG, PNG, APNG, GIF, WEBP, OTHER=15, ERROR=-1 }
    public enum Order { ASCENDING, DESCENDING }
    public enum Sort
    {
        HASH, PATH, NAME, SIZE, UPLOAD, CREATION, LAST_WRITE, LAST_EDIT, DIMENSIONS, WIDTH, HEIGHT, TAG_COUNT, QUALITY, APPEAL, ART_STYLE,
        RED, LIGHT_RED, DARK_RED, GREEN, LIGHT_GREEN, DARK_GREEN, BLUE, LIGHT_BLUE, DARK_BLUE, YELLOW, LIGHT_YELLOW, DARK_YELLOW, CYAN, LIGHT_CYAN,
        DARK_CYAN, FUCHSIA, LIGHT_FUCHSIA, DARK_FUCHSIA, LIGHT, DARK, ALPHA, RANDOM, DARK_LIGHT, ORANGE, PURPLE, LIME, AQUAMARINE, TEAL, HOT_PINK
    }
    public enum SortSimilarity { AVERAGED=0, AVERAGE, DIFFERENCE, WAVELET, PERCEPTUAL }

    public enum TabType { DEFAULT, SIMILARITY }
    public enum ExpressionType { ALL, ANY, NONE }

    public sealed class Global : Node
    {
        public const string ALL = "All";
        public const int MAX_PATH_LENGTH = 256, THUMBNAIL_SIZE = 256, PROGRESS_SECTION_SIZE = 16;

        internal static string currentTabId = ALL;
        public static void SetCurrentTabId(string id) { currentTabId = id; }

        // I hope to find a better solution eventually, Godot is convinced that Settings is null, even if I make it not static
        public static Settings Settings;

        // manual get/set of Setting properties, because see above
        public static void SetMaxQueriesToStore(int num) { Settings.MaxQueriesToStore = num; }
        public static int GetMaxQueriesToStore() { return Settings.MaxQueriesToStore; }
        public static void SetCurrentSort(int sort) { Settings.CurrentSort = (Sort)sort; }
        public static int GetCurrentSort() { return (int)Settings.CurrentSort; }
        public static void SetCurrentOrder(int order) { Settings.CurrentOrder = (Order)order; }
        public static int GetCurrentOrder() { return (int)Settings.CurrentOrder; }
        public static void SetCurrentSortSimilarity(int simi) { Settings.CurrentSortSimilarity = (SortSimilarity)simi; }
        public static int GetCurrentSortSimilarity() { return (int)Settings.CurrentSortSimilarity; }

        public static void SetScanRecursively(bool value) { Settings.ScanRecursively = value; }
        public static bool GetScanRecursively() { return Settings.ScanRecursively; }

        public static void SetMaxImportThreads(int num) { Settings.MaxImportThreads = num; }
        public static int GetMaxImportThreads() { return Settings.MaxImportThreads; }

        public static void SetMaxImagesPerPage(int num) { Settings.MaxImagesPerPage = num; }
        public static int GetMaxImagesPerPage() { return Settings.MaxImagesPerPage; }
        public static void SetMaxThumbnailThreads(int num) { Settings.MaxThumbnailThreads = num; }
        public static int GetMaxThumbnailThreads() { return Settings.MaxThumbnailThreads; }
        public static void SetMaxPagesToStore(int num) { Settings.MaxPagesToStore = num; }
        public static int GetMaxPagesToStore() { return Settings.MaxPagesToStore; }
        public static void SetMaxThumbnailsToStore(int num) { Settings.MaxThumbnailsToStore = num; }
        public static int GetMaxThumbnailsToStore() { return Settings.MaxThumbnailsToStore; }
        public static void SetThumbnailWidth(int num) { Settings.ThumbnailWidth = num; }
        public static int GetThumbnailWidth() { return Settings.ThumbnailWidth; }

        public static void SetMaxImagesToStore(int num) { Settings.MaxImagesToStore = num; }
        public static int GetMaxImagesToStore() { return Settings.MaxImagesToStore; }
        public static void SetMaxLargeImagesToStore(int num) { Settings.MaxLargeImagesToStore = num; }
        public static int GetMaxLargeImagesToStore() { return Settings.MaxLargeImagesToStore; }
        public static void SetMaxAnimatedImagesToStore(int num) { Settings.MaxAnimatedImagesToStore = num; }
        public static int GetMaxAnimatedImagesToStore() { return Settings.MaxAnimatedImagesToStore; }

        public static void SetUseFullscreen(bool value) { Settings.UseFullScreen = value; }
        public static bool GetUseFullscreen() { return Settings.UseFullScreen; }
        public static void SetUseSmoothPixel(bool value) { Settings.UseSmoothPixel = value; }
        public static bool GetUseSmoothPixel() { return Settings.UseSmoothPixel; }
        public static void SetUseImageFilter(bool value) { Settings.UseImageFilter = value; }
        public static bool GetUseImageFilter() { return Settings.UseImageFilter; }
        public static void SetUseColorGrading(bool value) { Settings.UseColorGrading = value; }
        public static bool GetUseColorGrading() { return Settings.UseColorGrading; }
        public static void SetUseFXAA(bool value) { Settings.UseFXAA = value; }
        public static bool GetUseFXAA() { return Settings.UseFXAA; }
        public static void SetUseEdgeMix(bool value) { Settings.UseEdgeMix = value; }
        public static bool GetUseEdgeMix() { return Settings.UseEdgeMix; }

        public static void SetOffsetPopupH(int num) { Settings.OffsetPopupH = num; }
        public static int GetOffsetPopupH() { return Settings.OffsetPopupH; }
        public static void SetOffsetMainH(int num) { Settings.OffsetMainH = num; }
        public static int GetOffsetMainH() { return Settings.OffsetMainH; }
        public static void SetOffsetThumbnailsV(int num) { Settings.OffsetThumbnailsV = num; }
        public static int GetOffsetThumbnailsV() { return Settings.OffsetThumbnailsV; }
        public static void SetOffsetMetadataH(int num) { Settings.OffsetMetadataH = num; }
        public static int GetOffsetMetadataH() { return Settings.OffsetMetadataH; }
        public static void SetOffsetMetadataV(int offset) { Settings.OffsetMetadataV = offset; }
        public static int GetOffsetMetadataV() { return Settings.OffsetMetadataV; }

        public static void SetUseColoredTagBackgrounds(bool value) { Settings.UseColoredTagBackgrounds = value; }
        public static bool GetUseColoredTagBackgrounds() { return Settings.UseColoredTagBackgrounds; }
        public static void SetUseRoundedTagButtons(bool value) { Settings.UseRoundedTagButtons = value; }
        public static bool GetUseRoundedTagButtons() { return Settings.UseRoundedTagButtons; }
        public static void SetShowThumbnailTooltips(bool value) { Settings.ShowThumbnailTooltips = value; }
        public static bool GetShowThumbnailTooltips() { return Settings.ShowThumbnailTooltips; }

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

        public int Setup(string executablePath)
        {
            Settings = Settings.LoadFromJsonFile();
            try
            {
                System.IO.Directory.CreateDirectory(GetMetadataPath());
                System.IO.Directory.CreateDirectory(GetThumbnailPath());
            }
            catch
            {
                return (int)Error.IO;
            }

            var result = DatabaseAccess.Create();
            if (result != Error.OK) return (int)result;

            result = DatabaseAccess.Setup();
            if (result != Error.OK) return (int)result;

            result = ImageImporter.StartPython(executablePath);
            return (int)result;
        }

        public void Shutdown()
        {
            Settings.Save();
            DatabaseAccess.Shutdown();
            ImageImporter.Shutdown();
        }

        internal static int CountBits(ulong hash)
        {
            ulong temp = hash;
            int count = 0;
            for (; temp > 0; temp >>= 8)
                count += _bitCounts[temp & 0xff];
            return count;
        }

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
                ? ProjectSettings.GlobalizePath("user://metadata/")
                : Settings.MetadataPath;
        }

        public static string GetThumbnailPath()
        {
            return (Settings.UseDefaultThumbnailPath)
                ? ProjectSettings.GlobalizePath("user://thumbnails/")
                : Settings.ThumbnailPath;
        }

        private static string GetRandomId(int numBytes)
        {
            byte[] bytes = new byte[numBytes];
            var rng = new RNGCryptoServiceProvider();
            rng.GetBytes(bytes);
            rng?.Dispose();
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }
        public static string CreateImportId() { return $"I{GetRandomId(8)}"; }
        public static string CreateTabId() { return $"T{GetRandomId(8)}"; }
        public static string CreateGroupId() { return $"G{GetRandomId(8)}"; }
        public static string CreateSectionId() { return $"S{GetRandomId(8)}"; }
    }
}