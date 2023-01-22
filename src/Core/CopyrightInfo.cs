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
        public string[] Parents { get; set; } // for example; Nintendo:Super Mario Bros., Legend of Zelda, etc
        public string[] Names { get; set; }
        public string[] Subjects { get; set; } // a list of subjects associated with this copyright (ie 'Super Mario Bros.' >> 'Mario', 'Peach', 'Luigi', etc)
        // ^ might not keep this; only reason to keep this is to avoid querying all Subjects to see which copyrights they are assigned to

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
