namespace ImageTagger.ScanImport
{
    public sealed class Section
    {
        internal const int SIZE = 128;
        internal static readonly string[] Base = new string[SIZE];

        internal ulong Id { get; set; }
        internal string ImportId { get; set; }
        internal string Folder { get; set; }
        internal string[] Files { get; set; }

        public Section()
        {
            Id = Global.GetRandomInt64Id();
            ImportId = string.Empty;
            Folder = string.Empty;
            Files = Base;
        }
    }
}
