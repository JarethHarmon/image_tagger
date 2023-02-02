using System;
using ImageTagger.Extension;

namespace ImageTagger.Core
{
    // person/place/character/object/idea
    public sealed class SubjectInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Color Color { get; set; }

        public string[] Parents { get; set; } // for example; animal:cat,dog,bear,etc
        public string[] Names { get; set; }
        public string[] IdNames { get; set; }
        public string[] Copyrights { get; set; }  // the copyright(s) associated with this subject (if applicable) (original/photo could be default options)

        public SubjectInfo()
        {
            Id = Global.GetRandomId(8);
            Name = "default";
            Description = string.Empty;
            Color = Color.Grey;

            Names = new string[1] { Name };
            IdNames = new string[1] { Global.CreateIdName(Name) };
            Copyrights = Array.Empty<string>();
        }

        public SubjectInfo(string name)
        {
            if (string.IsNullOrEmpty(name)) name = "default";
            Id = Global.GetRandomId(8);
            Name = name;
            Description = string.Empty;
            Color = Color.GetRandomPastelColor();

            Names = new string[1] { name };
            IdNames = new string[1] { Global.CreateIdName(name) };
            Copyrights = Array.Empty<string>();
        }
    }
}
