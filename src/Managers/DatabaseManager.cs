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
        public int GetLastQueriedCount(string id)
        {
            return Querier.GetLastQueriedCount(id);
        }

        /*public string[] TempConstructQueryInfo(string tabId, int offset, int limit, string[] tagsAll, string[] tagsAny, string[] tagsNone, string[] tagsComplex,
            int sort = (int)Sort.HASH, int order = (int)Order.ASCENDING, bool countResults = false, int sortSimilarity = (int)SortSimilarity.AVERAGED)
        {
            var now = DateTime.Now;
            var tabInfo = TabInfoAccess.GetTabInfo(tabId);
            if (tabInfo is null) return Array.Empty<string>();
            var perceptualHashes = ImageImporter.GetPerceptualHashes(tabInfo.SimilarityHash);
            var conditions = Querier.ConvertStringToComplexTags(tagsAll, tagsAny, tagsNone, tagsComplex);
            string importId = (!tabInfo.ImportId?.Equals(string.Empty) ?? false) ? tabInfo.ImportId : Global.ALL;
            var importInfo = ImportInfoAccess.GetImportInfo(importId);
            int success = (importId.Equals(Global.ALL)) ? importInfo?.Success ?? 0 : (importInfo?.Success + importInfo?.Duplicate) ?? 0;

            var queryInfo = new QueryInfo
            {
                ImportId = importId,
                GroupId = string.Empty,

                TagsAll = conditions.All,
                TagsAny = conditions.Any,
                TagsNone = conditions.None,
                TagsComplex = conditions.Complex,

                QueryType = tabInfo.TabType,
                Sort = (Sort)sort,
                Order = (Order)order,
                SortSimilarity = (SortSimilarity)sortSimilarity,

                MinSimilarity = 74.999f,
                Success = success,

                SimilarityHash = tabInfo.SimilarityHash,
                AverageHash = perceptualHashes.Average,
                DifferenceHash = perceptualHashes.Difference,
                WaveletHash = perceptualHashes.Wavelet,
                PerceptualHash = perceptualHashes.Perceptual,

                Query = DatabaseAccess.GetImageInfoQuery(),
            };
            queryInfo.CalcId();

            var hashes = Querier.QueryDatabase(queryInfo, offset, limit, countResults);
            var results = hashes.Append(queryInfo.Id).ToArray();
            GetNode<Label>("/root/main/margin/vbox/core_buttons/margin/flow/query_time").Text = (DateTime.Now - now).ToString();

            return results;
        }*/

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
            float simi4 = Global.CalcHammingSimilarity(info1.PerceptualHash, info2.PerceptualHash);
            return (simi1 + simi2 + simi3 + simi4) / 4;
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

        public float GetPerceptualSimilarityTo(string hash1, string hash2)
        {
            var info1 = ImageInfoAccess.GetImageInfo(hash1);
            var info2 = ImageInfoAccess.GetImageInfo(hash2);
            return Global.CalcHammingSimilarity(info1.PerceptualHash, info2.PerceptualHash);
        }
    }
}
