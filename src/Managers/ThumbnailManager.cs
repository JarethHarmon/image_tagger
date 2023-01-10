using Godot;
using ImageTagger.Database;
using ImageTagger.Importer;
using ImageTagger.Metadata;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ImageTagger.Managers
{
    public sealed class ThumbnailManager : Node
    {
        private static string currentPageId;
        private static QueryInfo currentQuery;
        private int offset;
        private string thumbnailPath;

        private readonly Dictionary<string, Godot.ImageTexture> thumbnailHistory = new Dictionary<string, ImageTexture>();
        private readonly Queue<string> thumbnailHistoryQueue = new Queue<string>();

        private static readonly object locker = new object();

        private ItemList list;
        private ImageTexture failedIcon, bufferingIcon;
        private Node signals;
        private CenterContainer buffer;

        private void Setup()
        {
            currentQuery = new QueryInfo
            {
                QueryType = TabType.DEFAULT,
                Order = Global.Settings.CurrentOrder,
                Sort = Global.Settings.CurrentSort,
                SortSimilarity = Global.Settings.CurrentSortSimilarity,
                MinSimilarity = 74.999f,
            };
        }

        public override void _Ready()
        {
            list = GetNode<ItemList>("/root/main/margin/vbox/hsplit/left/vsplit/thumbnail_list/margin/vbox/thumbnails");
            signals = GetNode<Node>("/root/Signals");
            buffer = list.GetNode<CenterContainer>("cc");

            var resBroken = ResourceLoader.Load<StreamTexture>("res://assets/icon-broken.png");
            failedIcon = new Godot.ImageTexture();
            failedIcon.CreateFromImage(resBroken.GetData(), 0);
            failedIcon.SetMeta("image_hash", "0");

            var resBuffer = ResourceLoader.Load<StreamTexture>("res://assets/buffer-01.png");
            bufferingIcon = new Godot.ImageTexture();
            bufferingIcon.CreateFromImage(resBuffer.GetData(), 0);
            bufferingIcon.SetMeta("image_hash", "1");

            CallDeferred(nameof(Setup));
        }

        public void UpdateTagsAll(string[] tagsAll)
        {
            currentQuery.TagsAll = tagsAll;
            QueryDatabaseGD();
        }

        public void UpdateTagsAny(string[] tagsAny)
        {
            currentQuery.TagsAny = tagsAny;
            QueryDatabaseGD();
        }

        public void UpdateTagsNone(string[] tagsNone)
        {
            currentQuery.TagsNone = tagsNone;
            QueryDatabaseGD();
        }

        public void UpdateTagsComplex(string[] tagsComplex)
        {
            var conditions = Querier.ConvertStringToComplexTags(currentQuery.TagsAll, currentQuery.TagsAny, currentQuery.TagsNone, tagsComplex);
            currentQuery.TagsAll = conditions.All;
            currentQuery.TagsAny = conditions.Any;
            currentQuery.TagsNone = conditions.None;
            currentQuery.TagsComplex = conditions.Complex;
            QueryDatabaseGD();
        }

        public void UpdateSort(int sort)
        {
            currentQuery.Sort = (Sort)sort;
            QueryDatabaseGD();
        }

        public void UpdateOrder(int order)
        {
            currentQuery.Order = (Order)order;
            QueryDatabaseGD();
        }

        public void UpdateSortSimilarity(int similarity)
        {
            currentQuery.SortSimilarity = (SortSimilarity)similarity;
            QueryDatabaseGD();
        }

        public void UpdateImportId(string tabId)
        {
            var tabInfo = TabInfoAccess.GetTabInfo(tabId);
            currentQuery.ImportId = tabInfo.ImportId ?? Global.ALL;

            if (tabInfo.TabType == TabType.SIMILARITY)
            {
                currentQuery.ImportId = Global.ALL;
                var hashes = ImageImporter.GetPerceptualHashes(tabInfo.SimilarityHash);
                currentQuery.AverageHash = hashes.Average;
                currentQuery.DifferenceHash = hashes.Difference;
                currentQuery.PerceptualHash = hashes.Perceptual;
                currentQuery.WaveletHash = hashes.Wavelet;
            }

            var iinfo = ImportInfoAccess.GetImportInfo(currentQuery.ImportId);
            currentQuery.Success = (iinfo.Id.Equals(Global.ALL)) ? iinfo?.Success ?? 0 : (iinfo?.Success + iinfo?.Duplicate) ?? 0;
            currentQuery.QueryType = tabInfo.TabType;

            QueryDatabaseGD();
        }

        public void UpdatePage(int pageNumber)
        {
            offset = (pageNumber - 1) * Global.Settings.MaxImagesPerPage;
            QueryDatabaseGD();
        }

        public void QueryDatabaseGD(bool forceUpdate = false)
        {
            var info = currentQuery.Clone();
            info.LastQueriedCount = -1;
            string queryId = info.CalcId();

            string pageId = $"{queryId}?{offset}?{Global.Settings.MaxImagesPerPage}";
            currentPageId = pageId;
            Querier.CurrentId = pageId;

            _ = Task.Run(() => QueryDatabase(info, pageId, forceUpdate));
        }

        private sealed class ThumbnailTask
        {
            public string Id { get; set; }
            public string Hash { get; set; }
            public int Index { get; set; }

            public ThumbnailTask(string id, string hash, int index)
            {
                Id = id;
                Hash = hash;
                Index = index;
            }
        }

        private async Task QueryDatabase(QueryInfo info, string pageId, bool forceUpdate=false)
        {
            buffer.Show();
            thumbnailPath = Global.GetThumbnailPath();
            if (thumbnailPath is null) return;

            //var now = DateTime.Now;
            string[] results = await Querier.QueryDatabase(info, offset, Global.Settings.MaxImagesPerPage, forceUpdate);
            //Console.WriteLine((DateTime.Now - now).ToString());

            SetupList(results.Length, pageId, results);
            await LoadThumbnails(results, pageId);
            if (currentPageId.Equals(pageId, StringComparison.InvariantCultureIgnoreCase))
                buffer.Hide();
        }

        private void SetupList(int size, string pageId, string[] results)
        {
            lock (locker)
            {
                if (!currentPageId.Equals(pageId, StringComparison.InvariantCultureIgnoreCase)) return;
                int queriedImageCount = Querier.GetLastQueriedCount(pageId);
                int queriedPageCount = (int)Math.Ceiling((float)queriedImageCount / Global.Settings.MaxImagesPerPage);
                signals.Call("emit_signal", "max_pages_changed", queriedPageCount);
                signals.Call("emit_signal", "image_count_changed", queriedImageCount);

                list.Set("current_hashes", results);
                list.Clear();

                for (int i = 0; i < size; i++)
                {
                    list.AddIconItem(bufferingIcon);
                }
                // set scroll value for tab
            }
        }

        private async Task LoadThumbnails(string[] hashes, string pageId)
        {
            if (!currentPageId.Equals(pageId, StringComparison.InvariantCultureIgnoreCase)) return;

            var tasks = new ActionBlock<ThumbnailTask>(x => LoadThumbnail(x));
            for (int i = 0; i < hashes.Length; i++)
            {
                tasks.Post(new ThumbnailTask(pageId, hashes[i], i));
            }

            try
            {
                if (!currentPageId.Equals(pageId, StringComparison.InvariantCultureIgnoreCase)) return;
                tasks.Complete();
                await tasks.Completion;
            }
            catch (AggregateException aex)
            {
                Console.Write(aex);
            }
        }

        private async void LoadThumbnail(ThumbnailTask x)
        {
            if (x.Hash is null) return;
            if (ThreadsafeSetIcon(x)) return; // try to set icon from history, return if time to stop loading, or loading succeeded

            // if loading from history failed, try to load from disk
            string path = System.IO.Path.Combine(thumbnailPath, $"{x.Hash.Substring(0, 2)}/{x.Hash}.thumb");
            try
            {
                await Task.Run(() =>
                {
                    if (!currentPageId.Equals(x.Id, StringComparison.InvariantCultureIgnoreCase)) return;
                    byte[] data = System.IO.File.ReadAllBytes(path);

                    if (!currentPageId.Equals(x.Id, StringComparison.InvariantCultureIgnoreCase)) return;
                    var image = new Godot.Image();
                    var err = image.LoadWebpFromBuffer(data);
                    if (!currentPageId.Equals(x.Id, StringComparison.InvariantCultureIgnoreCase)) return;
                    if (err != Godot.Error.Ok) ThreadsafeSetIcon(x, true);

                    if (!currentPageId.Equals(x.Id, StringComparison.InvariantCultureIgnoreCase)) return;
                    var texture = new Godot.ImageTexture();
                    texture.CreateFromImage(image, 0);
                    texture.SetMeta("image_hash", x.Hash);
                    AddToHistory(x.Hash, texture); // may as well store it in history once loaded

                    if (!currentPageId.Equals(x.Id, StringComparison.InvariantCultureIgnoreCase)) return;
                    ThreadsafeSetIcon(x);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                if (!currentPageId.Equals(x.Id, StringComparison.InvariantCultureIgnoreCase)) return;
                ThreadsafeSetIcon(x, true);
            }
        }

        private void AddToHistory(string hash, Godot.ImageTexture texture)
        {
            lock (locker)
            {
                if (thumbnailHistoryQueue.Count == Global.Settings.MaxThumbnailsToStore)
                {
                    thumbnailHistory.Remove(thumbnailHistoryQueue.Dequeue());
                }
                thumbnailHistory[hash] = texture;
                thumbnailHistoryQueue.Enqueue(hash);
            }
        }

        private bool ThreadsafeSetIcon(ThumbnailTask x, bool failed=false)
        {
            lock (locker)
            {
                Godot.ImageTexture icon = null;
                if (failed) icon = failedIcon;
                else thumbnailHistory.TryGetValue(x.Hash, out icon);

                if (icon is null) return false; // not in history, not failed ;; ie return to try and load it

                if (!currentPageId.Equals(x.Id, StringComparison.InvariantCultureIgnoreCase)) return true;
                if (list.GetItemCount() < x.Index) return true;
                list.SetItemIcon(x.Index, icon);

                var tabInfo = TabInfoAccess.GetTabInfo(Global.currentTabId);
                if (!currentPageId.Equals(x.Id, StringComparison.InvariantCultureIgnoreCase)) return true;
                if (tabInfo.TabType == TabType.SIMILARITY)
                {
                    switch (Global.Settings.CurrentSortSimilarity)
                    {
                        case SortSimilarity.AVERAGE: list.SetItemText(x.Index, GetAverageSimilarityTo(tabInfo.SimilarityHash, x.Hash).ToString("0.00")); break;
                        case SortSimilarity.DIFFERENCE: list.SetItemText(x.Index, GetDifferenceSimilarityTo(tabInfo.SimilarityHash, x.Hash).ToString("0.00")); break;
                        case SortSimilarity.WAVELET: list.SetItemText(x.Index, GetWaveletSimilarityTo(tabInfo.SimilarityHash, x.Hash).ToString("0.00")); break;
                        case SortSimilarity.PERCEPTUAL: list.SetItemText(x.Index, GetPerceptualSimilarityTo(tabInfo.SimilarityHash, x.Hash).ToString("0.00")); break;
                        default: list.SetItemText(x.Index, GetAveragedSimilarityTo(tabInfo.SimilarityHash, x.Hash).ToString("0.00")); break;
                    }
                }
                return true;
            }
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
