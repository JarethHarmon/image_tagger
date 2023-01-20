using System;

namespace ImageTagger.Core
{
    public sealed class Category
    {
        // 4 8bit ints would fit in a single int, but it would need(?) to be a uint which would serialize to an int64 in liteDB
        // the strings at least likely do not need to be const values; unsure about the int[]
        private const string DEFAULT = "default";
        private readonly int[] GREY = new int[4] { 127, 127, 127, 255 };

        public string Name { get; set; } // this is also the Id of the category
        public int[] Color { get; set; } // might use an actual Color object and write my own serializer; if not then this will be 0123 = rgba

        public Category()
        {
            Name = DEFAULT;
            Color = GREY;
        }
    }

    // will need to have a dict of categories, and look up the category name in the dict whenever a tag is to be displayed (to see what color it should be)
    public sealed class TagInfo
    {
        private const string DEFAULT_CATEGORY = "default/";
        private const string DEFAULT_NAME = "unknown";

        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; } // should multiple categories be allowed (?) (probably capped at two if so)

        public string[] IdNames { get; set; }
        public string[] Names { get; set; }
        public string[] Parents { get; set; }

        public TagInfo()
        {
            Id = DEFAULT_CATEGORY + DEFAULT_NAME;
            Name = DEFAULT_NAME;
            Category = DEFAULT_CATEGORY;

            IdNames = new string[1] { DEFAULT_NAME };
            Names = new string[1] { DEFAULT_NAME };
            Parents = Array.Empty<string>();
        }

        public TagInfo(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = DEFAULT_NAME;
            Id = DEFAULT_CATEGORY + name;
            Name = name;
            Category = DEFAULT_CATEGORY;

            IdNames = new string[1] { Global.CreateIdName(name) };
            Names = new string[1] { name };
            Parents = Array.Empty<string>();
        }

        public TagInfo(string name, string category)
        {
            if (string.IsNullOrWhiteSpace(name)) name = DEFAULT_NAME;
            if (string.IsNullOrWhiteSpace(category)) category = DEFAULT_CATEGORY;
            Id = category + name;
            Name = name;
            Category = category;

            IdNames = new string[1] { Global.CreateIdName(name) };
            Names = new string[1] { name };
            Parents = Array.Empty<string>();
        }
    }
}
