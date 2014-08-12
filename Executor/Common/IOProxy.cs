using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Net;
using System.IO;

using Config = System.Web.Configuration.WebConfigurationManager;

namespace MITP
{
    public abstract class IOProxy
    {
        public abstract class Ftp
        {
            public static void MakePath(string ftpPath)
            {
                Log.Debug(String.Format(
                    "Trying to make path: {0}",
                    ftpPath
                ));

                string[] folders = ftpPath
                    .Replace("\\", "/")
                    .Replace("ftp://", "")
                    .Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string currentFolder = "ftp://";

                for (int i = 0; i < folders.Length - 1; i++)
                {
                    currentFolder += folders[i] + "/";

                    //Log.Debug(String.Format(
                    //    "Trying to make folder {1} in {0}",
                    //    currentFolder, folders[i+1]
                    //));

                    try
                    {
                        MakeFolder(currentFolder, folders[i + 1]);
                    }
                    catch //(Exception e)// todo : do not silently catch errors
                    {
                        // Folder already exists
                        //Log.Warn(String.Format(
                        //    "FTP exception while making path: {0}\n{1}",
                        //    e.Message, e.StackTrace
                        //));
                    }
                }
            }

            private static void MakeFolder(string ftpFolder)
            {
                var req = (FtpWebRequest)FtpWebRequest.Create(ftpFolder);
                req.Method = WebRequestMethods.Ftp.MakeDirectory;
                //req.KeepAlive = false;
                //req.UseBinary = true;
                //req.UsePassive = true;
                //req.ServicePoint.

                using (WebResponse resp = req.GetResponse())
                {
                    //((FtpWebResponse) resp).StatusCode == FtpStatusCode.
                };
            }

            private static void MakeFolder(string ftpFolder, string newFolderName)
            {
                if (!ftpFolder.EndsWith("/"))
                    ftpFolder += "/";

                MakeFolder(ftpFolder + newFolderName);
            }

            private static bool IsAFolder(string ftpPath)
            {
                bool result;

                try
                {
                    WebRequest req = FtpWebRequest.Create(ftpPath);
                    req.Method = WebRequestMethods.Ftp.GetFileSize;

                    using (WebResponse resp = req.GetResponse())
                    using (Stream respStream = resp.GetResponseStream())
                    using (StreamReader reader = new StreamReader(respStream))
                    {
                        string dirList = reader.ReadToEnd();
                        result = false;
                    }
                }
                catch
                {
                    result = true;
                }

                return result;
            }

            public static List<string> GetFileNamesInAllTree(string ftpFolder)
            {
                var files = new List<string>();
                string[] fileAndFolderNames;

                try
                {
                    WebRequest req = FtpWebRequest.Create(ftpFolder);
                    req.Method = WebRequestMethods.Ftp.ListDirectory;

                    using (WebResponse resp = req.GetResponse())
                    using (Stream respStream = resp.GetResponseStream())
                    using (StreamReader reader = new StreamReader(respStream))
                    {
                        string dirList = reader.ReadToEnd();
                        fileAndFolderNames = dirList.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string fullFileOrfolderName in fileAndFolderNames)
                        {
                            string fileOrfolderName = Path.GetFileName(fullFileOrfolderName);
                            string fileOrFolderFtpPath = ftpFolder + "/" + fileOrfolderName;

                            if (IsAFolder(fileOrFolderFtpPath))
                                files.AddRange(GetFileNamesInAllTree(fileOrFolderFtpPath));
                            else
                                files.Add(fileOrFolderFtpPath);
                        }
                    }
                }
                catch // ftpFolder is a file
                {
                }

                return files;
            }

            private static string[] GetFileNames(string ftpFolder)
            {
                const int MAX_ATTEMPTS = 3;

                string[] fileNames = null;
                string dirList = "";

                bool dirListSucceeded = false;
                Exception lastException = null;
                for (int attemptNum = 0; !dirListSucceeded && attemptNum < MAX_ATTEMPTS; attemptNum++)
                {
                    try
                    {
                        WebRequest req = FtpWebRequest.Create(ftpFolder);
                        req.Method = WebRequestMethods.Ftp.ListDirectory;

                        using (WebResponse resp = req.GetResponse())
                        using (Stream respStream = resp.GetResponseStream())
                        using (StreamReader reader = new StreamReader(respStream))
                        {
                            dirList = reader.ReadToEnd();
                            dirListSucceeded = true;
                        }
                    }
                    catch (Exception e)
                    {
                        lastException = e;
                    }
                }

                if (dirListSucceeded)
                {
                    fileNames = dirList.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                }
                else
                {
                    Log.Error(String.Format("Возникло исключение при попытке получить список файлов FTP папки <{0}> \n{1}\n{2}",
                        ftpFolder, lastException.Message, lastException.StackTrace
                    ));
                }

                return fileNames;
            }

            public static long GetFileSize(string ftpPath)
            {
                return 0; // todo : Ftp.GetFileSize

                WebRequest req = FtpWebRequest.Create(ftpPath);
                //req.Method = WebRequestMethods.Ftp.GetFileSize;
                req.Method = WebRequestMethods.Ftp.DownloadFile;

                using (WebResponse resp = req.GetResponse())
                {
                    return resp.ContentLength;
                }
            }

            public static void DownloadFile(string ftpPath, string localPath)
            {
                var req = (FtpWebRequest) FtpWebRequest.Create(ftpPath);
                req.Method = WebRequestMethods.Ftp.DownloadFile;
                req.UseBinary = true;

                using (WebResponse resp = req.GetResponse())
                using (Stream respStream = resp.GetResponseStream())
                using (var localStream = File.Create(localPath))
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = 0;

                    while ((bytesRead = respStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        localStream.Write(buffer, 0, bytesRead);
                    }
                }
            }

            private static string DownloadFileContent(string ftpFolder, string fileName)
            {
                string content = "";

                if (!ftpFolder.EndsWith("/"))
                    ftpFolder += "/";

                WebRequest req = FtpWebRequest.Create(ftpFolder + fileName);
                req.Method = WebRequestMethods.Ftp.DownloadFile;

                using (WebResponse resp = req.GetResponse())
                using (Stream reqStream = resp.GetResponseStream())
                using (var reader = new StreamReader(reqStream))
                {
                    content = reader.ReadToEnd();
                }

                return content;
            }

            public static void UploadFileContent(string content, string ftpFolder, string fileName)
            {
                if (!ftpFolder.EndsWith("/"))
                    ftpFolder += "/";

                var req = (FtpWebRequest) FtpWebRequest.Create(ftpFolder + fileName);
                //req.KeepAlive = false;
                req.Method = WebRequestMethods.Ftp.UploadFile;

                using (Stream reqStream = req.GetRequestStream())
                using (var writer = new StreamWriter(reqStream))
                {
                    writer.Write(content);
                }

                using (WebResponse resp = req.GetResponse()) { };
            }

            public static void UploadFileContent(byte[] bytes, string ftpFolder, string fileName)
            {
                if (!ftpFolder.EndsWith("/"))
                    ftpFolder += "/";

                var req = (FtpWebRequest) FtpWebRequest.Create(ftpFolder + fileName);
                req.Method = WebRequestMethods.Ftp.UploadFile;
                req.UseBinary = true;

                using (Stream reqStream = req.GetRequestStream())
                using (var writer = new BinaryWriter(reqStream))
                {
                    writer.Write(bytes);
                }

                using (WebResponse resp = req.GetResponse()) { };
            }

            public static void UploadLocalFile(string localFilePath, string ftpFolder, string fileName, bool shouldCreatePath = false)
            {
                Log.Debug("Uploading local file to ftp: " + localFilePath + " -> " + ftpFolder + "/" + fileName);
                if (shouldCreatePath)
                    MakePath(ftpFolder);
                Log.Debug("Path has been made: " + ftpFolder);

                byte[] bytes = File.ReadAllBytes(localFilePath);
                UploadFileContent(bytes, ftpFolder, fileName);
                Log.Debug("File uploaded: " + fileName);
            }
        }

        public abstract class Storage
        {
            private const int STORAGE_POOL_SIZE = 50;
            private static object _storageLock = new object();
            private static object[] _storageLockPool = new object[STORAGE_POOL_SIZE];
            private static object GetStorageLock()
            {
                int poolIndex = System.Threading.Thread.CurrentThread.ManagedThreadId % STORAGE_POOL_SIZE;

                lock (_storageLock)
                {
                    if (_storageLockPool[poolIndex] == null)
                    {
                        Log.Debug("Creating storage lock for index = " + poolIndex.ToString());
                        _storageLockPool[poolIndex] = new object();
                    }
                    return _storageLockPool[poolIndex];
                }
            }

            [ThreadStatic]
            private static NetLibrary.Storage _storageInstance;
            private static NetLibrary.Storage _storage
            {
                get
                {
                    if (_storageInstance == null)
                        _storageInstance = new NetLibrary.Storage(
                            Config.AppSettings["Storage"],
                            debugMode: true,
                            secId: Config.AppSettings["StorageToken"]
                        );

                    return _storageInstance;
                }
            }

            public static string BuildPath(string userId, string wfId, string stepId, string stepName = null)
            {
                if (String.IsNullOrEmpty(stepName))
                    stepName = stepId;
                return String.Format(@"/home/{0}/runs/{1}/{2}/", userId, wfId, stepName);
            }

            //public static string GetNewInputId() //(string id = null)
            //{
            //    _storage.

            //    var service = EntryPointProxy.GetIOService();

            //    var req = new IOService.postDataRequest();
            //    req.DataID = null;//id;
            //    //req.CalcPackage = null;

            //    var resp = service.postData(req);
            //    return resp.DataId;
            //}

            private static string SendToStorageCore(string method, Dictionary<string, string> args)
            {
                string coreUrl = Config.AppSettings["Storage"];
                string coreToken = Config.AppSettings["StorageToken"];

                using (var webClient = new WebClient())
                {
                    webClient.BaseAddress = coreUrl;
                    webClient.Encoding = Encoding.UTF8; //.GetEncoding(1251);
                    webClient.Headers.Add("Content-Type", "application/json");

                    args["secid"] = coreToken;

                    string message = "{" + String.Join(", ", args.Select(pair => String.Format(@"""{0}"": ""{1}""", pair.Key, pair.Value))) + "}";
                    Log.Debug(String.Format("Sending to storage: {0} {1}", method, message));
                    
                    string response = webClient.UploadString("", method, message);
                    Log.Debug("Storage response: " + response);

                    return response;
                }
            }

            private static bool DirExists(string pathInStorage)
            {
                var _lock = GetStorageLock();
                lock (_lock)
                {
                    string response = SendToStorageCore("PUT", new Dictionary<string, string>()
                    {
                        {"action", "get_metadata"},
                        {"name", pathInStorage}
                    });

                    if (response.ToLowerInvariant().Replace(" ", "").Contains(@"""status"":""error"""))
                        return false;
                    return true;
                }
            }

            private static void MakeUpperDirs(string pathInStorage)
            {
                int MAX_RETIRES = 3;

                var _lock = GetStorageLock();
                lock (_lock)
                {
                    bool success = false;

                    for (int retries = 0; !success && retries < MAX_RETIRES; retries++)
                    {
                        try
                        {
                            Log.Debug("Making dirs: " + pathInStorage);
                            string dirPath = Path.GetDirectoryName(pathInStorage).Replace('\\', '/');

                            /*
                            //if (!DirExists(dirPath)) // no need for this check, only wastes time on slow Storage
                            {
                                string response = SendToStorageCore("PUT", new Dictionary<string, string>()
                                {
                                    {"action", "put"},
                                    {"name", dirPath},
                                    {"type", "d"}
                                });
                            }
                            */
                            /**/
                            //if (!_storage.IsExist(dirPath))
                            {
                                try
                                {
                                    _storage.MakeDirectory(dirPath);
                                }
                                catch (NetLibrary.StorageException e)
                                {
                                    Log.Warn("Exception while making dirs in storage: " + e.ToString());
                                }
                            }
                            /**/

                            success = true;
                        }
                        catch (Exception e)
                        {
                            if (retries == MAX_RETIRES - 1)
                                throw;

                            Log.Warn("Exception on making dirs in storage: " + e.ToString());
                            System.Threading.Thread.Sleep(200);
                        }
                    }

                    Log.Debug("Dirs have been made");
                }
            }

            public static string Upload(Stream stream, string pathInStorage)
            {
                //var _lock = GetStorageLock();
                //lock (_lock)
                {
                    MakeUpperDirs(pathInStorage);

                    string id = _storage.UploadFileAsStream(stream, pathInStorage);
                    return id;
                }
            }

            public static string Upload(string fileUrl, string pathInStorage)
            {
                //var _lock = GetStorageLock();
                //lock (_lock)
                {
                    Log.Debug("Upload file started: " + fileUrl + " -> " + pathInStorage);
                    MakeUpperDirs(pathInStorage);

                    if (fileUrl.StartsWith(@"\\")) // file on share
                    {
                        var stream = File.OpenRead(fileUrl);
                        string id = Upload(stream, pathInStorage);
                        Log.Debug(String.Format("Upload finished. Path = '{0}', id = '{1}'", pathInStorage, id));
                        return id;
                    }

                    if (!fileUrl.Contains(@"://")) // not on share, no protocol => local file
                    {
                        string id = _storage.UploadFile(fileUrl, pathInStorage);
                        Log.Debug(String.Format("Upload finished. Path = '{0}', id = '{1}'", pathInStorage, id));
                        return id;
                    }

                    if (fileUrl.ToLowerInvariant().StartsWith("ftp://"))
                    {
                        string id = null;
                        string tmpFile = Path.GetTempFileName();
                        try
                        {
                            IOProxy.Ftp.DownloadFile(fileUrl, tmpFile);
                            Log.Debug("Downloaded from ftp: " + fileUrl);
                            id = _storage.UploadFile(tmpFile, pathInStorage);
                        }
                        finally
                        {
                            Log.Debug("Deleting local tmp file");
                            File.Delete(tmpFile);
                        }
                        Log.Debug(String.Format("Upload finished. Path = '{0}', id = '{1}'", pathInStorage, id));
                        return id;
                    }

                    throw new Exception(String.Format(
                        "Specified file path type ('{0}') is not supported for uploading in storage",
                        fileUrl
                    ));
                }
            }

            public static void Download(string storageId, string fileUrl)
            {
                //var _lock = GetStorageLock();
                //lock (_lock)
                {
                    Log.Debug("Downloading file: " + storageId + " -> " + fileUrl);

                    if (fileUrl.StartsWith(@"\\")) // file on share
                    {
                        string tmpFile = Path.GetTempFileName();
                        try
                        {
                            _storage.DownloadFileById(storageId, tmpFile);
                            string parentDir = Path.GetDirectoryName(fileUrl);
                            Directory.CreateDirectory(parentDir);
                            File.Copy(tmpFile, fileUrl, overwrite: true);
                        }
                        finally
                        {
                            File.Delete(tmpFile);
                            Log.Debug("Dowloaded " + fileUrl);
                        }
                    }
                    else
                    if (!fileUrl.Contains(@"://")) // not on share, no protocol => local file
                    {
                        string parentDir = Path.GetDirectoryName(fileUrl);
                        Directory.CreateDirectory(parentDir);
                        _storage.DownloadFileById(storageId, fileUrl);
                        Log.Debug("Dowloaded " + fileUrl);
                    }
                    else
                    if (fileUrl.ToLowerInvariant().StartsWith("ftp://"))
                    {
                        int delimPos = fileUrl.LastIndexOf('/');
                        string ftpFolder = fileUrl.Substring(0, delimPos + 1);
                        string ftpFileName = fileUrl.Substring(delimPos + 1);

                        string tmpFile = Path.GetTempFileName();
                        try
                        {
                            _storage.DownloadFileById(storageId, tmpFile);
                            Ftp.UploadLocalFile(tmpFile, ftpFolder, ftpFileName, shouldCreatePath: true);
                        }
                        finally
                        {
                            File.Delete(tmpFile);
                            Log.Debug("Dowloaded " + fileUrl);
                        }
                    }
                    else
                        throw new Exception("Specified file path type is not supported for uploading in storage");
                }
            }

            //public static byte[] DownloadBytes(string storageId)
            //{
            //    using (var stream = _storage.DownloadFileAsStreamById(storageId))
            //    using (var reader = new System.IO.BinaryReader(stream))
            //    {
            //        reader.ReadBytes(
            //    }



            //    return bytes;
            //}

            public static long GetFileSize(string storageId)
            {
                //var _lock = GetStorageLock();
                //lock (_lock)
                {
                    return 0;
                    var fileInfo = _storage.GetFileInformationById(storageId);
                    return fileInfo.Size;
                }
            }

            //public static string GetContent(string inputId)
            //{
            //    string content = null;

            //    var service = EntryPointProxy.GetIOService();
            //    string url = service.Endpoint.Address.Uri.ToString().Replace("DataService", "HttpService")
            //        + "?data_id=" + inputId;

            //    WebRequest req = HttpWebRequest.Create(url);
            //    WebResponse resp = req.GetResponse();
            //    Stream respStream = resp.GetResponseStream();
            //    var reader = new StreamReader(respStream);

            //    try
            //    {
            //        content = reader.ReadToEnd();
            //    }
            //    finally
            //    {
            //        reader.Close();
            //        respStream.Close();
            //        resp.Close();
            //    }

            //    return content;
            //}
        }

    }
}
