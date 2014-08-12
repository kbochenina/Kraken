using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Globalization;

namespace MITP
{
	public class GamessAdapter : CommonXyzPackageAdapter
	{
        private const string DEFAULT_OUTPUT_NAME = "gamess";

		private const string INPUT_FILE_EXT = ".inp";
		private const string OUTPUT_FILE_EXT = ".out";

        private const string PACK_GAMESS = "GAMESS";

		public override bool Mathces(Task task)
		{
			if (task.Package.ToUpperInvariant() == PACK_GAMESS)
				return true;

			return false;			
		}

		protected override string GetInputFileTemplate()
		{
			return File.ReadAllText(CONST.Path.Templates.GAMESS);
		}

		protected override string GetProperMoleculeDescription(Atom[] atoms)
		{
			string content = "";
			foreach (var atom in atoms)
			{
				content += String.Format(CultureInfo.InvariantCulture, "{0,-10} {1,-5} {2,-15:F5} {3,-15:F5} {4,-15:F5}\n", 
					atom.Element, atom.Number, atom.X, atom.Y, atom.Z);
			}

			return content.Trim();
		}

		protected override void ModifyParams(Dictionary<string, string> @params)
		{
			// there is nothing to do yet
		}

        public override void SetIncarnationParams(Task task)
		{
            string outputFileName = DEFAULT_OUTPUT_NAME + OUTPUT_FILE_EXT;
            if (task.OutputFiles != null && task.OutputFiles.Any(f => f.FileName.EndsWith(OUTPUT_FILE_EXT)))
                outputFileName = task.OutputFiles.First(file => file.FileName.EndsWith(OUTPUT_FILE_EXT)).FileName;

			string cmdLine = "gms";
			cmdLine += " " + GetInputFileNameWithExtension();
            cmdLine += " " + outputFileName;

			task.Incarnation.CommandLine = cmdLine;
            //task.Incarnation.StdOutFile = "";
            //task.Incarnation.PackageNameInConfig = PACK_GAMESS;
		}

		protected override string GetInputFileNameWithExtension()
		{
			return "gamess" + INPUT_FILE_EXT;
		}
    }
}
