using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageTagger.Core
{
    public sealed class ImageInfo
    {
        private static readonly ushort[] defaultBuckets = new ushort[4];

        public string Hash { get; set; }
        public string Name { get; set; }

        public ulong AverageHash { get; set; } // would like to use [BsonField("hAvg")] on this and other long names, but I want core to be standalone in case I switch to sqlite
        public ulong WaveletHash { get; set; }
        public ulong DifferenceHash { get; set; }
        public ulong PerceptualHash { get; set; }
        public ulong ColorHash { get; set; }

        public ushort[] Buckets { get; set; }
        public int NumFrames { get; set; }
        public int[] Colors { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }
        public long Size { get; set; }
        public ImageType ImageType { get; set; }

        public long CreationTime { get; set; }
        public long LastWriteTime { get; set; }
        public long LastEditTime { get; set; }
        public long UploadTime { get; set; }

        //public HashSet<string> Folders { get; set; } // string[] Folders, Files // Dictionary<string, string[]> Paths (Folder:[FileNames]) // string[] Folder, Files int[] Indices
        public HashSet<string> Imports { get; set; }
        public HashSet<string> Paths { get; set; }
        public HashSet<string> Groups { get; set; }
        public bool IsGroupLeader { get; set; }

        public HashSet<string> Creators { get; set; } // person (people) who made or contributed to the making of an image/group
        public HashSet<string> Copyrights { get; set; } // ex: Disney/"Company Name"/Spongebob Squarepants/etc
        public HashSet<string> Subjects { get; set; } // people/places/characters/objects/ideas depicted in the image
        public HashSet<string> Tags { get; set; } // descriptive tags

        public int RatingSum { get; set; }
        public float RatingAvg { get; set; }
        public Dictionary<string, int> Ratings { get; set; }

        public ImageInfo()
        {
            ImageType = ImageType.Error;
            IsGroupLeader = false;
            Imports = new HashSet<string>();
            Paths = new HashSet<string>();
            Groups = new HashSet<string>();
            Creators = new HashSet<string>();
            Copyrights = new HashSet<string>();
            Subjects = new HashSet<string>();
            Tags = new HashSet<string>();
            Colors = Array.Empty<int>();
            Ratings = new Dictionary<string, int>();
            Buckets = defaultBuckets;
        }

        public void Merge(ImageInfo other)
        {
            Paths.UnionWith(other.Paths);
            Imports.UnionWith(other.Imports);
            Groups.UnionWith(other.Groups);
            Creators.UnionWith(other.Creators);
            Copyrights.UnionWith(other.Copyrights);
            Subjects.UnionWith(other.Subjects);
            Tags.UnionWith(other.Tags);
        }
    }
}