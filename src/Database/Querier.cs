using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageTagger.Extension;
using LiteDB;

namespace ImageTagger.Database
{
    internal sealed class TagConditions
    {
        public string[] All;
        public string[] Any;
        public string[] None;
        public Dictionary<FilterType, string[]>[] Complex;
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

    internal static class Querier
    {
        private readonly static Dictionary<string, QueryInfo> queryHistory = new Dictionary<string, QueryInfo>();
        private readonly static Queue<string> queryHistoryQueue = new Queue<string>();
        private readonly static Dictionary<string, Page> pageHistory = new Dictionary<string, Page>();
        private readonly static Queue<string> pageHistoryQueue = new Queue<string>();
        public static string CurrentId { get; set; }

        private static BsonExpression CreateCondition(string[] tags, FilterType type)
        {
            // NONE
            if (type == FilterType.None)
                return (BsonExpression)$"($.Tags[*] ANY IN {BsonMapper.Global.Serialize(tags)}) != true";

            // one ALL or ANY
            if (tags.Length == 1)
                return Query.Contains("$.Tags[*] ANY", tags[0]);

            // multiple ALL or ANY
            var list = new List<BsonExpression>();
            foreach (string tag in tags)
                list.Add(Query.Contains("$.Tags[*] ANY", tag));

            // return And (ALL) or Or (ANY)
            if (type == FilterType.All)
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
                _All = new HashSet<string>(), _None = new HashSet<string>();
            var conditions = new List<Dictionary<FilterType, string[]>>();

            // convert global all/any/none into a condition
            if (all.Length > 0 || any.Length > 0 || none.Length > 0)
            {
                var condition = new Dictionary<FilterType, string[]>
                {
                    { FilterType.All, all },
                    { FilterType.Any, any },
                    { FilterType.None, none }
                };
                conditions.Add(condition);
            }

            // iterate over each string representation condition in complexTags
            foreach (string conditionStr in complex)
            {
                string[] sections = conditionStr.Split(new string[1] { "%" }, StringSplitOptions.None);
                if (sections.Length < 3) continue;

                string[] _all = sections[0].Split(new string[1] { "," }, StringSplitOptions.RemoveEmptyEntries);
                string[] _any = sections[1].Split(new string[1] { "," }, StringSplitOptions.RemoveEmptyEntries);
                string[] _none = sections[2].Split(new string[1] { "," }, StringSplitOptions.RemoveEmptyEntries);

                var condition = new Dictionary<FilterType, string[]>
                {
                    { FilterType.All, _all },
                    { FilterType.Any, _any },
                    { FilterType.None, _none },
                };
                conditions.Add(condition);

                Any.UnionWith(_any);
                _All.UnionWith(_all);
                _None.UnionWith(_none);
            }

            // find tags that should be added to global ALL/ANY
            foreach (string tag in _All)
            {
                bool presentInEveryCondition = true;
                foreach (var condition in conditions)
                {
                    if (!condition[FilterType.All].Contains(tag))
                    {
                        presentInEveryCondition = false;
                        Any.Add(tag);
                        break; // skip to next tag
                    }
                }
                if (presentInEveryCondition) All.Add(tag);
            }

            // find tags that should be added to global NONE
            foreach (string tag in _None)
            {
                bool presentInEveryCondition = true;
                foreach (var condition in conditions)
                {
                    if (!condition[FilterType.None].Contains(tag))
                    {
                        presentInEveryCondition = false;
                        break;
                    }
                }
                if (presentInEveryCondition) None.Add(tag);
            }

            return new TagConditions
            {
                All = All.ToArray(),
                Any = Any.ToArray(),
                None = None.ToArray(),
                Complex = conditions.ToArray()
            };
        }

        private static void AddTagFilters(QueryInfo info)
        {
            var query = info.Query;
            if (query is null) return;
            if (info.TagsAll?.Length == 0 && info.TagsAny?.Length == 0 && info.TagsNone?.Length == 0 && info.TagsComplex?.Length == 0) return;
            info.Filtered = true;

            if (info.TagsAll?.Length > 0) foreach (string tag in info.TagsAll) query = query.Where(x => x.Tags.Contains(tag));
            if (info.TagsAny?.Length > 0) query = query.Where("$.Tags ANY IN @0", BsonMapper.Global.Serialize(info.TagsAny));
            if (info.TagsNone?.Length > 0) query = query.Where("($.Tags[*] ANY IN @0) != true", BsonMapper.Global.Serialize(info.TagsNone));
            if (info.TagsComplex?.Length > 0)
            {
                var conditions = new List<BsonExpression>();
                foreach (var condition in info.TagsComplex)
                {
                    if (condition[FilterType.All].Length == 0 && condition[FilterType.Any].Length == 0 && condition[FilterType.None].Length == 0) continue;

                    var list = new List<BsonExpression>();
                    if (condition[FilterType.All].Length > 0)
                        list.Add(CreateCondition(condition[FilterType.All], FilterType.All));
                    if (condition[FilterType.Any].Length > 0)
                        list.Add(CreateCondition(condition[FilterType.Any], FilterType.Any));
                    if (condition[FilterType.None].Length > 0)
                        list.Add(CreateCondition(condition[FilterType.None], FilterType.None));

                    if (list.Count == 1) conditions.Add(list[0]);
                    else if (list.Count > 1) conditions.Add(Query.And(list.ToArray()));
                }
                if (conditions.Count == 1)
                    query = query.Where(conditions[0]);
                else if (conditions.Count > 1)
                    query = query.Where(Query.Or(conditions.ToArray()));
            }

            info.Query = query;
        }

        private static void AddNumericalFilter(QueryInfo info, string property, int min, int max)
        {
            var query = info.Query;
            if (query is null) return;
            if (min < 0 && max < 0) return;
            info.Filtered = true;

            if (min < 0) query = query.Where((BsonExpression)$"$.{property} <= {max}"); // only max
            else if (max < 0) query = query.Where((BsonExpression)$"$.{property} >= {min}"); // only min
            else if (min == max) query = query.Where((BsonExpression)$"$.{property} = {min}"); // min == max, both > 0 :: could replace with Query.EQ()
            else if (min < max) query = query.Where(Query.And($"$.{property} >= {min}", $"$.{property} <= {max}")); // min < max, both > 0
            else query = query.Where(Query.Or($"$.{property} >= {min}", $"$.{property} <= {max}")); // min > max, both > 0, inverse of above (min=7, max=3 :: results= 0-3 + 7+)

            info.Query = query;
        }

        private static void AddNumericalFilter(QueryInfo info, string property, long min, long max)
        {
            var query = info.Query;
            if (query is null) return;
            if (min < 0 && max < 0) return;
            info.Filtered = true;

            if (min < 0) query = query.Where((BsonExpression)$"$.{property} <= {max}"); // only max
            else if (max < 0) query = query.Where((BsonExpression)$"$.{property} >= {min}"); // only min
            else if (min == max) query = query.Where((BsonExpression)$"$.{property} = {min}"); // min == max, both > 0 :: could replace with Query.EQ()
            else if (min < max) query = query.Where(Query.And($"$.{property} >= {min}", $"$.{property} <= {max}")); // min < max, both > 0
            else query = query.Where(Query.Or($"$.{property} >= {min}", $"$.{property} <= {max}")); // min > max, both > 0, inverse of above (min=7, max=3 :: results= 0-3 + 7+)

            info.Query = query;
        }

        private static void AddNumericalFilter(QueryInfo info, string property, float min, float max)
        {
            var query = info.Query;
            if (query is null) return;
            if (min < 0 && max < 0) return;
            info.Filtered = true;

            if (min < 0) query = query.Where((BsonExpression)$"$.{property} <= {max}"); // only max
            else if (max < 0) query = query.Where((BsonExpression)$"$.{property} >= {min}"); // only min
            else if (min == max) query = query.Where((BsonExpression)$"$.{property} = {min}"); // min == max, both > 0 :: could replace with Query.EQ()
            else if (min < max) query = query.Where(Query.And($"$.{property} >= {min}", $"$.{property} <= {max}")); // min < max, both > 0
            else query = query.Where(Query.Or($"$.{property} >= {min}", $"$.{property} <= {max}")); // min > max, both > 0, inverse of above (min=7, max=3 :: results= 0-3 + 7+)

            info.Query = query;
        }

        private static void AddCountFilter(QueryInfo info, string property, int min, int max)
        {
            var query = info.Query;
            if (query is null) return;
            if (min < 0 && max < 0) return;
            info.Filtered = true;

            if (min < 0) query = query.Where((BsonExpression)$"COUNT($.{property}) <= {max}"); // only max
            else if (max < 0) query = query.Where((BsonExpression)$"COUNT($.{property}) >= {min}"); // only min
            else if (min == max) query = query.Where((BsonExpression)$"COUNT($.{property}) = {min}"); // min == max, both > 0 
            else if (min < max) query = query.Where(Query.And($"COUNT($.{property}) >= {min}", $"COUNT($.{property}) <= {max}")); // min < max, both > 0
            else query = query.Where(Query.Or($"COUNT($.{property}) >= {min}", $"COUNT($.{property}) <= {max}")); // min > max, both > 0, inverse of above 

            info.Query = query;
        }

        private static void AddStringFilter(QueryInfo info, string property, Filter filter)
        {
            var query = info.Query;
            if (query is null) return;
            if (filter.Empty()) return;
            info.Filtered = true;

            if (filter.All.Length > 0) query = query.Where(Filter.CreateCondition(property, filter.All, FilterType.All));
            if (filter.Any.Length > 0) query = query.Where(Filter.CreateCondition(property, filter.Any, FilterType.Any));
            if (filter.None.Length > 0) query = query.Where(Filter.CreateCondition(property, filter.None, FilterType.None));
            if (filter.Complex.Count > 0)
            {
                var condition = filter.CreateComplexCondition(property);
                if (condition != null) query = query.Where(condition);
            }

            info.Query = query;
        }

        private static void AddFilters(QueryInfo info)
        {
            // it would probably be best to move AddNumericalFilter, AddCountFilter, AddStringFilter to the Filter class and change them to 
            //  return a BsonExpression instead of taking a QueryInfo argument

            // also I think I will merge queryInfo into tabInfo and just pass tabInfo around while querying; this will also allow per-tab filters
            //  for global filters, easiest option is to just use GetTabInfo(Global.All) for every query (overriding most of their tabInfo)

            AddStringFilter(info, "Imports", info.Imports); // might remove
            AddStringFilter(info, "Folders", info.Folders);
            AddStringFilter(info, "Groups", info.Groups);
            AddStringFilter(info, "Creators", info.Creators);
            AddStringFilter(info, "Copyrights", info.Copyrights);
            AddStringFilter(info, "Subjects", info.Subjects);
            AddStringFilter(info, "Descriptive", info.Descriptive);

            AddNumericalFilter(info, "Width", info.MinWidth, info.MaxWidth);
            AddNumericalFilter(info, "Height", info.MinHeight, info.MaxHeight);
            AddNumericalFilter(info, "Size", info.MinSize, info.MaxSize);
            AddNumericalFilter(info, "CreationTime", info.MinCreationTime, info.MaxCreationTime);
            AddNumericalFilter(info, "UploadTime", info.MinUploadTime, info.MaxUploadTime);
            AddNumericalFilter(info, "LastWriteTime", info.MinLastWriteTime, info.MaxLastWriteTime);
            AddNumericalFilter(info, "LastEditTime", info.MinLastEditTime, info.MaxLastEditTime);
            AddNumericalFilter(info, "RatingSum", info.MinRatingSum, info.MaxRatingSum);
            AddNumericalFilter(info, "RatingAvg", info.MinRatingAvg, info.MaxRatingAvg);

            AddCountFilter(info, "Descriptive", info.MinTagCount, info.MaxTagCount);
        }

        // can create a + and - color[] and multiply out the positive ones, then subtract the multiplied negative ones
        // to allow the user to filter out specific colors as well
        private static BsonExpression ConstructColorSort(Colors[] colors)
        {
            if (colors.Length == 0) return (BsonExpression)"$.Hash";
            if (colors.Length == 1) return (BsonExpression)$"$.Colors[{(int)colors[0]}]";

            var hue = new List<int>();
            var sat = new List<int>();
            var val = new List<int>();
            var exprs = new List<string>();

            foreach (int color in colors.Select(c => (int)c))
            {
                if (color <= (int)Colors.Fuchsia) hue.Add(color);
                else if (color <= (int)Colors.Dull) sat.Add(color);
                else val.Add(color);
            }

            if (val.Count == 1) exprs.Add($"$.Colors[{val[0]}]");
            else if (val.Count > 1)
            {
                var expr = new StringBuilder("((MAX([1, $.Colors[");
                for (int i = 0; i < val.Count-1; i++)
                {
                    if (i % 2 == 1) expr.Append(val[i]).Append("]]) / 256) * (MAX([1, $.Colors[");
                    else expr.Append(val[i]).Append("]]) * MAX([1, $.Colors[");
                }
                expr.Append(val[val.Count - 1]).Append("]])) / 256)");
                exprs.Add(expr.ToString());
            }

            if (sat.Count == 1) exprs.Add($"$.Colors[{sat[0]}]");
            else if (sat.Count > 1)
            {
                var expr = new StringBuilder("((MAX([1, $.Colors[");
                for (int i = 0; i < sat.Count-1; i++)
                {
                    if (i % 2 == 1) expr.Append(sat[i]).Append("]]) / 256) * (MAX([1, $.Colors[");
                    else expr.Append(sat[i]).Append("]]) * MAX([1, $.Colors[");
                }
                expr.Append(sat[sat.Count - 1]).Append("]])) / 256)");
                exprs.Add(expr.ToString());
            }

            if (hue.Count == 1) exprs.Add($"$.Colors[{hue[0]}]");
            else if (hue.Count > 1)
            {
                var expr = new StringBuilder("((MAX([1, $.Colors[");
                for (int i = 0; i < hue.Count - 1; i++)
                {
                    if (i % 2 == 1) expr.Append(hue[i]).Append("]]) / 256) * (MAX([1, $.Colors[");
                    else expr.Append(hue[i]).Append("]]) * MAX([1, $.Colors[");
                }
                expr.Append(hue[hue.Count - 1]).Append("]])) / 256)");
                exprs.Add(expr.ToString());
            }

            //Console.WriteLine(string.Join(" * ", exprs));
            return (BsonExpression)string.Join(" * ", exprs);
        }

        //private static readonly BsonExpression[] buckets = new BsonExpression[4];
        private static void PrefilterSimilarity(QueryInfo info)
        {
            // make sure to call GetPlan().ToString() and ensure that things that are supposed to use index are NOT doing a 'Full Index Scan'
            var query = info.Query;
            if (query is null) return;

            buckets.Clear();
            foreach (ushort num in info.Buckets)
                buckets.UnionWith(GetBuckets(num, Global.Settings.BucketVariance));

            query = query.Where("$.Buckets[*] ANY IN @0", BsonMapper.Global.Serialize(buckets));
            info.Query = query;
        }

        private static readonly int[] andMasks = new int[16]
        {
            0b1111_1111_1111_1110,
            0b1111_1111_1111_1101,
            0b1111_1111_1111_1011,
            0b1111_1111_1111_0111,

            0b1111_1111_1110_1111,
            0b1111_1111_1101_1111,
            0b1111_1111_1011_1111,
            0b1111_1111_0111_1111,

            0b1111_1110_1111_1111,
            0b1111_1101_1111_1111,
            0b1111_1011_1111_1111,
            0b1111_0111_1111_1111,

            0b1110_1111_1111_1111,
            0b1101_1111_1111_1111,
            0b1011_1111_1111_1111,
            0b0111_1111_1111_1111
        };

        private static readonly int[] orMasks = new int[16]
        {
            0b0000_0000_0000_0001,
            0b0000_0000_0000_0010,
            0b0000_0000_0000_0100,
            0b0000_0000_0000_1000,

            0b0000_0000_0001_0000,
            0b0000_0000_0010_0000,
            0b0000_0000_0100_0000,
            0b0000_0000_1000_0000,

            0b0000_0001_0000_0000,
            0b0000_0010_0000_0000,
            0b0000_0100_0000_0000,
            0b0000_1000_0000_0000,

            0b0001_0000_0000_0000,
            0b0010_0000_0000_0000,
            0b0100_0000_0000_0000,
            0b1000_0000_0000_0000,
        };

        private static readonly HashSet<ushort> emptyBuckets = new HashSet<ushort>(0);
        private static readonly HashSet<ushort> buckets0 = new HashSet<ushort>(1);
        private static readonly HashSet<ushort> buckets1 = new HashSet<ushort>(17);
        private static readonly HashSet<ushort> buckets2 = new HashSet<ushort>(137);
        private static readonly HashSet<ushort> buckets = new HashSet<ushort>(548);

        // note: and/or are both needed; example:
        //  bucket (have) = 1101    bucket (want from database) = 0111
        //  the and mask (0111) would fill in one missing bit, resulting in 0101
        //  but the or mask (0010) is also needed to get to 0111
        // (this is a simplified example as an actual bucket/mask is 16 bits)
        // (this example also assumes that the precision is set to <= +-2 incorrect bits)
        private static HashSet<ushort> GetBuckets(ushort bucket, byte variance)
        {
            if (variance == 0)
            {
                buckets0.Clear();
                buckets0.Add(bucket);
                return buckets0;
            }
            if (variance == 1)
            {
                buckets1.Clear();
                buckets1.Add(bucket);
                for (int i = 0; i < 16; i++)
                {
                    buckets1.Add((ushort)(bucket & andMasks[i]));
                    buckets1.Add((ushort)(bucket | orMasks[i]));
                }
                return buckets1;
            }
            if (variance == 2)
            {
                // there might be a clever way of iterating this that would allow me to use an int[137] instead
                // (maybe iterating backwards with j?) (this creates a very large number of duplicates, hence the hashset)
                buckets2.Clear();
                buckets2.Add(bucket);
                for (int i = 0; i < 16; i++)
                {
                    for (int j = 0; j < 16; j++)
                    {
                        buckets2.Add((ushort)((bucket & andMasks[i]) | orMasks[j]));
                        buckets2.Add((ushort)(bucket & andMasks[i] & andMasks[j]));
                        buckets2.Add((ushort)(bucket | orMasks[i] | orMasks[j]));
                    }
                }
                //Console.WriteLine(string.Join(", ", tmp));
                return buckets2;
            }
            return emptyBuckets;
        }

        private static void OrderSortQuery(QueryInfo info)
        {
            if (info.Query is null) return;
            var query = info.Query;

            // OrderBy($.Hash) is not working at all and I have no idea why; it is the only one that does not work
            if (info.QueryType == TabType.Default)
            {
                int order = (info.Order == Order.Ascending) ? 1 : -1;
                switch (info.Sort)
                {
                    case Sort.Path: query = query.OrderBy(x => x.Paths.FirstOrDefault(), order); break;
                    case Sort.Dimensions: query = query.OrderBy(x => x.Width * x.Height, order); break;
                    case Sort.TagCount: query = query.OrderBy("COUNT($.Tags)", order); break; // could be slower; have not tested at scale yet
                    case Sort.Quality: query = query.OrderBy(x => x.Ratings["Quality"], order); break;
                    case Sort.Appeal: query = query.OrderBy(x => x.Ratings["Appeal"], order); break;
                    case Sort.ArtStyle: query = query.OrderBy(x => x.Ratings["Art"], order); break;
                    case Sort.Color: query = query.OrderBy(ConstructColorSort(info.Colors), order); break;
                    case Sort.Random:
                        query = query.OrderBy("RANDOM()");
                        info.ResultsRandom = query.ToEnumerable().Select(x => x.Hash);
                        info.Query = query;
                        return;
                    case Sort.Hash: query = query.OrderBy("$._id", order); break;
                    default: query = query.OrderBy($"$.{info.Sort}", order); break;
                }
            }
            else if (info.QueryType == TabType.Similarity)
            {
                if (Global.Settings.UsePrefilter) PrefilterSimilarity(info);
                switch (info.SortSimilarity)
                {
                    case SortSimilarity.Averaged:
                        const string condition1 = "SIMILARITY_AVERAGED([$.AverageHash, $.ColorHash, $.DifferenceHash, $.PerceptualHash, $.WaveletHash], ";
                        string condition2 = $"[{(long)info.AverageHash}, {(long)info.ColorHash}, {(long)info.DifferenceHash}, {(long)info.PerceptualHash}, {(long)info.WaveletHash}])";
                        string averaged = condition1 + condition2;
                        query = query
                            .Where((BsonExpression)$"({averaged}) > {info.MinSimilarity}")
                            .OrderBy(averaged, -1);
                        break;
                    case SortSimilarity.Average:
                        string average = $"SIMILARITY($.AverageHash, {(long)info.AverageHash})";
                        query = query
                            .Where((BsonExpression)$"({average}) > {info.MinSimilarity}")
                            .OrderBy(average, -1);
                        break;
                    case SortSimilarity.Color:
                        string color = $"SIMILARITY($.ColorHash, {(long)info.ColorHash})";
                        query = query
                            .Where((BsonExpression)$"({color}) > {info.MinSimilarity}")
                            .OrderBy(color, -1);
                        break;
                    case SortSimilarity.Difference:
                        string difference = $"SIMILARITY($.DifferenceHash, {(long)info.DifferenceHash})";
                        query = query
                            .Where((BsonExpression)$"({difference}) > {info.MinSimilarity}")
                            .OrderBy(difference, -1);
                        break;
                    case SortSimilarity.Perceptual:
                        string perceptual = $"SIMILARITY($.PerceptualHash, {(long)info.PerceptualHash})";
                        query = query
                            .Where((BsonExpression)$"({perceptual}) > {info.MinSimilarity}")
                            .OrderBy(perceptual, -1);
                        break;
                    case SortSimilarity.Wavelet:
                        string wavelet = $"SIMILARITY($.WaveletHash, {(long)info.WaveletHash})";
                        query = query
                            .Where((BsonExpression)$"({wavelet}) > {info.MinSimilarity}")
                            .OrderBy(wavelet, -1);
                        break;
                    default: query = query.OrderBy("$._id", -1); break;
                }
                info.LastQueriedCount = query.Count();
            }

            info.Query = query;
            info.Results = query.Select(x => x.Hash);
        }

        private static void WriteTime(string text, ref DateTime prev)
        {
            Console.WriteLine($"{text}: " + (DateTime.Now - prev).ToString());
            prev = DateTime.Now;
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
                OrderSortQuery(info);
                queryHistory[info.Id] = info;
            }

            //Thread.Sleep(20);
            if (!pageId.Equals(CurrentId, StringComparison.OrdinalIgnoreCase)) return Array.Empty<string>();

            if (info.Filtered && Global.Settings.PreferSpeed)
            {
                if (info.Sort == Sort.Random && info.QueryType != TabType.Similarity)
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
                if (info.Sort == Sort.Random && info.QueryType != TabType.Similarity)
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
            if (info.QueryType != TabType.Similarity)
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
