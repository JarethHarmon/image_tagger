using Godot;
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Alphaleonis.Win32.Filesystem;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using LiteDB;
using Data;

public class DatabaseTemp : Node
{
/*==============================================================================*/
/*                                   Variables                                  */
/*==============================================================================*/
    private int progressSectionSize = 128;
    private bool useJournal = false;
    private string metadataPath;
    public void SetMetadataPath(string path) { metadataPath = path; }

    /* may eventually reduce and merge these (especially merging dbGroups with dbTags) */
    private LiteDatabase dbHashes, dbImports, dbGroups, dbTags;
    private ILiteCollection<HashInfo> colHashes;
    private ILiteCollection<ImportInfo> colImports;
    private ILiteCollection<ImportProgress> colProgress;
    private ILiteCollection<GroupInfo> colGroups;
    private ILiteCollection<TagInfo> colTags;
    private ILiteCollection<TabInfo> colTabs;

    /* for thread safety, it might be better to make all of these into concurrent dictionaries */
    private Dictionary<string, HashInfo> dictHashes = new Dictionary<string, HashInfo>();
    private ConcurrentDictionary<string, ImportInfo> dictImports = new ConcurrentDictionary<string, ImportInfo>();
    private Dictionary<string, GroupInfo> dictGroups = new Dictionary<string, GroupInfo>();
    private Dictionary<string, TagInfo> dictTags = new Dictionary<string, TagInfo>();
    private Dictionary<string, TabInfo> dictTabs = new Dictionary<string, TabInfo>();

    private ImageScanner scanner;
    private ImageImporter importer;
    private Node globals;

/*==============================================================================*/
/*                                 Initialization                               */
/*==============================================================================*/
    public override void _Ready()
    {
        scanner = (ImageScanner) GetNode("/root/ImageScanner");
        importer = (ImageImporter) GetNode("/root/ImageImporter");
        globals = (Node) GetNode("/root/Globals");
    }

    public int Create()
    {
        try {
            if (useJournal) {
                dbHashes = new LiteDatabase(metadataPath + "hash_info.db");
                dbImports = new LiteDatabase(metadataPath + "import_info.db");
                dbGroups = new LiteDatabase(metadataPath + "group_info.db");
                dbTags = new LiteDatabase(metadataPath + "tag_info.db");
            } else {
                dbHashes = new LiteDatabase(metadataPath + "hash_info.db; journal=false");
                dbImports = new LiteDatabase(metadataPath + "import_info.db; journal=false");
                dbGroups = new LiteDatabase(metadataPath + "group_info.db; journal=false");
                dbTags = new LiteDatabase(metadataPath + "tag_info.db; journal=false");
            }

            BsonMapper.Global.Entity<HashInfo>().Id(x => x.imageHash);
            BsonMapper.Global.Entity<ImportInfo>().Id(x => x.importId);
            BsonMapper.Global.Entity<ImportProgress>().Id(x => x.progressId);
            BsonMapper.Global.Entity<GroupInfo>().Id(x => x.groupId);
            BsonMapper.Global.Entity<TagInfo>().Id(x => x.tagId);
            BsonMapper.Global.Entity<TabInfo>().Id(x => x.tabId);

            colHashes = dbHashes.GetCollection<HashInfo>("hashes");
            colImports = dbImports.GetCollection<ImportInfo>("imports");
            colProgress = dbImports.GetCollection<ImportProgress>("progress");
            colGroups = dbGroups.GetCollection<GroupInfo>("groups");
            colTags = dbTags.GetCollection<TagInfo>("tags");
            colTabs = dbImports.GetCollection<TabInfo>("tabs");

            return (int)ErrorCodes.OK;
        } 
        catch { return (int)ErrorCodes.ERROR; }
    } 
    
    public void Destroy()
    {
        dbHashes.Dispose();
        dbImports.Dispose();
        dbGroups.Dispose();
        dbTags.Dispose();
    }

    public void CheckpointHashDB() { dbHashes.Checkpoint(); }
    public void CheckpointImportDB() { dbImports.Checkpoint(); }
    public void CheckpointGroupDB() { dbGroups.Checkpoint(); }
    public void CheckpointTagDB() { dbTags.Checkpoint(); }

/*==============================================================================*/
/*                                  Hash Database                               */
/*==============================================================================*/

/*==============================================================================*/
/*                                 Import Database                              */
/*==============================================================================*/
    public void CreateImport(string _id, string _name, int _total, (string[], long[], long[], long[]) images)
    {
        try {
            if (_total == 0) return;
            var importInfo = new ImportInfoN {
                importId = _id,
                importName = _name,
                total = _total,
                processed = 0,
                success = 0,
                ignored = 0,
                duplicate = 0,
                failed = 0,
                importStart = 0,
                importFinish = 0,
                finished = false,
                progressIds = new List<string>(),
            };

            int numSections = (int)Math.Ceiling((double)_total/progressSectionSize);
            int lastSectionSize = _total-((numSections-1) * progressSectionSize);
            (string[] _paths, long[] _sizes, long[] _creationTimes, long[] _lastEditTimes) = images;
            var listProgress = new List<ImportProgress>();

            for (int i = 0; i < numSections-1; i++) {
                string[] __paths = new string[progressSectionSize];
                long[] __sizes = new long[progressSectionSize];
                long[] __creation = new long[progressSectionSize];
                long[] __lastEdit = new long[progressSectionSize];

                Array.Copy(_paths, i * progressSectionSize, __paths, 0, progressSectionSize);
                Array.Copy(_sizes, i * progressSectionSize, __sizes, 0, progressSectionSize);
                Array.Copy(_creationTimes, i * progressSectionSize, __creation, 0, progressSectionSize);
                Array.Copy(_lastEditTimes, i * progressSectionSize, __lastEdit, 0, progressSectionSize);
                
                var _progressId = importer.CreateProgressID();
                var importProgress = new ImportProgress {
                    progressId = _progressId,
                    paths = __paths,
                    sizes = __sizes,
                    creationTimes = __creation,
                    lastWriteTimes = __lastEdit,
                };
                listProgress.Add(importProgress);
                importInfo.progressIds.Add(_progressId);
            }
            if (lastSectionSize > 0) {
                string[] __paths = new string[lastSectionSize];
                long[] __sizes = new long[lastSectionSize];
                long[] __creation = new long[lastSectionSize];
                long[] __lastEdit = new long[lastSectionSize];

                Array.Copy(_paths, _total-lastSectionSize, __paths, 0, lastSectionSize);
                Array.Copy(_sizes, _total-lastSectionSize, __sizes, 0, lastSectionSize);
                Array.Copy(_creationTimes, _total-lastSectionSize, __creation, 0, lastSectionSize);
                Array.Copy(_lastEditTimes, _total-lastSectionSize, __lastEdit, 0, lastSectionSize);

                var _progressId = importer.CreateProgressID();
                var importProgress = new ImportProgress {
                    progressId = _progressId,
                    paths = __paths,
                    sizes = __sizes,
                    creationTimes = __creation,
                    lastWriteTimes = __lastEdit,
                };
                listProgress.Add(importProgress);
                importInfo.progressIds.Add(_progressId);
            }
            
            dbImports.BeginTrans();
            // uncomment once ImportInfo is fully replaced with current ImportInfoN
            //colImports.Insert(importInfo);
            foreach (ImportProgress imp in listProgress)
                colProgress.Insert(imp);
            dbImports.Commit();
        } 
        catch (Exception ex) { GD.Print("Database::CreateImport() : ", ex); return; }
    }


}
