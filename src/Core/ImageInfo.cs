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
        public ulong PerceptualHash { get; set; }

        public int Bucket { get; set; }
        public int NumFrames { get; set; }
        public ulong[] Colors { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }
        public long Size { get; set; }
        public ImageType ImageType { get; set; }

        public long CreationTime { get; set; }
        public long LastWriteTime { get; set; }
        public long LastEditTime { get; set; }
        public long UploadTime { get; set; }

        public HashSet<string> Imports { get; set; }
        public HashSet<string> Paths { get; set; }
        public HashSet<string> Groups { get; set; }
        public bool IsGroupLeader { get; set; }

        public HashSet<string> Creators { get; set; } // person (people) who made or contributed to the making of an image/group
        public HashSet<string> Copyrights { get; set; } // 
        public HashSet<string> Subjects { get; set; } // person/place/character/object/idea depicted in the image
        public string[] Tags { get; set; } // needs to be an array; OrderBy(tags.Count) does not work correctly (list/hashset) if the count is 1 (treated as if it was 0)

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
            Tags = Array.Empty<string>();
            Colors = Array.Empty<ulong>();
            Ratings = new Dictionary<string, int>();
        }

        public void Merge(ImageInfo other)
        {
            Paths.UnionWith(other.Paths);
            Imports.UnionWith(other.Imports);
            Groups.UnionWith(other.Groups);
            Creators.UnionWith(other.Creators);
            Copyrights.UnionWith(other.Copyrights);
            Subjects.UnionWith(other.Subjects);
            Tags = Tags.Union(other.Tags).ToArray();
        }
    }
}