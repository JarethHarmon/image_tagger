using Godot;
using Newtonsoft.Json;
using System.IO;

namespace ImageTagger
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

        public Settings()
        {
            DefaultMetadataPath = ProjectSettings.GlobalizePath("user://metadata");
            DefaultThumbnailPath = ProjectSettings.GlobalizePath("user://thumbnails");
            MetadataPath = string.Empty;
            ThumbnailPath = string.Empty;

            MaxQueriesToStore = 10;
        }

        public static Settings LoadFromJsonFile()
        {
            Settings settings;
            TextReader reader = null;

            try
            {
                string path = ProjectSettings.GlobalizePath("user://settings.txt");
                reader = new StreamReader(path);
                var json = reader.ReadToEnd();
                settings = JsonConvert.DeserializeObject<Settings>(json);
            }
            catch
            {
                settings = new Settings();
            }
            finally
            {
                reader?.Dispose();
            }

            return settings;
        }
    }
}
