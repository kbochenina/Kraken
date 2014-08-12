using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using Easis.PackageBase;
using Easis.PackageBase.Client;
using Easis.PackageBase.Engine;
using Easis.PackageBase.Definition;
using Easis.PackageBase.Types;

namespace MITP
{
    public partial class PackageBaseProxy
    {
        private class OutputFileManager : IOutputFileManager
        {
            private string _storageRoot;

            private string _ftpRoot;
            private List<Tuple<TaskFileDescription, string>> _filesToCopyFromFTP;
            private List<Tuple<TaskFileDescription, string>> _filesToCopyFromTmp;
            private List<string> _tmpFilesToDel;

            #region IOutputFileManager Members

            public OutputFileManager(string ftpRoot, string storageRoot)
            {
                _storageRoot = storageRoot;
                if (!_storageRoot.EndsWith("/"))
                    _storageRoot += "/";
                if (!_storageRoot.EndsWith("outs/"))
                    _storageRoot += "outs/";

                _ftpRoot = ftpRoot;
                if (!_ftpRoot.EndsWith("/"))
                    _ftpRoot += "/";

                _filesToCopyFromFTP = new List<Tuple<TaskFileDescription, string>>();
                _filesToCopyFromTmp = new List<Tuple<TaskFileDescription, string>>();

                _tmpFilesToDel = new List<string>();
            }

            public string GetFileName(string filePath, IOutFileDef fileDef)
            {
                if (fileDef == null)
                {
                    Log.Warn(String.Format("Output FileDef is null for file '{0}'", filePath));
                    return filePath.TrimStart(new[] { '/', '\\' });
                }

                string folder = String.IsNullOrEmpty(filePath)? "/": Path.GetDirectoryName(filePath);
                folder = fileDef.Path ?? folder;
                folder = folder.Trim(new[] { '/', '\\' }) + "/";
                if (folder == "/")
                    folder = "";

                string fileName = fileDef.ExpectedName;
                if (String.IsNullOrEmpty(fileName))
                    fileName = Path.GetFileName(filePath);

                return (folder + fileName.TrimStart(new[] { '/', '\\' })).Replace('\\', '/');
            }

            public string GetSlotName(IOutFileDef fileDef)
            {
                if (fileDef == null)
                {
                    Log.Warn("Output FileDef is null for some file. Setting slot name to 'none'");
                    return "none";
                }

                return fileDef.Name;
            }

            public long OutputFileCopy(IOutFileDef fileDef, string filePath, DynamicContext ctx)
            {
                _filesToCopyFromFTP.Add(Tuple.Create(
                    new TaskFileDescription { 
                        FileName = GetFileName(filePath, fileDef),
                        SlotName = GetSlotName(fileDef) 
                    },
                    filePath
                ));

                string ftpPath = _ftpRoot + filePath.TrimStart(new[] { '/', '\\' });
                return IOProxy.Ftp.GetFileSize(ftpPath);
            }

            public OpenFileResult OutputFileOpenRead(IOutFileDef fileDef, string filePath, DynamicContext ctx)
            {
                string ftpPath = _ftpRoot + filePath.TrimStart(new[] { '/', '\\' });
                string tmpFilePath = Path.GetTempFileName();

                Log.Debug("Downloading file for PB: " + ftpPath);
                IOProxy.Ftp.DownloadFile(ftpPath, tmpFilePath);
                Log.Debug("Downloading file done: " + ftpPath);
                _tmpFilesToDel.Add(tmpFilePath);

                var res = new OpenFileResult()
                {
                    FilePath = filePath,
                    Stream = File.OpenRead(tmpFilePath)
                };

                return res;
            }

            public OpenFileResult OutputFileOpenWrite(IOutFileDef fileDef, DynamicContext ctx)
            {
                string tmpFilePath = Path.GetTempFileName();
                _tmpFilesToDel.Add(tmpFilePath);

                string fileName = GetFileName("", fileDef);
                _filesToCopyFromTmp.Add(Tuple.Create(
                    new TaskFileDescription { 
                        FileName = fileName,
                        SlotName = GetSlotName(fileDef)
                    },
                    tmpFilePath
                ));

                var res = new OpenFileResult()
                {
                    FilePath = fileName,
                    Stream = File.OpenWrite(tmpFilePath)
                };

                return res;
            }

            public IEnumerable<TaskFileDescription> GetOutputFiles()
            {
                var outFiles = new List<TaskFileDescription>();

                Log.Debug("Moving output files to storage");

                Log.Debug("Moving FTP files");
                foreach (var fileFromFtp in _filesToCopyFromFTP)
                {
                    TaskFileDescription descr = fileFromFtp.Item1;
                    string ftpPath = _ftpRoot + fileFromFtp.Item2;
                    string storagePath = _storageRoot + descr.FileName;
                    descr.StorageId = IOProxy.Storage.Upload(ftpPath, storagePath);

                    outFiles.Add(descr);
                }

                Log.Debug("Moving local files");
                foreach (var fileFromTmp in _filesToCopyFromTmp)
                {
                    TaskFileDescription descr = fileFromTmp.Item1;
                    string tmpPath = fileFromTmp.Item2;
                    string storagePath = _storageRoot + descr.FileName;
                    descr.StorageId = IOProxy.Storage.Upload(tmpPath, storagePath);

                    outFiles.Add(descr);
                }
                
                Log.Debug("Moving output files to storage done");

                Cleanup();

                return outFiles;
            }

            private void Cleanup()
            {
                foreach (string tmpFileName in _tmpFilesToDel)
                {
                    try
                    {
                        File.Delete(tmpFileName);
                    }
                    catch (Exception e)
                    {
                        Log.Warn(String.Format(
                            "Couldn't delete tmp file '{0}': {1}\n{2}",
                            tmpFileName, e.Message, e.StackTrace
                        ));
                    }
                }
            }

            #endregion
        }
    }
}

