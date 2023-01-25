using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using ImageTagger.Core;

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

        private static void AddNumericalFilter(QueryInfo info, string property, int min, int max)
        {
            var query = info.Query;
            if (query is null) return;
            if (min < 0 && max < 0) return;
            info.Filtered = true;

            // have already confirmed that at least one of min/max is > 0
            if (min < 0) query = query.Where((BsonExpression)$"$.{property} <= {max}"); // only max
            else if (max < 0) query = query.Where((BsonExpression)$"$.{property} >= {min}"); // only min
            else if (min < max) query = query.Where(Query.And($"$.{property} >= {min}", $"$.{property} <= {max}")); // min < max, both > 0
            else query = query.Where(Query.Or($"$.{property} <= {max}", $"$.{property} >= {min}")); // max < min, both > 0; inverse of above ie if max=3, min=7 then range is 0-3 OR 7+

            info.Query = query;
        }

        private static void AddNumericalFilter(QueryInfo info, string property, long min, long max)
        {
            var query = info.Query;
            if (query is null) return;
            if (min < 0 && max < 0) return;
            info.Filtered = true;

            // have already confirmed that at least one of min/max is > 0
            if (min < 0) query = query.Where((BsonExpression)$"$.{property} <= {max}"); // only max
            else if (max < 0) query = query.Where((BsonExpression)$"$.{property} >= {min}"); // only min
            else if (min < max) query = query.Where(Query.And($"$.{property} >= {min}", $"$.{property} <= {max}")); // min < max, both > 0
            else query = query.Where(Query.Or($"$.{property} <= {max}", $"$.{property} >= {min}")); // max < min, both > 0; inverse of above ie if max=3, min=7 then range is 0-3 OR 7+

            info.Query = query;
        }

        private static void AddNumericalFilter(QueryInfo info, string property, float min, float max)
        {
            var query = info.Query;
            if (query is null) return;
            if (min < 0 && max < 0) return;
            info.Filtered = true;

            // have already confirmed that at least one of min/max is > 0
            if (min < 0) query = query.Where((BsonExpression)$"$.{property} <= {max}"); // only max
            else if (max < 0) query = query.Where((BsonExpression)$"$.{property} >= {min}"); // only min
            else if (min < max) query = query.Where(Query.And($"$.{property} >= {min}", $"$.{property} <= {max}")); // min < max, both > 0
            else query = query.Where(Query.Or($"$.{property} <= {max}", $"$.{property} >= {min}")); // max < min, both > 0; inverse of above ie if max=3, min=7 then range is 0-3 OR 7+

            info.Query = query;
        }

        private static void AddCountFilter(QueryInfo info, string property, int min, int max)
        {
            var query = info.Query;
            if (query is null) return;
            if (min < 0 && max < 0) return;
            info.Filtered = true;

            // have already confirmed that at least one of min/max is > 0
            if (min < 0) query = query.Where((BsonExpression)$"COUNT($.{property}) <= {max}"); // only max
            else if (max < 0) query = query.Where((BsonExpression)$"COUNT($.{property}) >= {min}"); // only min
            else if (min < max) query = query.Where(Query.And($"COUNT($.{property}) >= {min}", $"COUNT($.{property}) <= {max}")); // min < max, both > 0
            else query = query.Where(Query.Or($"COUNT($.{property}) <= {max}", $"COUNT($.{property}) >= {min}")); // max < min, both > 0; inverse of above ie if max=3, min=7 then range is 0-3 OR 7+

            info.Query = query;
        }

        private static void AddStringFilter(QueryInfo info, string property, Filters filter)
        {
            var query = info.Query;
            if (query is null) return;
            if (filter.Empty()) return;
            info.Filtered = true;

            var conditions = new List<BsonExpression>();
            if (filter.All.Length > 0) query = query.Where(Filters.CreateCondition(property, filter.All, ExpressionType.All));
            if (filter.Any.Length > 0) query = query.Where(Filters.CreateCondition(property, filter.Any, ExpressionType.Any));
            if (filter.None.Length > 0) query = query.Where(Filters.CreateCondition(property, filter.None, ExpressionType.None));
            if (filter.Complex.Length > 0)
            {
                var condition = filter.ConstructComplexCondition(property);
                if (condition != null) query = query.Where(condition);
            }

            info.Query = query;
        }

        private static void AddFilters(QueryInfo info)
        {
            var query = info.Query;
            if (query is null) return;

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

            info.Query = query;
        }

        private static IEnumerable<string> SimilarityQuery(QueryInfo info, int offset, int limit, string pageId)
        {
            if (info?.Query is null) return Array.Empty<string>();
            var query = info.Query;
            var results = Enumerable.Empty<SimilarityQueryResult>();
            if (!pageId.Equals(CurrentId, StringComparison.InvariantCultureIgnoreCase)) return Array.Empty<string>();

            if (info.SortSimilarity == SortSimilarity.Averaged)
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

                foreach (var result in results)
                {
                    float simi1 = Global.CalcHammingSimilarity(info.WaveletHash, result.Wavelet);
                    float simi2 = Global.CalcHammingSimilarity(info.AverageHash, result.Average);
                    float simi3 = Global.CalcHammingSimilarity(info.DifferenceHash, result.Difference);
                    float simi4 = Global.CalcHammingSimilarity(info.PerceptualHash, result.Perceptual);
                    result.Similarity = (simi1 + simi2 + simi3 + simi4) / 4;
                }
            }
            else if (info.SortSimilarity == SortSimilarity.Average)
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
            else if (info.SortSimilarity == SortSimilarity.Wavelet)
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
            else if (info.SortSimilarity == SortSimilarity.Difference)
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
            else if (info.SortSimilarity == SortSimilarity.Perceptual)
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
                .Take(limit)
                .ToArray();

            info.LastQueriedCount = _results.Length;
            return _results;
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
                string expr = "((MAX([1, $.Colors[";
                for (int i = 0; i < val.Count-1; i++)
                {
                    if (i % 2 == 1) expr += $"{val[i]}]]) / 256) * (MAX([1, $.Colors[";
                    else expr += $"{val[i]}]]) * MAX([1, $.Colors[";
                }
                expr += $"{val[val.Count-1]}]])) / 256)";
                exprs.Add(expr);
            }

            if (sat.Count == 1) exprs.Add($"$.Colors[{sat[0]}]");
            else if (sat.Count > 1)
            {
                string expr = "((MAX([1, $.Colors[";
                for (int i = 0; i < sat.Count-1; i++)
                {
                    if (i % 2 == 1) expr += $"{sat[i]}]]) / 256) * (MAX([1, $.Colors[";
                    else expr += $"{sat[i]}]]) * MAX([1, $.Colors[";
                }
                expr += $"{sat[sat.Count-1]}]])) / 256)";
                exprs.Add(expr);
            }

            if (hue.Count == 1) exprs.Add($"$.Colors[{hue[0]}]");
            else if (hue.Count > 1)
            {
                string expr = "((MAX([1, $.Colors[";
                for (int i = 0; i < hue.Count - 1; i++)
                {
                    if (i % 2 == 1) expr += $"{hue[i]}]]) / 256) * (MAX([1, $.Colors[";
                    else expr += $"{hue[i]}]]) * MAX([1, $.Colors[";
                }
                expr += $"{hue[hue.Count - 1]}]])) / 256)";
                exprs.Add(expr);
            }

            return (BsonExpression)string.Join(" * ", exprs);
        }

        private static void OrderSortQuery(QueryInfo info, int offset, int limit, string pageId)
        {
            if (info.Query is null) return;
            var query = info.Query;

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
                    default: query = query.OrderBy($"$.{info.Sort}", order); break;
                }
            }
            else if (info.QueryType == TabType.Similarity)
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
                AddFilters(info);
                OrderSortQuery(info, modOffset, modLimit, pageId);
                queryHistory[info.Id] = info;
            }

            Thread.Sleep(50);
            if (!pageId.Equals(CurrentId, StringComparison.InvariantCultureIgnoreCase)) return Array.Empty<string>();

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
