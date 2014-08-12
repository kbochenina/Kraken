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
	public partial class OrcaAdapter : CommonXyzPackageAdapter
	{
        private const string DEFAULT_OUTPUT_NAME = "orca";
        
        private const string INPUT_FILE_EXT = ".inp";
		private const string OUTPUT_FILE_EXT = ".out";

        private const string PACK_ORCA = "ORCA";

		public override bool Mathces(Task task)
		{
			if (task.Package.ToUpperInvariant() == PACK_ORCA)
				return true;

			return false;
		}

		protected override string GetInputFileTemplate()
		{
			return File.ReadAllText(CONST.Path.Templates.ORCA);
		}

		protected override string GetProperMoleculeDescription(Atom[] atoms)
		{
			string content = "";
			foreach (var atom in atoms)
			{
				content += String.Format(CultureInfo.InvariantCulture, "{0,-5} {2:F10} {3:F10} {4:F10}\n",
					atom.Element, atom.Number, atom.X, atom.Y, atom.Z);
			}

			return content.Trim();
		}

		protected override void ModifyParams(Dictionary<string, string> @params)
		{
		}

		/*
        public override IncarnationParams GetRunParams(ResourceConfig assignedCluster, TaskFileDescription[] inputFiles, TaskFileDescription[] outputFiles)
		{
			var singleOutputFile = outputFiles.Last(file => file.fileName.EndsWith(OUTPUT_FILE_EXT));

			IncarnationParams runParams = new IncarnationParams();
			runParams.StdInFile = GetInputFileNameWithExtension();
			runParams.StdOutFile = singleOutputFile.fileName; // with extension

			string cmdLine = "orca";
			cmdLine += " " + runParams.StdInFile;
			cmdLine += " " + runParams.StdOutFile;
			runParams.CommandLine = cmdLine;

			runParams.PackageNameInConfig = Packs.ORCA.ToString().ToUpper();

			return runParams;
		}
        */
          
		protected override string GetInputFileNameWithExtension()
		{
			return "orca" + INPUT_FILE_EXT;
		}

        public override void SetIncarnationParams(Task task)
        {
            string outputFileName = DEFAULT_OUTPUT_NAME + OUTPUT_FILE_EXT;
            if (task.OutputFiles != null && task.OutputFiles.Any(f => f.FileName.EndsWith(OUTPUT_FILE_EXT)))
                outputFileName = task.OutputFiles.First(file => file.FileName.EndsWith(OUTPUT_FILE_EXT)).FileName;

            //task.Incarnation.StdInFile = GetInputFileNameWithExtension();
            //task.Incarnation.StdOutFile = outputFileName; // with extension

            string cmdLine = "orca";
            cmdLine += " " + GetInputFileNameWithExtension(); //task.Incarnation.StdInFile;
            cmdLine += " " + outputFileName; // task.Incarnation.StdOutFile;
            task.Incarnation.CommandLine = cmdLine;

            //task.Incarnation.PackageNameInConfig = PACK_ORCA;
        }
    }
}



