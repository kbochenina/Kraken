using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommonDataTypes.Utility;
using ControllerFarmService.Installation.Interfaces;
using U = Common.Utility;
using IU = ControllerFarmService.Installation.InstallationUtility;

namespace ControllerFarmService.Installation.Impl
{
    class PackageCacherImpl : IPackageCacher, IStartStopable
    {

        protected class StaffBundle
        {
            private bool isDownloaded = false;
            private int revNumber = -1;

            public event EventHandler PackageDownloaded;
            public string LocalAddress { get; set; }
            public int RevisionNumber {
                get { return revNumber; }
                set { revNumber = value; } 
            }

            public bool IsDownloaded
            {
                get{return isDownloaded;}
            }

            public DateTime LastAccessTime{get; private set;}

            public void SetDownloaded()
            {
                isDownloaded = true;
                OnPackageDownloaded();
            }

            public void UpdateLastAccessTime()
            {
                LastAccessTime = DateTime.Now;
            }

            public DateTime GetLastUsingTime()
            {
                var dt =  File.GetLastWriteTime(LocalAddress);
                return dt.Ticks > LastAccessTime.Ticks ? dt : LastAccessTime;
            }
            private void OnPackageDownloaded()
            {
                if (PackageDownloaded != null)
                {
                    PackageDownloaded(this,new EventArgs());
                }
            }


        }

        private int clearInterval;
        private int lifeInterval;
        private string tempFileDirectory;
        private Dictionary<string/*key*/,StaffBundle> cacheContent = new Dictionary<string, StaffBundle>();
        private RepeatedTaskHolder daemonHolder;
        private object _lock = new object();


        public PackageCacherImpl(int clearInterval, int lifeInterval, string tempFileDirectory)
        {
            this.clearInterval = clearInterval;
            this.lifeInterval = lifeInterval;
            this.tempFileDirectory = tempFileDirectory;
        }

        public string ReserveAddress(string key)
        {
            lock (_lock)
            {
                string localAddress = null;

                    if (cacheContent.ContainsKey(key))
                    {
                        throw new InvalidDataException(U.Message(IU.NOT_ALLOWED_KEYS_DUPLICATION,
                            U.Arg("Key", key)));
                    }
                    localAddress = GetLocalAddress(key);
                    var bundle = new StaffBundle()
                    {
                        LocalAddress = localAddress,
                    };
                    cacheContent.Add(key, bundle); 
    
                U.LogDebug(U.Message(IU.LOCAL_ADDRESS_RESERVED,U.Arg("LocalAddress",localAddress)));
                
                return localAddress;
            }
        }

        public void RegisterDownloaded(string key, int revisionNumber)
        {
            lock (_lock)
            {
                    cacheContent[key].RevisionNumber = revisionNumber;
                    cacheContent[key].UpdateLastAccessTime();
                    cacheContent[key].SetDownloaded();

                    U.LogDebug(U.Message(IU.ADDRESS_CONFIRMED, U.Arg("Key", key), 
                                                               U.Arg("RevNumber", revisionNumber)));
            }
        }

        public void Remove(string key)
        {
            lock (_lock)
            {
                if (cacheContent.ContainsKey(key))
                {
                    cacheContent.Remove(key);

                    U.LogDebug(U.Message(IU.ENTRY_REMOVED, U.Arg("Key", key)));
                }
            }
        }

        public void Get(string key, Action<PackageCacherResponse> response)
        {
            lock (_lock)
            {
                    if (cacheContent.ContainsKey(key))
                    {
                        var bundle = cacheContent[key];
                        bundle.UpdateLastAccessTime();
                        if (bundle.IsDownloaded)
                        {
                            U.LogDebug(U.Message(IU.FILE_FOUND_BY_KEY,U.Arg("Key",key)));
                            response(new PackageCacherResponse(key, bundle.LocalAddress, null, bundle.RevisionNumber));
                        }
                        else
                        {
                            U.LogDebug(U.Message(IU.FILE_IS_DOWNLOADING,U.Arg("Key",key)));
                            bundle.PackageDownloaded +=
                                (sender, args) => { response(new PackageCacherResponse(key, bundle.LocalAddress, null, bundle.RevisionNumber)); };
                        }
                        return;
                    }
            }
            
            U.LogDebug(U.Message(IU.KEY_NOT_FOUND, U.Arg("Key", key)));

            response(new PackageCacherResponse(key,null,null,-1));
        }

        private string GetLocalAddress(string key)
        {           
            string name = null;
            int i = 0;
            do
            {
                name = Path.Combine(this.tempFileDirectory, Path.GetFileName(key.ToString()) + (i == 0 ? "" : "-" + i) + ".tmp");
                ++i;
            } while (File.Exists(name));

            return name;
        }

        public void Start()
        {
            if (daemonHolder != null)
            {
                daemonHolder.Stop();
            }

            daemonHolder = CommonDataTypes.Utility.Utility.CreateAndRunRepeatedProcess(this.clearInterval, false, Clean);
        
            U.LogInfo(U.Message(IU.BACKGROUND_TEMP_CLEANER_STARTED));
        }

        public void Stop()
        {
            if (daemonHolder != null)
            {
                daemonHolder.Stop();
            }
            daemonHolder = null;

            U.LogInfo(U.Message(IU.BACKGROUND_TEMP_CLEANER_STOPPED));
        }

        public void Clean()
        {
            IEnumerable<string> tempFiles = null;

            Common.Utility.ExceptionablePlaceWrapper(() =>
            {
                lock (_lock)
                {
                    var now = DateTime.Now;
                    var life = new TimeSpan(0,0,0,0,this.lifeInterval);

                    cacheContent = cacheContent.Where(
                        x => !x.Value.IsDownloaded || now.Subtract((DateTime) x.Value.GetLastUsingTime()).Ticks < life.Ticks)
                        .ToDictionary(x => x.Key, x => x.Value);

                    //also remove files survived by some reason from previous iteration
                    //By some reason it means locking of file by reading or writing
                    tempFiles = Directory.EnumerateFiles(this.tempFileDirectory)
                        .Where(x => x.EndsWith(".tmp")).Except(cacheContent.Values.Select(x => x.LocalAddress));

                }
            }, "", "", false);

           ;

            Parallel.ForEach(tempFiles, (file) =>
            {
                Common.Utility.ExceptionablePlaceWrapper(() =>
                {
                    //if file is locked, it won't be deleted.
                    File.Delete(file);

                },
                 U.Message(InstallationUtility.FILE_NOT_DELETED, U.Arg("FileName",file)),
                 U.Message(InstallationUtility.FILE_DELETED, U.Arg("FileName", file)), false);
            });
        }

        

    }
}
