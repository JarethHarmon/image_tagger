using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using ImageTagger.Metadata;

namespace ImageTagger.Database
{
    internal sealed class TagConditions
    {
        public string[] All;
        public string[] Any;
        public string[] None;
        public List<Dictionary<ExpressionType, HashSet<string>>> Complex;
    }

    internal sealed class Querier
    {
        private static Dictionary<string, QueryInfo> queryHistory = new Dictionary<string, QueryInfo>();
        private static Queue<string> queryHistoryQueue = new Queue<string>();

        private static BsonExpression CreateCondition(string[] tags, ExpressionType type)
        {
            // NONE
            if (type == ExpressionType.NONE)
                return (BsonExpression)$"($.Tags[*] ANY IN {BsonMapper.Global.Serialize(tags)}) != true";

            // one ALL or ANY
            if (tags.Length == 1)
                return Query.Contains("$.Tags[*] ANY", tags[0]);

            // multiple ALL or ANY
            var list = new List<BsonExpression>();
            foreach (string tag in tags)
                list.Add(Query.Contains("$.Tags[*] ANY", tag));

            // return And (ALL) or Or (ANY)
            if (type == ExpressionType.ALL)
                return Query.And(list.ToArray());
            return Query.Or(list.ToArray());
        }

        // numerical condition functions

        private static bool ManageQuery(ref QueryInfo info)
        {
            // return if query already in history
            if (queryHistory.TryGetValue(info.Id, out var _info))
            {
                info = _info; // retrieve info with results objects from history
                return false;
            }
            // if queryHistory already full, remove the oldest entry
            if (queryHistoryQueue.Count == Global.Settings.MaxQueriesToStore)
                queryHistory.Remove(queryHistoryQueue.Dequeue());
            queryHistoryQueue.Enqueue(info.Id);
            // return true if query was newly added (and therefore needs to be filtered)
            return true;
        }

        private static void AddNumericalFilters(QueryInfo info)
        {
            // need to implement a UI for changing these, in the meantime pointless
            var query = info.Query;

            // not sure if these are best to have as first filter or not
            if (!info.ImportId.Equals(Global.ALL)) query = query.Where(x => x.Imports.Contains(info.ImportId));
            if (!info.GroupId.Equals(string.Empty)) query = query.Where(x => x.Groups.Contains(info.GroupId));

            // apply filters

            info.Query = query;
        }

        internal static TagConditions ConvertStringToComplexTags(string[] all, string[] any, string[] none, string[] complex)
        {
            HashSet<string> All = new HashSet<string>(all), Any = new HashSet<string>(any), None = new HashSet<string>(none),
                _All = new HashSet<string>(all), _None = new HashSet<string>(none);
            var conditions = new List<Dictionary<ExpressionType, HashSet<string>>>();

            // convert global all/any/none into a condition
            if (all.Length > 0 || any.Length > 0 || none.Length > 0)
            {
                var condition = new Dictionary<ExpressionType, HashSet<string>>
                {
                    { ExpressionType.ALL, new HashSet<string>(all) },
                    { ExpressionType.ANY, new HashSet<string>(any) },
                    { ExpressionType.NONE, new HashSet<string>(none) }
                };
                conditions.Add(condition);
            }

            // iterate over each string representation condition in complexTags
            foreach (string condition in complex)
            {
                string[] sections = condition.Split(new string[1] { "%" }, StringSplitOptions.None);
                if (sections.Length < 3) continue;

                string[] _all = sections[0].Split(new string[1] { "," }, StringSplitOptions.RemoveEmptyEntries);
                string[] _any = sections[1].Split(new string[1] { "," }, StringSplitOptions.RemoveEmptyEntries);
                string[] _none = sections[2].Split(new string[1] { "," }, StringSplitOptions.RemoveEmptyEntries);

                var dict = new Dictionary<ExpressionType, HashSet<string>>
                {
                    { ExpressionType.ALL, new HashSet<string>(_all) },
                    { ExpressionType.ANY, new HashSet<string>(_any) },
                    { ExpressionType.NONE, new HashSet<string>(_none) },
                };
                conditions.Add(dict);

                // any not needed since there is no need to iterate a Unioned any to check if it is present in every condition
                _All.UnionWith(_all);
                _None.UnionWith(_none);
            }

            // find tags that should be added to global ALL/ANY
            foreach (string tag in _All)
            {
                bool presentInEveryCondition = true;
                foreach (var condition in conditions)
                {
                    if (!condition[ExpressionType.ALL].Contains(tag))
                    {
                        presentInEveryCondition = false;
                        Any.Add(tag);
                        break; // skip to next tag
                    }
                }
                if (presentInEveryCondition) All.Add(tag);
            }

            // find tags that should be added to global ANY
            foreach (var condition in conditions)
            {
                Any.UnionWith(condition[ExpressionType.ANY]);
            }

            // find tags that should be added to global NONE
            foreach (string tag in _None)
            {
                bool presentInEveryCondition = true;
                foreach (var condition in conditions)
                {
                    if (!condition[ExpressionType.NONE].Contains(tag))
                    {
                        presentInEveryCondition = false;
                        break;
                    }
                }
                if (presentInEveryCondition) None.Add(tag);
            }

            // consider usage of GC.Collect();
            _All = null;
            _None = null;

            all = All.ToArray();
            any = Any.ToArray();
            none = None.ToArray();

            All = null;
            Any = null;
            None = null;

            return new TagConditions
            {
                All = all,
                Any = any,
                None = none,
                Complex = conditions
            };
        }

        private static void AddTagFilters(QueryInfo info)
        {
            var query = info.Query;
            if (query is null) return;

            // this block is almost pointless now; basic idea is to just look up the actual count if there are no filters applied, but
            // that requires checking that info in numerical filters as well (and keeping track of it);; no issues for now because the 
            // filtering UI is only implemented for tags, but I will need to fix this in the future
            if (info.TagsAll?.Length == 0 && info.TagsAny?.Length == 0 && info.TagsNone?.Length == 0 && info.TagsComplex?.Count == 0)
            {
                var importInfo = ImportInfoAccess.GetImportInfo(info.ImportId);
                if (importInfo != null)
                {
                    info.LastQueriedCount = (info.ImportId.Equals(Global.ALL, System.StringComparison.InvariantCultureIgnoreCase))
                        ? importInfo.Success
                        : importInfo.Success + importInfo.Duplicate;
                }
            }

            if (info.TagsAll?.Length > 0) foreach (string tag in info.TagsAll) query = query.Where(x => x.Tags.Contains(tag));
            if (info.TagsAny?.Length > 0) query = query.Where("$.Tags ANY IN @0", BsonMapper.Global.Serialize(info.TagsAny));
            if (info.TagsNone?.Length > 0) query = query.Where("($.Tags[*] ANY IN @0) != true", BsonMapper.Global.Serialize(info.TagsNone));
            if (info.TagsComplex?.Count > 0)
            {
                var conditions = new List<BsonExpression>();
                foreach (var condition in info.TagsComplex)
                {
                    if (condition[ExpressionType.ALL].Count == 0 && condition[ExpressionType.ANY].Count == 0 && condition[ExpressionType.NONE].Count == 0) continue;

                    var list = new List<BsonExpression>();
                    if (condition[ExpressionType.ALL].Count > 0)
                        list.Add(CreateCondition(condition[ExpressionType.ALL].ToArray(), ExpressionType.ALL));
                    if (condition[ExpressionType.ANY].Count > 0)
                        list.Add(CreateCondition(condition[ExpressionType.ANY].ToArray(), ExpressionType.ANY));
                    if (condition[ExpressionType.NONE].Count > 0)
                        list.Add(CreateCondition(condition[ExpressionType.NONE].ToArray(), ExpressionType.NONE));

                    if (list.Count == 0) continue;
                    else if (list.Count == 1) conditions.Add(list[0]);
                    else conditions.Add(Query.And(list.ToArray()));
                }
                if (conditions.Count == 1)
                    query = query.Where(conditions[0]);
                else if (conditions.Count > 1)
                    query = query.Where(Query.Or(conditions.ToArray()));
            }

            info.Query = query;
        }

        private static IEnumerable<string> SimilarityQuery(QueryInfo info)
        {
            if (info?.Query is null) return Array.Empty<string>();
            var query = info.Query;
            var results = Enumerable.Empty<SimilarityQueryResult>();

            if (info.SortSimilarity == SortSimilarity.AVERAGED)
            {
                results = query.Select(x => new SimilarityQueryResult
                {
                    Hash = x.Hash,
                    Wavelet = x.WaveletHash,
                    Average = x.AverageHash,
                    Difference = x.DifferenceHash,
                }).ToArray();
                foreach (var result in results) // results is empty on new que
                {
                    float simi1 = Global.CalcHammingSimilarity(info.WaveletHash, result.Wavelet);
                    float simi2 = Global.CalcHammingSimilarity(info.AverageHash, result.Average);
                    float simi3 = Global.CalcHammingSimilarity(info.DifferenceHash, result.Difference);
                    result.Similarity = (simi1 + simi2 + simi3) / 3;
                }
            }
            else if (info.SortSimilarity == SortSimilarity.AVERAGE)
            {
                results = query.Select(x => new SimilarityQueryResult
                {
                    Hash = x.Hash,
                    Average = x.AverageHash,
                }).ToArray();
                foreach (var result in results)
                {
                    float temp = Global.CalcHammingSimilarity(info.AverageHash, result.Average) / 100;
                    result.Similarity = (float)Math.Pow(temp, 3) * 100;
                }
            }
            else if (info.SortSimilarity == SortSimilarity.WAVELET)
            {
                results = query.Select(x => new SimilarityQueryResult
                {
                    Hash = x.Hash,
                    Wavelet = x.WaveletHash
                }).ToArray();
                foreach (var result in results)
                {
                    result.Similarity = Global.CalcHammingSimilarity(info.WaveletHash, result.Wavelet);
                }
            }
            else if (info.SortSimilarity == SortSimilarity.DIFFERENCE)
            {
                results = query.Select(x => new SimilarityQueryResult
                {
                    Hash = x.Hash,
                    Difference = x.DifferenceHash,
                }).ToArray();
                foreach (var result in results)
                {
                    result.Similarity = Global.CalcHammingSimilarity(info.DifferenceHash, result.Difference);
                }
            }
            else return Array.Empty<string>();

            var _results = results.Where(x => x.Similarity > info.MinSimilarity)
                .OrderByDescending(x => x.Similarity)
                .Select(x => x.Hash)
                .Skip(info.Offset)
                .Take(info.Limit)
                .ToArray();

            info.LastQueriedCount = _results.Length;
            return _results;
        }

        private static void OrderSortQuery(QueryInfo info, bool countResults)
        {
            if (info.Query is null) return;
            var query = info.Query;
            if (info.QueryType == TabType.DEFAULT)
            {
                switch (info.Sort)
                {
                    case Sort.PATH: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Paths.FirstOrDefault()) : query.OrderByDescending(x => x.Paths.FirstOrDefault()); break;
                    case Sort.NAME: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name); break;
                    case Sort.SIZE: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Size) : query.OrderByDescending(x => x.Size); break;
                    case Sort.UPLOAD: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.UploadTime) : query.OrderByDescending(x => x.UploadTime); break;
                    case Sort.CREATION: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.CreationTime) : query.OrderByDescending(x => x.CreationTime); break;
                    case Sort.LAST_WRITE: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.LastWriteTime) : query.OrderByDescending(x => x.LastWriteTime); break;
                    case Sort.LAST_EDIT: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.LastEditTime) : query.OrderByDescending(x => x.LastEditTime); break;
                    case Sort.DIMENSIONS: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Width * x.Height) : query.OrderByDescending(x => x.Width * x.Height); break;
                    case Sort.WIDTH: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Width) : query.OrderByDescending(x => x.Width); break;
                    case Sort.HEIGHT: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Height) : query.OrderByDescending(x => x.Height); break;
                    case Sort.TAG_COUNT: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Tags.Length) : query.OrderByDescending(x => x.Tags.Length); break;
                    case Sort.QUALITY: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Ratings["Quality"]) : query.OrderByDescending(x => x.Ratings["Quality"]); break;
                    case Sort.APPEAL: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Ratings["Appeal"]) : query.OrderByDescending(x => x.Ratings["Appeal"]); break;
                    case Sort.ART_STYLE: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Ratings["Art"]) : query.OrderByDescending(x => x.Ratings["Art"]); break;
                    case Sort.RED: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Red) : query.OrderByDescending(x => x.Red); break;
                    case Sort.GREEN: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Green) : query.OrderByDescending(x => x.Green); break;
                    case Sort.BLUE: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Blue) : query.OrderByDescending(x => x.Blue); break;
                    case Sort.ALPHA: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Alpha) : query.OrderByDescending(x => x.Alpha); break;
                    case Sort.LIGHT: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Light) : query.OrderByDescending(x => x.Light); break;
                    case Sort.DARK: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Dark) : query.OrderByDescending(x => x.Dark); break;
                    case Sort.YELLOW: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Red + x.Green) : query.OrderByDescending(x => x.Red + x.Green); break;
                    case Sort.CYAN: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Green + x.Blue) : query.OrderByDescending(x => x.Green + x.Blue); break;
                    case Sort.FUSCHIA: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Blue + x.Red) : query.OrderByDescending(x => x.Blue + x.Red); break;
                    case Sort.LIGHT_RED: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Red + x.Light) : query.OrderByDescending(x => x.Red + x.Light); break;
                    case Sort.DARK_RED: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Red + x.Dark) : query.OrderByDescending(x => x.Red + x.Dark); break;
                    case Sort.LIGHT_GREEN: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Green + x.Light) : query.OrderByDescending(x => x.Green + x.Light); break;
                    case Sort.DARK_GREEN: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Green + x.Dark) : query.OrderByDescending(x => x.Green + x.Dark); break;
                    case Sort.LIGHT_BLUE: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Blue + x.Light) : query.OrderByDescending(x => x.Blue + x.Light); break;
                    case Sort.DARK_BLUE: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Blue + x.Dark) : query.OrderByDescending(x => x.Blue + x.Dark); break;
                    case Sort.LIGHT_YELLOW: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Red + x.Green + x.Light) : query.OrderByDescending(x => x.Red + x.Green + x.Light); break;
                    case Sort.DARK_YELLOW: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Red + x.Green + x.Dark) : query.OrderByDescending(x => x.Red + x.Green + x.Dark); break;
                    case Sort.LIGHT_CYAN: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Green + x.Blue + x.Light) : query.OrderByDescending(x => x.Green + x.Blue + x.Light); break;
                    case Sort.DARK_CYAN: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Green + x.Blue + x.Dark) : query.OrderByDescending(x => x.Green + x.Blue + x.Dark); break;
                    case Sort.LIGHT_FUSCHIA: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Blue + x.Red + x.Light) : query.OrderByDescending(x => x.Blue + x.Red + x.Light); break;
                    case Sort.DARK_FUSCHIA: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Blue + x.Red + x.Dark) : query.OrderByDescending(x => x.Blue + x.Red + x.Dark); break;
                    case Sort.RANDOM:
                        query = query.OrderBy("RANDOM()");
                        info.ResultsRandom = query.ToEnumerable().Select(x => x.Hash);
                        info.Query = query;
                        return;
                    default: query = (info.Order == Order.ASCENDING) ? query.OrderBy(x => x.Hash) : query.OrderByDescending(x => x.Hash); break;
                }
            }
            else if (info.QueryType == TabType.SIMILARITY)
            {
                var hashes = new HashSet<string>(SimilarityQuery(info));
                query = query.Where(x => hashes.Contains(x.Hash));
            }

            if (info.LastQueriedCount == 0 && countResults) info.LastQueriedCount = query.Count();
            info.Query = query;
            info.Results = query.Select(x => x.Hash);
        }

        internal static string[] QueryDatabase(QueryInfo info, bool countResults=false)
        {
            if (info is null) return Array.Empty<string>();
            if (ManageQuery(ref info))
            {
                AddNumericalFilters(info);
                AddTagFilters(info);
                OrderSortQuery(info, countResults);
                queryHistory[info.Id] = info;
            }

            if (info.Sort == Sort.RANDOM)
                return info.ResultsRandom?.Skip(info.Offset).Take(info.Limit).ToArray() ?? Array.Empty<string>();
            return info.Results?.Offset(info.Offset).Limit(info.Limit).ToArray() ?? Array.Empty<string>();
        }

        internal static int GetLastQueriedCount(string id)
        {
            if (queryHistory.TryGetValue(id, out var info))
            {
                return info.LastQueriedCount;
            }
            return 0;
        }
    }
}
