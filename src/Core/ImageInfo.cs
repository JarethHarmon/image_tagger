using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageTagger.Core
{
    public sealed class ImageInfo
    {
        public string Hash { get; set; }
        public string Name { get; set; }

        public ulong AverageHash { get; set; } // would like to use [BsonField("hAvg")] on this and other long names, but I want core to be standalone in case I switch to sqlite
        public ulong WaveletHash { get; set; }
        public ulong DifferenceHash { get; set; }

        public int Bucket { get; set; }
        public int Red { get; set; }
        public int Green { get; set; }
        public int Blue { get; set; }
        public int Alpha { get; set; }
        public int Light { get; set; }
        public int Dark { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }
        public long Size { get; set; }
        public ImageType ImageType { get; set; }

        public long CreationTime { get; set; }
        public long LastWriteTime { get; set; }
        public long LastEditTime { get; set; }
        public long UploadTime { get; set; }

        public bool IsGroupLeader { get; set; }
        public HashSet<string> Imports { get; set; }
        public HashSet<string> Paths { get; set; }
        public HashSet<string> Groups { get; set; }
        public string[] Tags { get; set; }

        public int RatingSum { get; set; }
        public float RatingAverage { get; set; }
        public Dictionary<string, int> Ratings { get; set; }

        public ImageInfo()
        {
            ImageType = ImageType.ERROR;
            IsGroupLeader = false;
            Imports = new HashSet<string>();
            Paths = new HashSet<string>();
            Groups = new HashSet<string>();
            Tags = Array.Empty<string>();
            Ratings = new Dictionary<string, int>();
        }

        public void Merge(ImageInfo other)
        {
            this.Paths.UnionWith(other.Paths);
            this.Imports.UnionWith(other.Imports);
            this.Groups.UnionWith(other.Groups);
            this.Tags = (string[])this.Tags.Union(other.Tags);
        }
    }
}