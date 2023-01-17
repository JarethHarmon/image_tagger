using System;

namespace ImageTagger.Core
{
    // person/place/character/object/idea
    public sealed class SubjectInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid[] Copyrights { get; set; }  // the copyright(s) associated with this subject (if applicable) (original/photo should be default options)

        public SubjectInfo()
        {
            Id = Guid.NewGuid();
            Name = string.Empty;
            Copyrights = Array.Empty<Guid>();
        }

        public SubjectInfo(string name)
        {
            Id = Guid.NewGuid();
            Name = name;
            Copyrights = Array.Empty<Guid>();
        }
    }
}
