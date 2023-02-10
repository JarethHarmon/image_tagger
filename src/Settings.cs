﻿using Godot;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.IO;

namespace ImageTagger
{
    public sealed class Settings
    {
        public bool UseDefaultMetadataPath { get; set; }
        public string MetadataPath { get; set; }
        public bool UseDefaultThumbnailPath { get; set; }
        public string ThumbnailPath { get; set; }
        public string LastViewedDirectory { get; set; }
        public string LastImportedDirectory { get; set; }

        public int MaxQueriesToStore { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public Sort CurrentSort { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public Order CurrentOrder { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public SortSimilarity CurrentSortSimilarity { get; set; }
        public bool PreferSpeed { get; set; }
        public float MinSimilarity { get; set; }
        public bool UsePrefilter { get; set; }
        public int BucketVariance { get; set; }

        public bool ScanRecursively { get; set; }
        public int MaxImportThreads { get; set; }

        public int MaxExtraQueriedPages { get; set; }
        public int MaxImagesPerPage { get; set; }
        public int MaxThumbnailThreads { get; set; }
        public int MaxPagesToStore { get; set; }
        public int MaxThumbnailsToStore { get; set; }
        public int ThumbnailWidth { get; set; }

        public int MaxImagesToStore { get; set; }
        public int MaxLargeImagesToStore { get; set; }
        public int MaxAnimatedImagesToStore { get; set; }

        public bool UseDelimiter { get; set; }
        public bool UseColoredText { get; set; }
        public bool UseSeparateFontColor { get; set; }

        public bool UseFullScreen { get; set; }
        public bool UseSmoothPixel { get; set; }
        public bool UseImageFilter { get; set; }
        public bool UseColorGrading { get; set; }
        public bool UseFXAA { get; set; }
        public bool UseEdgeMix { get; set; }

        public int OffsetPopupH { get; set; }
        public int OffsetMainH { get; set; }
        public int OffsetThumbnailsV { get; set; }
        public int OffsetMetadataV { get; set; }
        public bool UseColoredTagBackgrounds { get; set; }
        public bool UseRoundedTagButtons { get; set; }
        public bool ShowThumbnailTooltips { get; set; }

        public Settings DefaultSettings()
        {
            // paths
            UseDefaultMetadataPath = true;
            UseDefaultThumbnailPath = true;
            MetadataPath = string.Empty;
            ThumbnailPath = string.Empty;
            LastViewedDirectory = string.Empty;
            LastImportedDirectory = string.Empty;

            // queries
            MaxQueriesToStore = 10;
            CurrentSort = Sort.Hash;
            CurrentOrder = Order.Ascending;
            CurrentSortSimilarity = SortSimilarity.Averaged;
            PreferSpeed = true; // whether to prefer speed or RAM when counting results
            MinSimilarity = 70f;
            UsePrefilter = false;
            BucketVariance = 2;

            // scanning
            ScanRecursively = false;

            // importing
            MaxImportThreads = 3;

            // thumbnails
            MaxExtraQueriedPages = 50;
            MaxImagesPerPage = 400;
            MaxThumbnailThreads = 3;
            MaxPagesToStore = 500;
            MaxThumbnailsToStore = 8000;
            ThumbnailWidth = 240;

            // images
            MaxImagesToStore = 10;
            MaxLargeImagesToStore = 0;
            MaxAnimatedImagesToStore = 0;

            // tagging
            UseDelimiter = true;
            UseColoredText = true;
            UseSeparateFontColor = true;

            // shaders
            UseFullScreen = false;
            UseSmoothPixel = false;
            UseImageFilter = true;
            UseColorGrading = true;
            UseFXAA = false;
            UseEdgeMix = false;

            // ui
            OffsetPopupH = 100;
            OffsetMainH = -200;
            OffsetThumbnailsV = -360;
            OffsetMetadataV = 150;
            UseColoredTagBackgrounds = true;
            UseRoundedTagButtons = true;
            ShowThumbnailTooltips = false;

            return this;
        }

        public static Settings LoadFromJsonFile()
        {
            Settings settings;
            TextReader reader = null;

            try
            {
                reader = new StreamReader("./settings.txt");
                var json = reader.ReadToEnd();
                settings = JsonConvert.DeserializeObject<Settings>(json);
            }
            catch
            {
                settings = new Settings().DefaultSettings();
            }
            finally
            {
                reader?.Dispose();
            }

            return settings;
        }

        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                using (StreamWriter file = System.IO.File.CreateText("./settings.txt"))
                    file.Write(json);
            }
            catch
            {
                GD.Print("Error saving settings file.");
            }
        }
    }
}
