using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

namespace MITP
{
	public abstract class PlasmonEtcAdapter : CommonPackageAdapter
	{
		protected List<string> _commandLineParams = null;

		public abstract string GetTemplateFilePath();

        protected void SetCommandLine(Task task, string manycoreScriptName)
        {
            int coresCount = task.AssignedTo.Cores.Sum();

            if (coresCount > 1)
                task.Incarnation.CommandLine = manycoreScriptName;
            else
                task.Incarnation.CommandLine = "run.sh";

            if (_commandLineParams != null && _commandLineParams.Count > 0)
                task.Incarnation.CommandLine += " " + String.Join(" ", _commandLineParams.ToArray());
        }

        public override void UploadAndPrepareInputFiles(Task task, string ftpFolder)
        {
			if (String.IsNullOrEmpty(task.Params[CONST.Params.Method]))
                task.Params.Remove(CONST.Params.Method);

			_commandLineParams = new List<string>();

			IOProxy.Ftp.MakeFolder(ftpFolder, "Graphs");

			string templateFilePath = GetTemplateFilePath();
			string content = File.ReadAllText(templateFilePath);

			foreach (string paramName in task.Params.Keys)
			{
				string paramValue = task.Params[paramName];
				if (paramValue.ToLower() == "true")
					paramValue = "T";
				else
				if (paramValue.ToLower() == "false")
					paramValue = "F";


				Regex regex = new Regex(
					"(?<=" +
					// до имени параметра не должно быть никаких символов: 
						@"[^\S]" +
					// перед искомым должно идти нижеследующее: 
						paramName.ToUpper() +
						@"\s*" + // сколько угодно пробельных символов
						 "=" +
						@"\s*" + // сколько угодно пробельных символов
					")"

					// собственно, ищем сколько угодно не запятых и не пробелов:
					+ @"[^,\s]*" +

					"(?=" +
					// после искомого должно идти нижеследующее:
						@"\s*" + // сколько угодно пробельных символов 
						 "[,/]" +
					")"
				);

				if (regex.IsMatch(content))
					content = regex.Replace(content, paramValue);
				else
					_commandLineParams.Add(paramName + "=" + paramValue);

			}

			string fileName = Path.GetFileName(templateFilePath);
			IOProxy.Ftp.UploadFileContent(content, ftpFolder, fileName);
		}
    }

	public class PlasmonAdapter : PlasmonEtcAdapter
	{
        public override bool Mathces(Task task)
        {
            if (task.Package.ToUpperInvariant() == "PLASMON")
                return true;

            return false;
        }

        public override void UploadAndPrepareInputFiles(Task task, string ftpFolder)
		{
			base.UploadAndPrepareInputFiles(task, ftpFolder);
			IOProxy.Ftp.UploadLocalFile(CONST.Path.Templates.PlasmonInitialSpectr, ftpFolder, Path.GetFileName(CONST.Path.Templates.PlasmonInitialSpectr));
		}

		public override string GetTemplateFilePath()
		{
			return CONST.Path.Templates.Plasmon;
		}

        public override void SetIncarnationParams(Task task)
        {
            SetCommandLine(task, "runOMP.sh");
            //task.Incarnation.PackageNameInConfig = "PLASMON";
		}

        public override IEnumerable<string> GetDefaultInputFileNames()
        {
            return new string[] { "Plasmon.dataList", "PlasmonInitialSpectr.dat" };
        }
    }

	public class QDLaserAdapter : PlasmonEtcAdapter
	{
        public override bool Mathces(Task task)
        {
            if (task.Package.ToUpperInvariant() == "QDLASER")
                return true;

            return false;
        }

        public override void UploadAndPrepareInputFiles(Task task, string ftpFolder)
		{
			base.UploadAndPrepareInputFiles(task, ftpFolder);
			IOProxy.Storage.MoveOutside(task.InputFiles[0].StorageId, ftpFolder + "QDLaser.bin");
		}

		public override string GetTemplateFilePath()
		{
			return CONST.Path.Templates.QDLaser;
		}

        public override void SetIncarnationParams(Task task)
        {
            SetCommandLine(task, "runMPI.sh");
            //task.Incarnation.PackageNameInConfig = "QDLASER";
		}

        public override IEnumerable<string> GetDefaultInputFileNames()
        {
            return new string[] { "QDLaser.dataList", "QDLaser.bin" };
        }
    }

	public class JAggregateAdapter : PlasmonEtcAdapter
	{
        public override bool Mathces(Task task)
        {
            if (task.Package.ToUpperInvariant() == "JAGGREGATE")
                return true;

            return false;
        }
        
        public override string GetTemplateFilePath()
		{
			return CONST.Path.Templates.JAggregate;
		}

        public override void SetIncarnationParams(Task task)
        {
            SetCommandLine(task, "runMPI.sh");
            //task.Incarnation.PackageNameInConfig = "JAGGREGATE";
        }

        public override IEnumerable<string> GetDefaultInputFileNames()
        {
            return new string[] { "JAggregate.dataList", "JAggregateInitialBeam.dataList", "JAggregate.bin" };
        }
    }
}