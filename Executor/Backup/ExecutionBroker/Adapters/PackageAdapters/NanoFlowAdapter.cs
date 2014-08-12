using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;

namespace MITP
{
	public class NanoFlowAdapter : CommonPackageAdapter
	{
        private const string PACK_NANOFLOW = "NANOFLOW";

        public override bool Mathces(Task task)
        {
            if (task.Package.ToUpperInvariant() == PACK_NANOFLOW)
                return true;

            return false;
        }

        public override IEnumerable<string> GetDefaultInputFileNames()
        {
            var res = CONST.Path.Templates.NanoFlowConstFiles.ToList();
            res.Add(CONST.Path.Templates.NanoFlow);
            return res.Select(st => /* "IO/" + */ Path.GetFileName(st)); // todo : "IO/" folder for manual & auto modes (Nanoflow)
        }

        public override void SetIncarnationParams(Task task)
		{
			int coresCount = task.AssignedTo.Cores.Sum();
			task.Incarnation.CommandLine = String.Format("mpirun {0} NanoFlow", coresCount);
            //task.Incarnation.PackageNameInConfig = PACK_NANOFLOW;
		}

        public override void UploadAndPrepareInputFiles(Task task, string ftpFolder)
		{
            IOProxy.Ftp.MakeFolder(ftpFolder, "IO");
            ftpFolder += "IO/";

            foreach (string fileName in CONST.Path.Templates.NanoFlowConstFiles)
                IOProxy.Ftp.UploadLocalFile(fileName, ftpFolder, Path.GetFileName(fileName));

            foreach (var file in task.InputFiles)
			{
				byte[] bytes = IOProxy.Storage.GetBinaryContent(file.StorageId);
                IOProxy.Ftp.UploadFileContent(bytes, ftpFolder, file.FileName);
			}

			string content = File.ReadAllText(CONST.Path.Templates.NanoFlow);
            var applicableParamsNames = task.Params.Keys.Where(key => task.Params[key] != null);
			foreach (var paramName in applicableParamsNames)
			{
				Regex regex = new Regex(
					"(?<=" +
					// перед искомым должно идти нижеследующее: 
						paramName +
						@"[^=]*" + // сколько угодно символов, которые не "="
						@"=" +
						@"\s*" + // сколько угодно пробельных символов
						 "\"" + // кавычка
					")"

					// собственно, ищем сколько угодно не кавычек:
					+ "[^\"]*" +

					"(?=" +
					// после искомого должно идти нижеследующее:
						 "\"" + // кавычка
						@"\s*" + // сколько угодно пробельных символов
						@";" +
					")"
				);

				content = regex.Replace(content, task.Params[paramName]);
			}

            IOProxy.Ftp.UploadFileContent(content, ftpFolder, Path.GetFileName(CONST.Path.Templates.NanoFlow));
		}
    }
}