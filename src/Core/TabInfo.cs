using ImageTagger.Database;

namespace ImageTagger.Core
{
    public sealed class TabInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public QueryInfo Info { get; set; } // need to find a way to serialize this to database

        public TabInfo()
        {
            Id = Global.GetRandomId(8);
            Name = "import";
            Info = new QueryInfo();
        }
    }
}
