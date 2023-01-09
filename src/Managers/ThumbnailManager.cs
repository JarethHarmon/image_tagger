using Godot;
using ImageTagger.Database;
using ImageTagger.Metadata;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ImageTagger.Managers
{
    public sealed class ThumbnailManager : Node
    {
        private static QueryInfo currentQuery;
        private int offset, limit;
        private bool countResults;
        private string thumbnailPath;

        private Dictionary<string, Godot.ImageTexture> thumbnailHistory = new Dictionary<string, ImageTexture>();
        private Queue<string> thumbnailHistoryQueue = new Queue<string>();

        private static readonly object locker = new object();

        private ItemList list;
        private ImageTexture failedIcon, bufferingIcon;

        private void Setup()
        {
            currentQuery = new QueryInfo
            {
                Order = Global.Settings.CurrentOrder,
                Sort = Global.Settings.CurrentSort,
                SortSimilarity = Global.Settings.CurrentSortSimilarity
            };
        }

        public override void _Ready()
        {
            list = GetNode<ItemList>("/root/main/margin/vbox/hsplit/left/vsplit/thumbnail_list/margin/vbox/thumbnails");

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
            _ = QueryDatabase();
        }

        public void UpdateTagsAny(string[] tagsAny)
        {
            currentQuery.TagsAny = tagsAny;
            _ = QueryDatabase();
        }

        public void UpdateTagsNone(string[] tagsNone)
        {
            currentQuery.TagsNone = tagsNone;
            _ = QueryDatabase();
        }

        public void UpdateTagsComplex(string[] tagsComplex)
        {
            var conditions = Querier.ConvertStringToComplexTags(currentQuery.TagsAll, currentQuery.TagsAny, currentQuery.TagsNone, tagsComplex);
            currentQuery.TagsAll = conditions.All;
            currentQuery.TagsAny = conditions.Any;
            currentQuery.TagsNone = conditions.None;
            currentQuery.TagsComplex = conditions.Complex;
            _ = QueryDatabase();
        }

        public void UpdateSort(int sort)
        {
            currentQuery.Sort = (Sort)sort;
            _ = QueryDatabase();
        }

        public void UpdateOrder(int order)
        {
            currentQuery.Order = (Order)order;
            _ = QueryDatabase();
        }

        public void UpdateSortSimilarity(int similarity)
        {
            currentQuery.SortSimilarity = (SortSimilarity)similarity;
            _ = QueryDatabase();
        }

        public void UpdateImportId(string tabId)
        {
            var info = TabInfoAccess.GetTabInfo(tabId);
            currentQuery.ImportId = info.ImportId;
            _ = QueryDatabase();
        }

        public void UpdatePage(int pageNumber)
        {
            offset = (pageNumber - 1) * Global.Settings.MaxImagesPerPage;
            _ = QueryDatabase();
        }

        public void QueryDatabaseGD(bool forceUpdate = false)
        {
            _ = QueryDatabase(forceUpdate);
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

        internal async Task QueryDatabase(bool forceUpdate=false)
        {
            thumbnailPath = Global.GetThumbnailPath();
            if (thumbnailPath is null) return;
            currentQuery.Query = DatabaseAccess.GetImageInfoQuery();
            currentQuery.CalcId();

            var now = DateTime.Now;
            string[] results = await Querier.QueryDatabase(currentQuery, offset, Global.Settings.MaxImagesPerPage, forceUpdate);
            Console.WriteLine((DateTime.Now - now).ToString());

            int queriedImageCount = Querier.GetLastQueriedCount(currentQuery.Id);
            int queriedPageCount = (int)Math.Ceiling((float)queriedImageCount / Global.Settings.MaxImagesPerPage);

            // update control nodes with above values

            await SetupList(results.Length, currentQuery.Id);
            await LoadThumbnails(currentQuery, results);
        }

        private async Task SetupList(int size, string id)
        {
            list.CallDeferred("clear");
            await ToSignal(GetTree(), "idle_frame");

            if (!currentQuery.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase)) return;
            for (int i = 0; i < size; i++)
            {
                list.CallDeferred("add_icon_item", bufferingIcon);
            }
            await ToSignal(GetTree(), "idle_frame");

            if (!currentQuery.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase)) return;
            // set scroll value for tab
        }

        private async Task LoadThumbnails(QueryInfo current, string[] hashes)
        {
            var tasks = new ActionBlock<ThumbnailTask>(x =>
            {
                LoadThumbnail(x);
            });

            for (int i = 0; i < hashes.Length; i++)
            {
                tasks.Post(new ThumbnailTask(current.Id, hashes[i], i));
            }

            try
            {
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
            if (await ThreadsafeSetIcon(x)) return; // try to set icon from history, return if time to stop loading, or loading succeeded

            // if loading from history failed, try to load from disk
            string path = System.IO.Path.Combine(thumbnailPath, $"{x.Hash.Substring(0, 2)}/{x.Hash}.thumb");
            try
            {
                if (!currentQuery.Id.Equals(x.Id, StringComparison.InvariantCultureIgnoreCase)) return;
                byte[] data = System.IO.File.ReadAllBytes(path);

                if (!currentQuery.Id.Equals(x.Id, StringComparison.InvariantCultureIgnoreCase)) return;
                var image = new Godot.Image();
                var err = image.LoadWebpFromBuffer(data);
                if (err != Godot.Error.Ok) await ThreadsafeSetIcon(x, true);

                if (!currentQuery.Id.Equals(x.Id, StringComparison.InvariantCultureIgnoreCase)) return;
                var texture = new Godot.ImageTexture();
                texture.CreateFromImage(image, 0);
                texture.SetMeta("image_hash", x.Hash);
                AddToHistory(x.Hash, texture); // may as well store it in history once loaded

                if (!currentQuery.Id.Equals(x.Id, StringComparison.InvariantCultureIgnoreCase)) return;
                await ThreadsafeSetIcon(x);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                await ThreadsafeSetIcon(x, true);
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

        private async Task<bool> ThreadsafeSetIcon(ThumbnailTask x, bool failed=false)
        {
            Godot.ImageTexture icon = null;
            lock (locker)
            {
                if (failed) icon = failedIcon;
                else thumbnailHistory.TryGetValue(x.Hash, out icon);
            }
            if (icon is null) return false; // not in history, not failed ;; ie return to try and load it

            if (!currentQuery.Id.Equals(x.Id, StringComparison.InvariantCultureIgnoreCase)) return true;
            list.CallDeferred("set_item_icon", x.Index, icon);
            await ToSignal(GetTree(), "idle_frame");

            var tabInfo = TabInfoAccess.GetTabInfo(Global.currentTabId);
            if (tabInfo.TabType == TabType.SIMILARITY)
            {
                switch (Global.Settings.CurrentSortSimilarity)
                {
                    case SortSimilarity.AVERAGE: list.CallDeferred("set_item_text", x.Index, GetAverageSimilarityTo(tabInfo.SimilarityHash, x.Hash).ToString("0.00")); break;
                    case SortSimilarity.DIFFERENCE: list.CallDeferred("set_item_text", x.Index, GetDifferenceSimilarityTo(tabInfo.SimilarityHash, x.Hash).ToString("0.00")); break;
                    case SortSimilarity.WAVELET: list.CallDeferred("set_item_text", x.Index, GetWaveletSimilarityTo(tabInfo.SimilarityHash, x.Hash).ToString("0.00")); break;
                    case SortSimilarity.PERCEPTUAL: list.CallDeferred("set_item_text", x.Index, GetPerceptualSimilarityTo(tabInfo.SimilarityHash, x.Hash).ToString("0.00")); break;
                    default: list.CallDeferred("set_item_text", x.Index, GetAveragedSimilarityTo(tabInfo.SimilarityHash, x.Hash).ToString("0.00")); break;
                }
            }
            return true;
        }

        /*=========================================================================================
                                                Similarity
        =========================================================================================*/
        private float GetAveragedSimilarityTo(string hash1, string hash2)
        {
            var info1 = ImageInfoAccess.GetImageInfo(hash1);
            var info2 = ImageInfoAccess.GetImageInfo(hash2);
            float simi1 = Global.CalcHammingSimilarity(info1.AverageHash, info2.AverageHash);
            float simi2 = Global.CalcHammingSimilarity(info1.DifferenceHash, info2.DifferenceHash);
            float simi3 = Global.CalcHammingSimilarity(info1.WaveletHash, info2.WaveletHash);
            float simi4 = Global.CalcHammingSimilarity(info1.PerceptualHash, info2.PerceptualHash);
            return (simi1 + simi2 + simi3 + simi4) / 4;
        }

        private float GetAverageSimilarityTo(string hash1, string hash2)
        {
            var info1 = ImageInfoAccess.GetImageInfo(hash1);
            var info2 = ImageInfoAccess.GetImageInfo(hash2);
            return Global.CalcHammingSimilarity(info1.AverageHash, info2.AverageHash);
        }

        private float GetDifferenceSimilarityTo(string hash1, string hash2)
        {
            var info1 = ImageInfoAccess.GetImageInfo(hash1);
            var info2 = ImageInfoAccess.GetImageInfo(hash2);
            return Global.CalcHammingSimilarity(info1.DifferenceHash, info2.DifferenceHash);
        }

        private float GetWaveletSimilarityTo(string hash1, string hash2)
        {
            var info1 = ImageInfoAccess.GetImageInfo(hash1);
            var info2 = ImageInfoAccess.GetImageInfo(hash2);
            return Global.CalcHammingSimilarity(info1.WaveletHash, info2.WaveletHash);
        }

        private float GetPerceptualSimilarityTo(string hash1, string hash2)
        {
            var info1 = ImageInfoAccess.GetImageInfo(hash1);
            var info2 = ImageInfoAccess.GetImageInfo(hash2);
            return Global.CalcHammingSimilarity(info1.PerceptualHash, info2.PerceptualHash);
        }
    }
}
