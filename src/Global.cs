using Godot;
using ImageTagger.Database;
using ImageTagger.Importer;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using static ImageTagger.Importer.ImageImporter;

namespace ImageTagger
{
    public enum Error {  OK, Generic, Database, Dictionary, IO, Python}
    public enum ImportStatus {  Success, Duplicate, Ignored, Failed }

    public enum ImageType { Jpeg, Png, Apng, Gif, Webp, Other=15, Error=-1 }
    public enum Order { Ascending, Descending }
    public enum Sort {
        Hash, Path, Name, Size, UploadTime, CreationTime, LastWriteTime, LastEditTime,
        Dimensions, Width, Height, TagCount, NumFrames, Quality, Appeal, ArtStyle, Color, Random
    }
    public enum Colors { Red, Green, Blue, Yellow, Cyan, Fuchsia, Light, Dark, Alpha };
    public enum SortSimilarity { Averaged=0, Average, Difference, Wavelet, Perceptual }

    public enum TabType { Default, Similarity }
    public enum ExpressionType { All, Any, None }

    public sealed class Global : Node
    {
        public static Godot.ImageTexture DefaultIcon = new Godot.ImageTexture();
        public const string DefaultIconHash = "2c160bfdb8d0423b958083202dc7b58d499cbef22f28d2a58626884378ce9b7f";

        public const int MAX_PATH_LENGTH = 256, THUMBNAIL_SIZE = 256, PROGRESS_SECTION_SIZE = 16;
        public const string ALL = "All";
        internal static string currentTabId = ALL;

        public static void SetCurrentTabId(string id) { currentTabId = id; }

        // I hope to find a better solution eventually, Godot is convinced that Settings is null, even if I make it not static
        // might be possible to fix this by moving all godot functions that access settings at program start into call_deferred
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

        public int Setup()
        {
            CreateDefaultIcon();
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

            result = ImageImporter.StartPython();
            return (int)result;
        }

        public void Shutdown()
        {
            Settings.Save();
            DatabaseAccess.Shutdown();
            ImageImporter.Shutdown();
        }

        public static void OpenSettingsFile()
        {
            string path = (OS.IsDebugBuild())
                ? ProjectSettings.GlobalizePath("res://")
                : OS.GetExecutablePath();
            OS.ShellOpen(path.PlusFile("settings.txt"));
        }

        internal static int CountBits(ulong hash)
        {
            ulong temp = hash;
            int count = 0;
            for (; temp > 0; temp >>= 8)
                count += _bitCounts[temp & 0xff];
            return count;
        }

        internal static int CountAllBits(PerceptualHashes phashes)
        {
            int count = CountBits(phashes.Average);
            count += CountBits(phashes.Difference);
            count += CountBits(phashes.Perceptual);
            return count + CountBits(phashes.Wavelet);
        }

        internal static float CalcHammingSimilarity(ulong hash1, ulong hash2)
        {
            ulong xor = hash1 ^ hash2;
            int hammingDistance = CountBits(xor);
            return (64 - hammingDistance) * 1.5625f; // 100/64
        }

        internal static string CreateIdName(string name)
        {
            var separators = new List<char>();
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c))
                    separators.Add(c);
            var sections = name.Split(separators.ToArray(), StringSplitOptions.RemoveEmptyEntries);
            Array.Sort(sections);
            return string.Join("_", sections);
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

        // need to look into moving setup stuff into the constructor (unsure how that will interact with Godot)

        public static void CreateDefaultIcon()
        {
            const string base64 = "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";
            byte[] data = Convert.FromBase64String(base64);
            var image = new Godot.Image();
            image.LoadPngFromBuffer(data);
            DefaultIcon.CreateFromImage(image, 0);
        }

        public static string GetRandomId(int numBytes)
        {
            byte[] bytes = new byte[numBytes];
            var rng = new RNGCryptoServiceProvider();
            rng.GetBytes(bytes);
            rng?.Dispose();
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }
    }
}
