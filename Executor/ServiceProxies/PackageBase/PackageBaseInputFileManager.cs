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
        private class InputFileManager : IInputFileManager
        {
            private string _storageRoot;

            private List<TaskFileDescription> _filesToCopyFromStorage;
            private List<Tuple<TaskFileDescription, string>> _filesToCopyFromTmp;
            private List<string> _tmpFilesToDel;

            #region IInputFileManager Members

            public InputFileManager(string storageRoot)
            {
                _storageRoot = storageRoot;
                if (!_storageRoot.EndsWith("/"))
                    _storageRoot += "/";
                if (!_storageRoot.EndsWith("ins/"))
                    _storageRoot += "ins/";

                _filesToCopyFromStorage = new List<TaskFileDescription>();
                _filesToCopyFromTmp = new List<Tuple<TaskFileDescription, string>>();
                _tmpFilesToDel = new List<string>();
            }

            public string GetFileName(IInFileDef fileDef, ExternalFileDef extDef = null)
            {
                if (fileDef == null)
                    throw new PackageBaseException("InFileDef is null");

                string placeIn = fileDef.Path;
                if (placeIn == null)
                {
                    Log.Warn(String.Format("Path is null for file '{0}'", fileDef.Name));
                    placeIn = "";
                }

                string folder = placeIn.Trim(new[] { '/', '\\' }) + "/";
                if (folder == "/")
                    folder = "";

                string defaultName = fileDef.ExpectedName;
                string userSpecifiedName = (extDef != null)? extDef.FileName: "";
                string fileName = folder + (String.IsNullOrEmpty(defaultName)? userSpecifiedName: defaultName);

                return fileName.Replace('\\', '/');
            }

            public string GetSlotName(IInFileDef fileDef)
            {
                if (fileDef == null)
                    throw new PackageBaseException("InFileDef is null");

                return fileDef.Name;
            }

            public long InputFileCopy(IInFileDef fileDef, ExternalFileDef externalFileDefinition, DynamicContext ctx)
            {
                long fileSize = 0;

                string storageId = externalFileDefinition.Locator;
                if (!String.IsNullOrEmpty(storageId))
                {
                    _filesToCopyFromStorage.Add(new TaskFileDescription
                    {
                        FileName = GetFileName(fileDef, externalFileDefinition),
                        SlotName = GetSlotName(fileDef),
                        StorageId = storageId,
                    });

                    fileSize = IOProxy.Storage.GetFileSize(storageId); // todo : 0?
                }

                return fileSize;
            }

            public OpenFileResult InputFileOpenRead(IInFileDef fileDef, ExternalFileDef externalFileDefinition, DynamicContext ctx)
            {
                OpenFileResult result;

                string storageId = externalFileDefinition.Locator;
                if (String.IsNullOrEmpty(storageId))
                {
                    result = new OpenFileResult
                    {
                        FilePath = "", // note: or fileName
                        Stream = null,
                    };
                }
                else
                {
                    string tmpFileName = Path.GetTempFileName();
                    _tmpFilesToDel.Add(tmpFileName);

                    Log.Debug("Loading input file from storage for PB: " + storageId);
                    IOProxy.Storage.Download(storageId, tmpFileName);
                    Log.Debug("Loading done for file: " + storageId);

                    result = new OpenFileResult
                    {
                        FilePath = tmpFileName, // note: or fileName
                        Stream = File.OpenRead(tmpFileName),
                    };
                }

                return result;
            }

            public OpenFileResult InputFileOpenWrite(IInFileDef fileDef, DynamicContext ctx)
            {
                string tmpFilePath = Path.GetTempFileName();
                _tmpFilesToDel.Add(tmpFilePath);

                _filesToCopyFromTmp.Add(Tuple.Create(
                    new TaskFileDescription
                    {
                        FileName = GetFileName(fileDef),
                        SlotName = GetSlotName(fileDef)
                    },
                    tmpFilePath
                ));

                var res = new OpenFileResult()
                {
                    FilePath = tmpFilePath, // note: or fileName
                    Stream = File.OpenWrite(tmpFilePath)
                };

                return res;
            }

            public IEnumerable<TaskFileDescription> GetInputFiles()
            {
                var inputFiles = new List<TaskFileDescription>(_filesToCopyFromStorage);

                Log.Debug("Uploading input files created by PB");
                foreach (var fileFromTmp in _filesToCopyFromTmp)
                {
                    TaskFileDescription descr = fileFromTmp.Item1;
                    string tmpPath = fileFromTmp.Item2;
                    string storagePath = _storageRoot + descr.FileName;
                    descr.StorageId = IOProxy.Storage.Upload(tmpPath, storagePath);

                    inputFiles.Add(descr);
                }
                Log.Debug("Uploading input files done");

                return inputFiles;
            }

            public void Cleanup()
            {
                foreach (string tmpFileName in _tmpFilesToDel) // todo : PackageBaseFileManager.Cleanup: IDisposable & remove path from list
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

