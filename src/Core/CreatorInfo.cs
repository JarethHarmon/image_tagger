using System;

namespace ImageTagger.Core
{
    public sealed class CreatorInfo
    {
        public Guid Id { get; set; }                // the auto-generated Id for this creator; if I intend for metadata to be shared then this will need to be generated using other metadata
        public string Name { get; set; }            // the name for this creator shown in the program (not necessarily unique, but might be difficult to tell which is which)

        // not sure if array/list/hashset is best for these
        public string[] Names { get; set; }         // the names the creator goes by
        public string[] Links { get; set; }         // links to the artists pages on image sites
        public string[] RelatedLinks { get; set; }  // contact links/email/etc (non-image links)
        public string[] Folders { get; set; }       // the folders on the users disk where this creators works are stored

        public string[] CommonTags { get; set; }    // tags the creator frequently uses; might be worth generating this automatically
        public string[] Sites { get; set; }         // the websites the creator uses for sharing their works
        public int Count { get; set; }              // the total number of (unique) works with this creator listed; updated whenever the creator is queried

        public CreatorInfo()
        {
            Id = Guid.NewGuid();
            Name = string.Empty;

            Names = Array.Empty<string>();
            Links = Array.Empty<string>();
            RelatedLinks = Array.Empty<string>();
            Folders = Array.Empty<string>();

            CommonTags = Array.Empty<string>();
            Sites = Array.Empty<string>();
            Count = 0;
        }

        public CreatorInfo(string name)
        {
            Id = Guid.NewGuid();
            Name = name;

            Names = new string[1] { name };
            Links = Array.Empty<string>();
            RelatedLinks = Array.Empty<string>();
            Folders = Array.Empty<string>();

            CommonTags = Array.Empty<string>();
            Sites = Array.Empty<string>();
            Count = 0;
        }
    }
}
