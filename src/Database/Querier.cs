using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageTagger.Metadata;
using LiteDB;

namespace ImageTagger.Database
{
    internal sealed class TagConditions
    {
        public string[] All;
        public string[] Any;
        public string[] None;
        public List<Dictionary<ExpressionType, HashSet<string>>> Complex;
    }

    internal sealed class Page
    {
        internal int TotalQueriedCount { get; set; }
        internal string[] Hashes { get; set; }

        internal Page()
        {
            TotalQueriedCount = -1;
            Hashes = Array.Empty<string>();
        }

        internal Page(int count, string[] hashes)
        {
            TotalQueriedCount = count;
            Hashes = hashes;
        }
    }

    internal sealed class Querier
    {
        private readonly static Dictionary<string, QueryInfo> queryHistory = new Dictionary<string, QueryInfo>();
        private readonly static Queue<string> queryHistoryQueue = new Queue<string>();
        private readonly static Dictionary<string, Page> pageHistory = new Dictionary<string, Page>();
        private readonly static Queue<string> pageHistoryQueue = new Queue<string>();
        public static string CurrentId { get; set; }

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

        // I imagine the UI for creating this array as a container with a dropdown and add button next to it
        //  user selects the types they want to show up from the dropdown, click Add button, and then it adds
        //  the index to the array and adds a little node that shows the name to the container; then the user
        //  can select it and delete it (like tag container); it will also automatically prevent duplicates;
        //  there will also be grouped options available (large/static/animated)
        private static void AddImageTypeFilter(QueryInfo info, ImageType[] types)
        {
            if (types.Length == 0) return;
            info.Filtered = true;

            if (types.Length == 1)
            {
                info.Query = info.Query.Where(x => x.ImageType == types[0]);
            }
            else
            {
                var list = new List<BsonExpression>();
                foreach (ImageType _type in types)
                {
                    list.Add(Query.EQ("$.ImageType", (BsonValue)_type.ToString()));
                }
                info.Query = info.Query.Where(Query.Or(list.ToArray()));
            }
        }

        private static bool HasPage(string pageId, out Page page)
        {
            if (pageHistory.TryGetValue(pageId, out page)) return true;
            page = new Page();
            return false;
        }

        private static void ManagePage(string pageId, Page page)
        {
            if (pageHistoryQueue.Count == Global.Settings.MaxPagesToStore)
                pageHistory.Remove(pageHistoryQueue.Dequeue());
            pageHistoryQueue.Enqueue(pageId);
            pageHistory[pageId] = page;
        }

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

            //AddImageTypeFilter(info, new ImageType[2] { ImageType.APNG, ImageType.GIF });

            // apply filters
            // these will also need to set info.Filtered if any are active (ideally this will be done automatically when they change)

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

            all = All.ToArray();
            any = Any.ToArray();
            none = None.ToArray();

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

            if (info.TagsAll?.Length == 0 && info.TagsAny?.Length == 0 && info.TagsNone?.Length == 0 && info.TagsComplex?.Count == 0)
            {
                return;
            }
            info.Filtered = true;

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

        private static IEnumerable<string> SimilarityQuery(QueryInfo info, int offset, int limit, string pageId)
        {
            if (info?.Query is null) return Array.Empty<string>();
            var query = info.Query;
            var results = Enumerable.Empty<SimilarityQueryResult>();
            if (!pageId.Equals(CurrentId, StringComparison.InvariantCultureIgnoreCase)) return Array.Empty<string>();

            if (info.SortSimilarity == SortSimilarity.AVERAGED)
            {
                var tmp = query.Select(x => new SimilarityQueryResult
                {
                    Hash = x.Hash,
                    Wavelet = x.WaveletHash,
                    Average = x.AverageHash,
                    Difference = x.DifferenceHash,
                    Perceptual = x.PerceptualHash,
                });

                Thread.Sleep(100);
                if (!pageId.Equals(CurrentId, StringComparison.InvariantCultureIgnoreCase)) return Array.Empty<string>();
                results = tmp.ToArray();

                foreach (var result in results) // results is empty on new que
                {
                    float simi1 = Global.CalcHammingSimilarity(info.WaveletHash, result.Wavelet);
                    float simi2 = Global.CalcHammingSimilarity(info.AverageHash, result.Average);
                    float simi3 = Global.CalcHammingSimilarity(info.DifferenceHash, result.Difference);
                    float simi4 = Global.CalcHammingSimilarity(info.PerceptualHash, result.Perceptual);
                    result.Similarity = (simi1 + simi2 + simi3 + simi4) / 4;
                }
            }
            else if (info.SortSimilarity == SortSimilarity.AVERAGE)
            {
                var tmp = query.Select(x => new SimilarityQueryResult
                {
                    Hash = x.Hash,
                    Average = x.AverageHash,
                });

                Thread.Sleep(100);
                if (!pageId.Equals(CurrentId, StringComparison.InvariantCultureIgnoreCase)) return Array.Empty<string>();
                results = tmp.ToArray();

                foreach (var result in results)
                {
                    float temp = Global.CalcHammingSimilarity(info.AverageHash, result.Average) / 100;
                    result.Similarity = (float)Math.Pow(temp, 3) * 100;
                }
            }
            else if (info.SortSimilarity == SortSimilarity.WAVELET)
            {
                var tmp = query.Select(x => new SimilarityQueryResult
                {
                    Hash = x.Hash,
                    Wavelet = x.WaveletHash
                });

                Thread.Sleep(100);
                if (!pageId.Equals(CurrentId, StringComparison.InvariantCultureIgnoreCase)) return Array.Empty<string>();
                results = tmp.ToArray();

                foreach (var result in results)
                {
                    result.Similarity = Global.CalcHammingSimilarity(info.WaveletHash, result.Wavelet);
                }
            }
            else if (info.SortSimilarity == SortSimilarity.DIFFERENCE)
            {
                var tmp = query.Select(x => new SimilarityQueryResult
                {
                    Hash = x.Hash,
                    Difference = x.DifferenceHash,
                });

                Thread.Sleep(100);
                if (!pageId.Equals(CurrentId, StringComparison.InvariantCultureIgnoreCase)) return Array.Empty<string>();
                results = tmp.ToArray();

                foreach (var result in results)
                {
                    result.Similarity = Global.CalcHammingSimilarity(info.DifferenceHash, result.Difference);
                }
            }
            else if (info.SortSimilarity == SortSimilarity.PERCEPTUAL)
            {
                var tmp = query.Select(x => new SimilarityQueryResult
                {
                    Hash = x.Hash,
                    Perceptual = x.PerceptualHash
                });

                Thread.Sleep(100);
                if (!pageId.Equals(CurrentId, StringComparison.InvariantCultureIgnoreCase)) return Array.Empty<string>();
                results = tmp.ToArray();

                foreach (var result in results)
                {
                    result.Similarity = Global.CalcHammingSimilarity(info.PerceptualHash, result.Perceptual);
                }
            }
            else
            {
                return Array.Empty<string>();
            }

            var _results = results.Where(x => x.Similarity > info.MinSimilarity)
                .OrderByDescending(x => x.Similarity)
                .Select(x => x.Hash)
                .Skip(offset)
                .Take(limit) // consider removing/increasing this limit
                .ToArray();

            info.LastQueriedCount = _results.Length;
            return _results;
        }

        private static void OrderSortQuery(QueryInfo info, int offset, int limit, string pageId)
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
                var hashes = new HashSet<string>(SimilarityQuery(info, offset, limit, pageId));
                query = query.Where(x => hashes.Contains(x.Hash));
            }

            info.Query = query;
            info.Results = query.Select(x => x.Hash);
        }

        internal static async Task<string[]> QueryDatabase(QueryInfo info, int offset, int limit, bool forceUpdate)
        {
            if (info is null) return Array.Empty<string>();
            string pageId = $"{info.Id}?{offset}?{limit}";

            if (HasPage(pageId, out Page page) && !forceUpdate)
                return page.Hashes;

            int desired_page = offset / Math.Max(1, limit);
            int modOffset = Math.Max(0, offset - (limit * Global.Settings.MaxExtraQueriedPages));
            int modLimit = limit * (desired_page + Global.Settings.MaxExtraQueriedPages);

            if (ManageQuery(ref info) || forceUpdate)
            {
                info.Query = DatabaseAccess.GetImageInfoQuery();
                AddNumericalFilters(info);
                AddTagFilters(info);
                OrderSortQuery(info, modOffset, modLimit, pageId);
                queryHistory[info.Id] = info;
            }

            Thread.Sleep(50);
            if (!pageId.Equals(CurrentId, StringComparison.InvariantCultureIgnoreCase)) return Array.Empty<string>();

            if (info.Filtered && Global.Settings.PreferSpeed)
            {
                if (info.Sort == Sort.RANDOM && info.QueryType != TabType.SIMILARITY)
                {
                    page.Hashes = await Task.Run(() => info.ResultsRandom?
                        .Skip(modOffset)
                        .Take(limit * Global.Settings.MaxPagesToStore)
                        .ToArray() ?? Array.Empty<string>());
                }
                else
                {
                    page.Hashes = await Task.Run(() => info.Results?
                        .Offset(modOffset)
                        .Limit(limit * Global.Settings.MaxPagesToStore)
                        .ToArray() ?? Array.Empty<string>());
                }
            }
            else
            {
                if (info.Sort == Sort.RANDOM && info.QueryType != TabType.SIMILARITY)
                {
                    page.Hashes = await Task.Run(() => info.ResultsRandom?
                        .Skip(modOffset)
                        .Take(modLimit)
                        .ToArray() ?? Array.Empty<string>());
                }
                else
                {
                    page.Hashes = await Task.Run(() => info.Results?
                        .Offset(modOffset)
                        .Limit(modLimit)
                        .ToArray() ?? Array.Empty<string>());
                }
            }

            // similarity tabs are forced to query all results, and they keep track of count themselves
            if (info.QueryType != TabType.SIMILARITY)
            {
                if (!info.Filtered) info.LastQueriedCount = info.Success;
                else if (Global.Settings.PreferSpeed) info.LastQueriedCount = page.Hashes.Length;
                else if (info.LastQueriedCount == -1) info.LastQueriedCount = info.Query.Count();
                queryHistory[info.Id] = info;
            }

            string[] ret = Array.Empty<string>();
            if (page.Hashes.Length > limit)
            {
                for (int i = 0; i < page.Hashes.Length; i += limit)
                {
                    string _pageId = $"{info.Id}?{modOffset+i}?{limit}";
                    if (pageHistory.ContainsKey(_pageId) && !forceUpdate) continue;
                    string[] newArr = new string[Math.Min(limit, page.Hashes.Length - i)];
                    Array.Copy(page.Hashes, i, newArr, 0, newArr.Length);
                    ManagePage(_pageId, new Page(info.LastQueriedCount, newArr));
                    if (i == offset)
                    {
                        ret = newArr;
                    }
                }
            }
            else
            {
                ManagePage(pageId, new Page(info.LastQueriedCount, page.Hashes));
                return page.Hashes;
            }

            return ret;
        }

        internal static int GetLastQueriedCount(string id)
        {
            if (pageHistory.TryGetValue(id, out var page))
            {
                return page.TotalQueriedCount;
            }
            return -1;
        }
    }
}
