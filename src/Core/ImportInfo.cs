using System;
using System.Collections.Generic;

namespace ImageTagger.Core
{
    public sealed class ImportInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public int Total { get; set; }
        public int Processed { get; set; }
        public int Success { get; set; }
        public int Duplicate { get; set; }
        public int Ignored { get; set; }
        public int Failed { get; set; }

        public bool Finished { get; set; }
        public long StartTime { get; set; }
        public long FinishTime { get; set; }

        public HashSet<string> Sections { get; set; }

        public ImportInfo()
        {
            Sections = new HashSet<string>();
        }
    }

    public sealed class ImportSection
    {
        public string Id { get; set; }
        public string[] Paths { get; set; }

        public ImportSection()
        {
            Paths = Array.Empty<string>();
        }
    }
}