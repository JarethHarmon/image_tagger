using System;

namespace ImageTagger.Core
{
    // person/place/character/object/idea
    public sealed class SubjectInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string[] Copyrights { get; set; }  // the copyright(s) associated with this subject (if applicable) (original/photo should be default options)

        public SubjectInfo()
        {
            Id = Global.GetRandomId(8);
            Name = string.Empty;
            Copyrights = Array.Empty<string>();
        }

        public SubjectInfo(string name)
        {
            Id = Global.GetRandomId(8);
            Name = name;
            Copyrights = Array.Empty<string>();
        }
    }
}
