using System;

namespace ImageTagger.Core
{
    public sealed class CopyrightInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public string[] IdNames { get; set; }
        public string[] Names { get; set; }
        public string[] Tags { get; set; } // tags that should be applied when this copyright is assigned to an image

        public CopyrightInfo()
        {
            Id = Global.GetRandomId(8);
            Name = string.Empty;

            IdNames = Array.Empty<string>();
            Names = Array.Empty<string>();
            Tags = Array.Empty<string>();
        }

        public CopyrightInfo(string name)
        {
            Id = Global.GetRandomId(8);
            Name = name;

            IdNames = new string[1] { Global.CreateIdName(name) };
            Names = new string[1] { name };
            Tags = Array.Empty<string>();
        }
    }
}
