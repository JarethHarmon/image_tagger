﻿namespace ImageTagger.Core
{
    public sealed class TabInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public TabType TabType { get; set; }
        public string ImportId { get; set; }
        public string GroupId { get; set; }
        public string Tag { get; set; }
        public string SimilarityHash { get; set; }
    }
}