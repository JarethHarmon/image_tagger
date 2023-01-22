using System;

namespace ImageTagger.Core
{
    public sealed class CopyrightInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Color Color { get; set; }

        public string[] IdNames { get; set; }
        public string[] Names { get; set; }
        public string[] Subjects { get; set; } // a list of subjects associated with this copyright (ie 'Super Mario Bros.' >> 'Mario', 'Peach', 'Luigi', etc)
        // ^ might not keep this

        public CopyrightInfo()
        {
            Id = Global.GetRandomId(8);
            Name = "default";
            Description = string.Empty;
            Color = Color.Grey;

            IdNames = new string[1] { Global.CreateIdName(Name) };
            Names = new string[1] { Name };
            Subjects = Array.Empty<string>();
        }

        public CopyrightInfo(string name)
        {
            if (string.IsNullOrEmpty(name)) name = "unknown";
            Id = Global.GetRandomId(8);
            Name = name;
            Description = string.Empty;
            Color = Color.GetRandomPastelColor();

            IdNames = new string[1] { Global.CreateIdName(name) };
            Names = new string[1] { name };
            Subjects = Array.Empty<string>();
        }
    }
}
