using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageTagger.Core
{
    public sealed class ImageInfo
    {
        public string Hash { get; set; }
        public ulong AverageHash { get; set; } // would like to use [BsonField("hAvg")] on this and other long names, but I want core to be standalone in case I switch to sqlite
        public ulong WaveletHash { get; set; }
        public ulong DifferenceHash { get; set; }
        public ulong PerceptualHash { get; set; }

        public int Bucket { get; set; }
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

        public Dictionary<string, HashSet<string>> Paths { get; set; }
        public HashSet<string> Groups { get; set; }
        public bool IsGroupLeader { get; set; }

        public HashSet<string> Creators { get; set; } // person (people) who made or contributed to the making of an image/group
        public HashSet<string> Copyrights { get; set; } // ex: Disney/"Company Name"/Spongebob Squarepants/etc
        public HashSet<string> Subjects { get; set; } // people/places/characters/objects/ideas depicted in the image
        public HashSet<string> Descriptive { get; set; } // descriptive tags

        public int RatingSum { get; set; }
        public float RatingAvg { get; set; }
        public Dictionary<string, int> Ratings { get; set; }

        public ImageInfo()
        {
            ImageType = ImageType.Error;
            IsGroupLeader = false;
            Paths = new Dictionary<string, HashSet<string>>();
            Groups = new HashSet<string>();
            Creators = new HashSet<string>();
            Copyrights = new HashSet<string>();
            Subjects = new HashSet<string>();
            Descriptive = new HashSet<string>();
            Colors = Array.Empty<int>();
            Ratings = new Dictionary<string, int>();
        }

        public void Merge(ImageInfo other)
        {
            foreach (KeyValuePair<string, HashSet<string>> item in other.Paths)
                Paths.Add(item.Key, item.Value);
            Groups.UnionWith(other.Groups);
            Creators.UnionWith(other.Creators);
            Copyrights.UnionWith(other.Copyrights);
            Subjects.UnionWith(other.Subjects);
            Descriptive.UnionWith(other.Descriptive);
        }
    }
}