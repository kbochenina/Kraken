using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.IO;
using System.Net;

using ClustersService = ServiceProxies.ClustersService;

namespace MITP
{
    public abstract class CommonPackageAdapter : AbstractPackageAdapter
	{
        public abstract IEnumerable<string> GetDefaultInputFileNames();
        public abstract void SetIncarnationParams(Task task);
        public abstract void UploadAndPrepareInputFiles(Task task, string ftpFolder);

        protected void CheckInputFileNames(Task task)
        {
            var defaultFileNames = GetDefaultInputFileNames();
            if (defaultFileNames == null || !defaultFileNames.Any())
            {
                Log.Warn(String.Format("Adapter {0} hasn't specified default file names. Using slot names", this.ToString()));
                defaultFileNames = task.InputFiles.Select(file => String.IsNullOrEmpty(file.SlotName)? "input" : file.SlotName);

            }
            string defaultExtension = Path.GetExtension(defaultFileNames.First());

            
            if (task.InputFiles.Length == 1 && String.IsNullOrEmpty(task.InputFiles[0].FileName) && defaultFileNames.Count() == 1)
            {
                // don't care about slot names when 1 input & 1 default name
                task.InputFiles[0].FileName = defaultFileNames.First();
                Log.Warn(String.Format(
                    "Input file name is not specified. Using default name \"{0}\".",
                    task.InputFiles[0].FileName
                ));
            }
            else
            {
                bool warned = false;
                for (int i=0; i<task.InputFiles.Length; i++)
                {
                    if (String.IsNullOrEmpty(task.InputFiles[i].FileName))
                    {
                        if (!warned)
                        {
                            Log.Warn("Input file names are not specified. Using autodetect based on slot names.");
                            warned = true;
                        }

                        Func<string, bool> containsSlotName = (string st) =>
                        {
                            return st.ToUpperInvariant().Contains(task.InputFiles[i].SlotName.ToUpperInvariant());
                        };

                        if (defaultFileNames.Any(containsSlotName))
                            task.InputFiles[i].FileName = defaultFileNames.First(containsSlotName);
                        else
                        {
                            task.InputFiles[i].FileName = task.InputFiles[i].SlotName + defaultExtension;

                            Log.Warn(String.Format(
                                "Unknown slot name \"{0}\" for empty file name. Using \"{1}\" as a file name.",
                                task.InputFiles[i].SlotName,
                                task.InputFiles[i].FileName
                            ));
                        }
                    }
                }
            }

            Log.Debug("Input file names: " + String.Join(", ", task.InputFiles.Select(inp => inp.FileName)));
        }


		protected void UploadManualFiles(string localFolder, string ftpFolder) // todo : move UploadManualFiles() from Adapter to IOProxy
		{
			string[] fullFileNames = Directory.GetFiles(localFolder);
			foreach (string fullFileName in fullFileNames)
				IOProxy.Ftp.UploadLocalFile(fullFileName, ftpFolder, Path.GetFileName(fullFileName));

			string[] fullSubfolderPaths = Directory.GetDirectories(localFolder);
			foreach (string fullSubfolderPath in fullSubfolderPaths)
			{
				string subfolderName = Path.GetFileName(fullSubfolderPath);

				IOProxy.Ftp.MakeFolder(ftpFolder, subfolderName);
				UploadManualFiles(fullSubfolderPath, ftpFolder + subfolderName + "/");
			}
		}

        public override void OnManualStart(Task task, string ftpFolder)
		{
            CheckInputFileNames(task);

			if (Directory.Exists(CONST.Path.ManualTemplatesFolder + task.Package.ToString()))
                UploadManualFiles(CONST.Path.ManualTemplatesFolder + task.Package.ToString(), ftpFolder);

			foreach (var inputFile in task.InputFiles)
			{
                IOProxy.Storage.MoveOutside(inputFile.StorageId, ftpFolder + inputFile.FileName);
				//Log.Debug(String.Format("*** {0} {1} ***", inputFile.FileName, ftpInputFolder + inputFile.FileName));
			}

            SetIncarnationParams(task);
		}

        public override void OnStart(Task task, string ftpFolder)
		{
            CheckInputFileNames(task);
            UploadAndPrepareInputFiles(task, ftpFolder);
            SetIncarnationParams(task);
		}

        public override void OnFinish(Task task, string ftpFolder)
		{
            DateTime timeBefore = DateTime.Now;

            var forbiddenFileNamesRx = (new string[] { 
                @"\.\./", @"torque", @"\.dataList\.src$", @"\.hosts$", @"\.sh$" 
            }).Select(st => new Regex(st, RegexOptions.IgnoreCase));

			switch (task.LaunchMode)
			{
				case TaskLaunchMode.Auto:
                    if (task.OutputFiles == null || task.OutputFiles.Length == 0)
                    {
                        Log.Warn(String.Format(
                            "Output files are not specified in \"auto\" mode for task {0}. Using handler for \"manual\" mode.",
                            task.TaskId
                        ));
                        goto case TaskLaunchMode.Manual;
                    }
                    else
                    {
                        for (int i = 0; i < task.OutputFiles.Length; i++)
                        {
                            if (forbiddenFileNamesRx.Any(rx => rx.IsMatch(task.OutputFiles[i].FileName)))
                                Log.Warn(String.Format(
                                    "Task {0}: request for forbidden file {1} is denied.",
                                    task.TaskId, task.OutputFiles[i].FileName
                                ));
                            else
                            {
                                string fileName = Path.GetFileName(task.OutputFiles[i].FileName);
                                string folderName = ftpFolder + Path.GetDirectoryName(task.OutputFiles[i].FileName).Replace(@"\", "/");
                                if (!folderName.EndsWith("/"))
                                    folderName += "/";

                                string[] fileNames = IOProxy.Ftp.GetFileNames(folderName);

                                task.OutputFiles[i].StorageId = null;
                                if (!String.IsNullOrEmpty(fileName) && fileNames.Contains(fileName))
                                {
                                    task.OutputFiles[i].StorageId = IOProxy.Storage.MoveInside(folderName + fileName, null);
                                }
                                else
                                {
                                    if (!String.IsNullOrEmpty(task.OutputFiles[i].SlotName) && 
                                        fileNames.Any(name => name.Contains(task.OutputFiles[i].SlotName)))
                                    {
                                        string expectedFileName = fileName;
                                        fileName = fileNames.First(name => name.Contains(task.OutputFiles[i].SlotName));
                                        task.OutputFiles[i].StorageId = IOProxy.Storage.MoveInside(folderName + fileName, null);

                                        Log.Warn(String.Format(
                                            "Task {0}: couldn't find expected output file \"{1}\" with slot \"{3}\". Using \"{2}\" instead.", 
                                            task.TaskId, expectedFileName, fileName, task.OutputFiles[i].SlotName
                                        ));
                                    }
                                    else
                                    {
                                        Log.Warn(String.Format(
                                            "Task {0}: couldn't find expected output file \"{1}\" with slot \"{2}\".",
                                            task.TaskId, fileName, task.OutputFiles[i].SlotName
                                        ));
                                    }
                                }
                            }
                        }
                    }
				break;

				case TaskLaunchMode.Manual:
					{
						string ftpFolderWithoutSlash = ftpFolder.Remove(ftpFolder.Length - 1);
						var fullFilePaths = 
                            IOProxy.Ftp.GetFileNamesInAllTree(ftpFolderWithoutSlash)
                            .Where(st => !forbiddenFileNamesRx.Any(rx => rx.IsMatch(st)))
                            .ToList();
						task.OutputFiles = new TaskFileDescription[fullFilePaths.Count];

						for (int i = 0; i < fullFilePaths.Count; i++)
						{
							task.OutputFiles[i].FileName = fullFilePaths[i].Remove(0, ftpFolder.Length);
							task.OutputFiles[i].SlotName = null;
							task.OutputFiles[i].StorageId = IOProxy.Storage.MoveInside(fullFilePaths[i], null);
						}
					}
				break;

				default:
					Log.Warn(String.Format(
						"Неожиданный тип режима запуска ({0}) в адаптере {1}",
						task.LaunchMode, this.ToString()
					));
				break;
			}

			Log.Stats("T_copy_to_storage", task.WfId, task.TaskId, DateTime.Now - timeBefore);
            Log.Debug("Output file names: " + String.Join(", ", task.OutputFiles.Select(file => file.FileName)));
        }
	}
}





