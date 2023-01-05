using ImageTagger.Database;
using Godot;
using ImageTagger.Metadata;
using System;
using System.Linq;
using ImageTagger.Core;

namespace ImageTagger.Managers
{
    public sealed class DatabaseManager : Node
    {
        private ImageInfo currentImageInfo;

        public string GetCurrentHash()
        {
            return currentImageInfo?.Hash ?? string.Empty;
        }

        public bool IncorrectImage(string hash)
        {
            string _hash = GetCurrentHash();
            if (_hash.Equals(string.Empty)) return true;
            return !hash.Equals(_hash, StringComparison.InvariantCultureIgnoreCase);
        }

        public int GetLastQueriedCount(string id)
        {
            return Querier.GetLastQueriedCount(id);
        }

        public string[] TempConstructQueryInfo(string tabId, int offset, int count, string[] tagsAll, string[] tagsAny, string[] tagsNone, string[] tagsComplex,
            int sort = (int)Sort.HASH, int order = (int)Order.ASCENDING, bool countResults = false, int sortSimilarity = (int)SortSimilarity.AVERAGED)
        {
            var tabInfo = TabInfoAccess.GetTabInfo(tabId);
            if (tabInfo is null) return Array.Empty<string>();
            var perceptualHashes = ImageInfoAccess.GetPerceptualHashes(tabInfo.SimilarityHash);
            var conditions = Querier.ConvertStringToComplexTags(tagsAll, tagsAny, tagsNone, tagsComplex);

            var queryInfo = new QueryInfo
            {
                ImportId = tabInfo.ImportId,
                GroupId = string.Empty,

                TagsAll = conditions.All,
                TagsAny = conditions.Any,
                TagsNone = conditions.None,
                TagsComplex = conditions.Complex,

                Offset = offset,
                Limit = count,

                QueryType = tabInfo.TabType,
                Sort = (Sort)sort,
                Order = (Order)order,
                SortSimilarity = (SortSimilarity)sortSimilarity,

                SimilarityHash = tabInfo.SimilarityHash,
                AverageHash = perceptualHashes.average,
                DifferenceHash = perceptualHashes.difference,
                WaveletHash = perceptualHashes.wavelet,
            };
            queryInfo.CalcId();

            var hashes = Querier.QueryDatabase(queryInfo, countResults);
            hashes.Append(queryInfo.Id);
            return hashes;
        }
    }
}
