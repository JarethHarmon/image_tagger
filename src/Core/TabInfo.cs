namespace ImageTagger.Core
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

        public TabInfo()
        {
            //Id = Global.CreateTabId(); // need to change all Info objects to work like this
            Name = "import";

            TabType = TabType.DEFAULT;
            ImportId = Global.ALL;
            GroupId = string.Empty;
            Tag = string.Empty;
            SimilarityHash = string.Empty;
        }
    }
}
