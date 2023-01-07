using Godot;
using System;
using System.Linq;
using ImageTagger.Database;
using ImageTagger.Metadata;
using ImageTagger.Importer;

namespace ImageTagger.Managers
{
    public sealed class DatabaseManager : Node
    {
        public int Create()
        {
            var result = DatabaseAccess.Create();
            if (result != Error.OK) return (int)result;
            return (int)DatabaseAccess.Setup();
        }

        public void Shutdown()
        {
            DatabaseAccess.Shutdown();
        }

        public bool IncorrectImage(string hash)
        {
            string _hash = ImageInfoAccess.GetCurrentHash();
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
            var perceptualHashes = ImageImporter.GetPerceptualHashes(tabInfo.SimilarityHash);
            var conditions = Querier.ConvertStringToComplexTags(tagsAll, tagsAny, tagsNone, tagsComplex);

            var queryInfo = new QueryInfo
            {
                ImportId = (!tabInfo.ImportId?.Equals(string.Empty) ?? false) ? tabInfo.ImportId : Global.ALL,
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
                AverageHash = perceptualHashes.Average,
                DifferenceHash = perceptualHashes.Difference,
                WaveletHash = perceptualHashes.Wavelet,

                Query = DatabaseAccess.GetImageInfoQuery(),
            };
            queryInfo.CalcId();

            var hashes = Querier.QueryDatabase(queryInfo, countResults);
            return hashes.Append(queryInfo.Id).ToArray();
        }

        /*=========================================================================================
                                                Similarity
        =========================================================================================*/
        public float GetAveragedSimilarityTo(string hash1, string hash2)
        {
            var info1 = ImageInfoAccess.GetImageInfo(hash1);
            var info2 = ImageInfoAccess.GetImageInfo(hash2);
            float simi1 = Global.CalcHammingSimilarity(info1.AverageHash, info2.AverageHash);
            float simi2 = Global.CalcHammingSimilarity(info1.DifferenceHash, info2.DifferenceHash);
            float simi3 = Global.CalcHammingSimilarity(info1.WaveletHash, info2.WaveletHash);
            return (simi1 + simi2 + simi3) / 3;
        }

        public float GetAverageSimilarityTo(string hash1, string hash2)
        {
            var info1 = ImageInfoAccess.GetImageInfo(hash1);
            var info2 = ImageInfoAccess.GetImageInfo(hash2);
            return Global.CalcHammingSimilarity(info1.AverageHash, info2.AverageHash);
        }

        public float GetDifferenceSimilarityTo(string hash1, string hash2)
        {
            var info1 = ImageInfoAccess.GetImageInfo(hash1);
            var info2 = ImageInfoAccess.GetImageInfo(hash2);
            return Global.CalcHammingSimilarity(info1.DifferenceHash, info2.DifferenceHash);
        }

        public float GetWaveletSimilarityTo(string hash1, string hash2)
        {
            var info1 = ImageInfoAccess.GetImageInfo(hash1);
            var info2 = ImageInfoAccess.GetImageInfo(hash2);
            return Global.CalcHammingSimilarity(info1.WaveletHash, info2.WaveletHash);
        }
    }
}
