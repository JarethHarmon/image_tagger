using ImageTagger.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageTagger.Core
{
    public sealed class GroupInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } // not sure if will keep
        public string CoverImage { get; set; } // the SHA256 hash of the leader/cover image for this group

        public string[] Groups { get; set; } // subgroups (useful for things like book/chapter/pages)
        public string[] Members { get; set; } //  images in this group (in their user-defined order)
        public string[] Tags { get; set; } // tags assigned to this group as a whole

        public GroupInfo()
        {
            Id = Global.GetRandomId();
            Name = "default";
            Description = string.Empty;
            CoverImage = string.Empty; // I think just check against string.Empty, and use Global.DefaultIcon if they match

            Groups = Array.Empty<string>();
            Members = Array.Empty<string>();
            Tags = Array.Empty<string>();
        }

        public void SortAlphabetical(Sort sort=Sort.Name, bool desc=false)
        {
            if (sort == Sort.Hash)
            {
                if (desc)
                {
                    Members = Members.OrderByDescending(x => x).ToArray();
                    Groups = Groups.OrderByDescending(x => x).ToArray();
                }
                else
                {
                    Members = Members.OrderBy(x => x).ToArray();
                    Groups = Groups.OrderBy(x => x).ToArray();
                }
                return;
            }

            var members = new HashSet<string>(Members);
            var groups = new HashSet<string>(Groups);
            var imageInfos = ImageInfoAccess.GetImageInfo(members);
            var groupInfos = GroupInfoAccess.GetGroupInfo(groups);

            if (desc)
            {
                if (sort == Sort.Name) Members = imageInfos.OrderByDescending(x => x.Name).Select(x => x.Hash).ToArray();
                else if (sort == Sort.Path) Members = imageInfos.OrderByDescending(x => x.Paths.FirstOrDefault()).Select(x => x.Hash).ToArray();
                Groups = groupInfos.OrderByDescending(x => x.Name).Select(x => x.Id).ToArray();
            }
            else
            {
                if (sort == Sort.Name) Members = imageInfos.OrderBy(x => x.Name).Select(x => x.Hash).ToArray();
                else if (sort == Sort.Path) Members = imageInfos.OrderBy(x => x.Paths.FirstOrDefault()).Select(x => x.Hash).ToArray();
                Groups = groupInfos.OrderBy(x => x.Name).Select(x => x.Id).ToArray();
            }
        }

        public void SortNatural(Sort sort=Sort.Name, bool desc=false)
        {
            if (sort == Sort.Hash)
            {
                if (desc)
                {
                    Members = Members.OrderByNatural(x => x, desc:true).ToArray();
                    Groups = Groups.OrderByNatural(x => x, desc:true).ToArray();
                }
                else
                {
                    Members = Members.OrderByNatural(x => x).ToArray();
                    Groups = Groups.OrderByNatural(x => x).ToArray();
                }
                return;
            }

            var members = new HashSet<string>(Members);
            var groups = new HashSet<string>(Groups);
            var imageInfos = ImageInfoAccess.GetImageInfo(members);
            var groupInfos = GroupInfoAccess.GetGroupInfo(groups);

            if (desc)
            {
                if (sort == Sort.Name) Members = imageInfos.OrderByNatural(x => x.Name, desc:true).Select(x => x.Hash).ToArray();
                else if (sort == Sort.Path) Members = imageInfos.OrderByNatural(x => x.Paths.FirstOrDefault(), desc:true).Select(x => x.Hash).ToArray();
                Groups = groupInfos.OrderByNatural(x => x.Name, desc:true).Select(x => x.Id).ToArray();
            }
            else
            {
                if (sort == Sort.Name) Members = imageInfos.OrderByNatural(x => x.Name).Select(x => x.Hash).ToArray();
                else if (sort == Sort.Path) Members = imageInfos.OrderByNatural(x => x.Paths.FirstOrDefault()).Select(x => x.Hash).ToArray();
                Groups = groupInfos.OrderByNatural(x => x.Name).Select(x => x.Id).ToArray();
            }
        }
    }
}
