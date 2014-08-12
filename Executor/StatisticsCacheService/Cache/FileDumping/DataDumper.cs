using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Common;
using CommonDataTypes;

namespace StatisticsCacheService.Cache.FileDumping
{
    public class DataDumper
    {
        private static object _readlock = new object();
       
        public static Stream GetFileByName(string pathToFolder, string fileName)
        {
            Stream file = null;
            lock (_readlock)
            {
                Common.Utility.ExceptionablePlaceWrapper(() =>
                {
                    file = new FileStream(pathToFolder + "\\" + fileName, FileMode.Open, FileAccess.Read);

                }, " Failed to get file with name: '" + pathToFolder + "\\" + fileName + "'",
                    "File have been got with name: '" + pathToFolder + "\\" + fileName + "'", false);
            }

            return file;
        }

        private DateTime startTime;

        public string ResourceType { get; private set; }

        public string PathToBackUpFolder { get; private set; }

        private SortedSet<NameCacheEntry> FileNamesCache;

        private const string DUMPED_FILENAME_TEMPLATE = "{0}_{1}_{2}_{3}";

        public DataDumper(string pathToBackUpFolder, string resourceType)
        {
            PathToBackUpFolder = pathToBackUpFolder;
            ResourceType = resourceType;
            startTime = DateTime.Now;
            ConstructNameCache(PathToBackUpFolder);
        }

        public ArchiveFilesTicket GetTicketByDates(DateTime startTime, DateTime endTime)
        {
            if (endTime <= startTime)
            {
                throw new ArgumentException("startTime cannot be greater or equal startTime");
            }

            //look for crossed intervals
            var f = FileNamesCache.Where(x => !(x.startTime > endTime || x.endTime < startTime));

            var founded = f
                .Select(x => CreateFileName(x))
                .ToList();

            return new ArchiveFilesTicket() {FileNames = founded};
        }

        public void FireDump(object data, DateTime _startTime, DateTime _endTime)
        {
                var cacheEntry = new NameCacheEntry()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = ResourceType,
                        startTime = _startTime,
                        endTime = _endTime
                    };
                string name = CreateFileName(cacheEntry);

                Common.Utility.ExceptionablePlaceWrapper(() =>
                {
                    using (var fileStream = new FileStream(Path.Combine(PathToBackUpFolder, name), 
                                                          FileMode.CreateNew, FileAccess.ReadWrite))
                    {
                      CommonDataTypes.Utility.Utility.SerializeObject(fileStream, data);
                    }
                }, "" ,"Dump succeded");
            
            startTime = DateTime.Now;
            AddToNamesCache(cacheEntry);
        }

//        protected Tuple<string, DateTime, DateTime> CreateNamesCacheEntry()
//        {
//            return new Tuple<string, DateTime, DateTime>(ResourceType, startTime, DateTime.Now);
//        }

        private string CreateFileName(NameCacheEntry namesCacheEntry)
        {

            return string.Format(DUMPED_FILENAME_TEMPLATE, 
                                                namesCacheEntry.Id, 
                                                namesCacheEntry.Type, 
                                                String.Format("{0:dd/MM/yyyy HH-mm-ss}", namesCacheEntry.startTime), 
                                                String.Format("{0:dd/MM/yyyy HH-mm-ss}", namesCacheEntry.endTime));
        }

        private void AddToNamesCache(NameCacheEntry namesCacheEntry)
        {
            FileNamesCache.Add(namesCacheEntry);
        }

        private void ConstructNameCache(string path)
        {
            Common.Utility.ExceptionablePlaceWrapper(() =>
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    FileNamesCache = new SortedSet<NameCacheEntry>(new FileNamesCacheComparer());
                    return;
                }

                string[] allFiles = Directory.GetFiles(PathToBackUpFolder);

                var matchedFiles = allFiles.Select(x => Common.Utility.ReverseStringFormat(DUMPED_FILENAME_TEMPLATE, x))
                                           .Where(x => x.Count == 4)
                                           .Select(x =>
                                               {
                                                   try
                                                   {
                                                     return new NameCacheEntry()
                                                           {
                                                               Id = x[0],
                                                               Type = x[1],
                                                               startTime = DateTime.Parse(x[2].Replace("-", ":")),
                                                               endTime = DateTime.Parse(x[3].Replace("-", ":"))
                                                           };
                                                   }
                                                   catch (Exception ex){return null;}
                                               })
                                           .Where(x => x != null);

                FileNamesCache = new SortedSet<NameCacheEntry>(matchedFiles, new FileNamesCacheComparer());
            }, "NameCache construction have been failed" ,      "NameCache have been constructed succesfully" );
        }

        private class NameCacheEntry
        {
            public string Id { get; set; }
            public string Type { get; set; }
            public DateTime startTime { get; set; }
            public DateTime endTime { get; set; }
        }

        private class FileNamesCacheComparer : IComparer<NameCacheEntry>
        {
            public int Compare(NameCacheEntry x, NameCacheEntry y)
            {
                //sorted by 
                return x.startTime.CompareTo(y.startTime);
            }
        }
    }
}
