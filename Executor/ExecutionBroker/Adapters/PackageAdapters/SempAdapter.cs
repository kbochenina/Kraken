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
	public class SempAdapter : CommonXyzPackageAdapter
	{
        private const string DEFAULT_OUTPUT_NAME = "semp";

        private const string INPUT_FILE_EXT = ".inp";
		private const string OUTPUT_FILE_EXT = ".out";
		private const string TMP_FILE_EXT = ".wrk";

        private const string PACK_SEMP = "SEMP";

		public override bool Mathces(Task task)
		{
			if (task.Package.ToUpperInvariant() == PACK_SEMP)
				return true;

			return false;
		}

		protected override string GetInputFileTemplate()
		{
			return File.ReadAllText(CONST.Path.Templates.SEMP);
		}

		protected override string GetProperMoleculeDescription(Atom[] atoms)
		{
			string content = "";

			int atomsInLine = 0;
			foreach (var atom in atoms)
			{
				content += String.Format("{0,-3}", atom.Element);

				atomsInLine = (atomsInLine + 1) % 26;
				if (atomsInLine == 0)
					content += Environment.NewLine;
			}

			bool even = false;
			foreach (var atom in atoms)
			{
				if (!even)
					content += Environment.NewLine;

				content += String.Format(CultureInfo.InvariantCulture, "{0,10:F5}{1,10:F5}{2,10:F5}",
					atom.X, atom.Y, atom.Z);

				even = !even;
			}

			return content.Trim();
		}

		protected override void ModifyParams(Dictionary<string, string> @params)
		{
			string[] names = @params.Keys.ToArray<string>();
			foreach (string paramName in names)
			{
				var culture = System.Globalization.CultureInfo.InvariantCulture;
				string paramValue = @params[paramName];

				switch (paramName)
				{
					case "atoms_count":
						@params[paramName] = String.Format("{0,-5}", Double.Parse(paramValue, culture).ToString(culture));
						break;

					case "shift_layers":
						@params[paramName] = String.Format("{0,-5}", Double.Parse(paramValue, culture).ToString(culture));
						break;

					case "dumping":
						@params[paramName] = String.Format("-{0,-4}", Double.Parse(paramValue, culture).ToString(culture));
						break;

					case "precision":
						@params[paramName] = String.Format("{0,-10}", Double.Parse(paramValue, culture).ToString(culture));
						break;

					case "dc_cutoff":
						@params[paramName] = String.Format("{0,-10}", Double.Parse(paramValue, culture).ToString(culture));
						break;
				}
			}
		}

		public override void  SetIncarnationParams(Task task)
    	{
            string outputFileName = DEFAULT_OUTPUT_NAME + OUTPUT_FILE_EXT;
            if (task.OutputFiles != null && task.OutputFiles.Any(f => f.FileName.EndsWith(OUTPUT_FILE_EXT)))
                outputFileName = task.OutputFiles.First(file => file.FileName.EndsWith(OUTPUT_FILE_EXT)).FileName;

            string cmdLine = "zindo1.sh";
			cmdLine += " " + GetInputFileNameWithExtension();
            cmdLine += " " + outputFileName; // with extension
            cmdLine += " " + Path.GetFileNameWithoutExtension(outputFileName) + TMP_FILE_EXT;
			task.Incarnation.CommandLine = cmdLine;

            //task.Incarnation.PackageNameInConfig = PACK_SEMP;
		}

		protected override string GetInputFileNameWithExtension()
		{
			return "semp" + INPUT_FILE_EXT;
		}
	}
}

