using System;

namespace ImageTagger.Core
{
    public sealed class TagInfo
    {
        // Currently I think it is fine for descriptive tags to be uniquely-named; I can change this to an ID later if I think of a use case for it
        public string Name { get; set; }
        public Color Color { get; set; }
        public string[] Parents { get; set; }

        public TagInfo()
        {
            Name = "default";
            Color = Color.Grey;
            Parents = Array.Empty<string>();
        }

        public TagInfo(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "default";
            Name = name;
            Color = Color.GetRandomPastelColor();
            Parents = Array.Empty<string>();
        }
    }
}
