namespace ImageTagger.Core
{
    public sealed class Settings
    {
        public bool UseDefaultMetadataPath { get; set; }
        public string DefaultMetadataPath { get; set; }
        public string MetadataPath { get; set; }

        public bool UseDefaultThumbnailPath { get; set; }
        public string DefaultThumbnailPath { get; set; }
        public string ThumbnailPath { get; set; }

        public int MaxQueriesToStore { get; set; }

    }
}
