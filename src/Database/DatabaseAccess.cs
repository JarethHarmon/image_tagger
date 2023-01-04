using ImageTagger.Core;
using ImageTagger.Metadata;
using LiteDB;
using System.Collections.Generic;

namespace ImageTagger.Database
{
    public sealed class DatabaseAccess
    {
        private static LiteDatabase dbImageInfo, dbImportInfo;
        private static ILiteCollection<ImageInfo> colImageInfo;
        private static ILiteCollection<ImportInfo> colImportInfo;
        private static ILiteCollection<TabInfo> colTabInfo;

        public static Error Create()
        {
            try
            {
                string metadataPath = Global.GetMetadataPath();

                BsonMapper.Global.Entity<ImageInfo>().Id(x => x.Hash); // still need to test if int id is better; last test was inconclusive
                dbImageInfo = new LiteDatabase(metadataPath + "image_info.db");
                dbImportInfo = new LiteDatabase(metadataPath + "import_info.db");

                colImageInfo = dbImageInfo.GetCollection<ImageInfo>("images");
                colImportInfo = dbImportInfo.GetCollection<ImportInfo>("imports");
                colTabInfo = dbImportInfo.GetCollection<TabInfo>("tabs");

                return Error.OK;
            }
            catch
            {
                // log error
                return Error.DATABASE; // maybe should switch to an int flag and return database and IO as the flags
            }
        }

        public static Error Setup()
        {
            if (colImageInfo is null) return Error.DATABASE;
            if (colImportInfo is null) return Error.DATABASE;
            if (colTabInfo is null) return Error.DATABASE;

            var imports = colImportInfo.FindAll();
            ImportInfoAccess.CreateDictionary(imports);

            var tabs = colTabInfo.FindAll();
            TabInfoAccess.CreateDictionary(tabs);

            return Error.OK;
        }

        public static void Shutdown()
        {
            dbImageInfo?.Dispose();
            dbImportInfo?.Dispose();
        }

        /* ===================================================================================
                                            ImageInfo 
        =================================================================================== */
        public static ILiteQueryable<ImageInfo> GetImageInfoQuery()
        {
            return colImageInfo?.Query();
        }

        public static ImageInfo FindImageInfo(string hash)
        {
            return colImageInfo?.FindById(hash);
        }

        /* ===================================================================================
                                            ImportInfo 
        =================================================================================== */
        public static ImportInfo FindImportInfo(string id)
        {
            return colImportInfo?.FindById(id);
        }

        public static void InsertImportInfo(ImportInfo info)
        {
            colImportInfo?.Insert(info);
        }

        public static void InsertImportInfo(IEnumerable<ImportInfo> infos)
        {
            colImportInfo?.Insert(infos);
        }

        /* ===================================================================================
                                              TabInfo 
        =================================================================================== */
        public static void InsertTabInfo(TabInfo tabInfo)
        {
            colTabInfo?.Insert(tabInfo);
        }
    }
}