using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;

namespace MITP
{
	public abstract class CommonXyzPackageAdapter : CommonPackageAdapter
	{
        [Serializable]
        public class Atom // todo : Atom class refactoring
        {
            #region Mendeleev's Table of elements
            private readonly string[] _elementsTable = new string[] {
				" ",
				"H", "He",
				"Li", "Be", "B", "C", "N", "O", "F", "Ne",
				"Na", "Mg", "Al", "Si", "P", "S", "Cl", "Ar",
				"K", "Ca", "Sc", "Ti", "V", "Cr", "Mn", "Fe", "Co", "Ni", "Cu",	"Zn", "Ga", "Ge", "As", "Se", "Br", "Kr",
				"Rb", "Sr",	"Y", "Zr", "Nb", "Mo", "Tc", "Ru", "Rh", "Pd", "Ag", "Cd", "In", "Sn", "Sb", "Te", "I", "Xe",
				"Cs", "Ba",
				"La", "Ce", "Pr", "Nd", "Pm", "Sm", "Eu", "Gd", "Tb", "Dy", "Ho", "Er", "Tm", "Yb", "Lu",
				"Hf", "Ta", "W", "Re", "Os", "Ir", "Pt", "Au", "Hg", "Tl", "Pb", "Bi", "Po", "At", "Rn",
				"Fr", "Ra",
				"Ac", "Th", "Pa", "U", "Np", "Pu", "Am", "Cm", "Bk", "Cf", "Es", "Fm", "Md", "No", "Lr",
				"Rf", "Db", "Sg", "Bh", "Hs", "Mt", "Ds", "Rg",
				"Cn", "Uut", "Uuq", "Uup", "Uuh", "Uus", "Uuo", "Uue", "Ubn",
                "Ubu", "Ubb", "Ubt", "Ubq", "Ubp", "Ubh",
			};
            #endregion

            #region Fields: Number, Element, X, Y, Z

            private readonly int _number;

            public int Number
            {
                get { return _number; }
            }

            public string Element
            {
                get { return _elementsTable[_number]; }
            }

            private double _x, _y, _z;

            public double X
            {
                get { return _x; }
                set { _x = value; }
            }

            public double Y
            {
                get { return _y; }
                set { _y = value; }
            }

            public double Z
            {
                get { return _z; }
                set { _z = value; }
            }

            #endregion

            #region Constructors

            public Atom(int number) : this(number, 0, 0, 0) { }

            public Atom(int number, double x, double y, double z)
            {
                _number = number;
                _x = x;
                _y = y;
                _z = z;
            }

            public Atom(string element) : this(element, 0, 0, 0) { }

            public Atom(string element, double x, double y, double z)
            {
                for (int i = 0; i < _elementsTable.Length; i++)
                {
                    if (element.ToLowerInvariant() == _elementsTable[i].ToLowerInvariant())
                        _number = i;
                }

                _x = x;
                _y = y;
                _z = z;
            }

            private Atom()
            {
            }

            #endregion
        }

        protected const string MOLECULE_PARAM_NAME = "atoms";
		protected const string ATOMS_COUNT_PARAM_NAME = "atoms_count";
		protected const string PROCS_COUNT_PARAM_NAME = "procs_count";

		protected abstract string GetInputFileNameWithExtension();
		protected abstract string GetInputFileTemplate();
		protected abstract string GetProperMoleculeDescription(Atom[] atoms);
		protected abstract void ModifyParams(Dictionary<string, string> @params);

		protected virtual string GetTemplateParamDelim()
		{
			return "%";
		}

        public override IEnumerable<string> GetDefaultInputFileNames()
        {
            return new string[] { GetInputFileNameWithExtension() };
        }

		private int _atomsCount = 0;

        public override void OnManualStart(Task task, string ftpFolder)
		{
			task.InputFiles[0].FileName = GetInputFileNameWithExtension();
			base.OnManualStart(task, ftpFolder);
		}

		private Atom[] ParseOrcaXyz(string[] words)
		{
			var invariantCulture = System.Globalization.CultureInfo.InvariantCulture;

			int len = words.Length / 4;
			Atom[] atoms = new Atom[len];

			for (int i = 0; i < len; i++)
			{
				string element = words[i*4];
				double x = Double.Parse(words[i*4 + 1], invariantCulture);
				double y = Double.Parse(words[i*4 + 2], invariantCulture);
				double z = Double.Parse(words[i*4 + 3], invariantCulture);

				atoms[i] = new Atom(element, x, y, z);
			}

			return atoms;
		}

		private Atom[] ParseSempXyz(string[] words)
		{
			var invariantCulture = System.Globalization.CultureInfo.InvariantCulture;
			
			int len = words.Length / 4;
			Atom[] atoms = new Atom[len];

			for (int i = 0; i < len; i++)
			{
				string element = words[i];
				double x = Double.Parse(words[len + i*3 + 0], invariantCulture);
				double y = Double.Parse(words[len + i*3 + 1], invariantCulture);
				double z = Double.Parse(words[len + i*3 + 2], invariantCulture);

				atoms[i] = new Atom(element, x, y, z);
			}

			return atoms;
		}

		private Atom[] ParseGamessXyz(string[] words)
		{
			var invariantCulture = System.Globalization.CultureInfo.InvariantCulture;
			int len = words.Length / 5;
			Atom[] atoms = new Atom[len];

			for (int i = 0; i < len; i++)
			{
				int elementNumber = Int32.Parse(words[i*5 + 1]);
				double x = Double.Parse(words[i*5 + 2], invariantCulture);
				double y = Double.Parse(words[i*5 + 3], invariantCulture);
				double z = Double.Parse(words[i*5 + 4], invariantCulture);

				atoms[i] = new Atom(elementNumber, x, y, z);
			}

			return atoms;
		}

		private Atom[] ParseNumsXyz(string[] words)
		{
			var invariantCulture = System.Globalization.CultureInfo.InvariantCulture;
			int len = words.Length / 4;
			Atom[] atoms = new Atom[len];

			for (int i = 0; i < len; i++)
			{
				int elementNumber = Int32.Parse(words[i*4 + 0]);
				double x = Double.Parse(words[i*4 + 1], invariantCulture);
				double y = Double.Parse(words[i*4 + 2], invariantCulture);
				double z = Double.Parse(words[i*4 + 3], invariantCulture);

				atoms[i] = new Atom(elementNumber, x, y, z);
			}

			return atoms;
		}

		protected Atom[] XyzToAtmos(string xyzMoleculeDescription)
		{
			string[] words = xyzMoleculeDescription.Split(new char[] { '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);

			var invariantCulture = System.Globalization.CultureInfo.InvariantCulture;
			var intStyle = System.Globalization.NumberStyles.Integer;
			var floatStyle = System.Globalization.NumberStyles.Float;

			int peekLength = 10;
			bool[] isInt = new bool[peekLength];
			bool[] isFloat = new bool[peekLength];
			bool[] isAlpha = new bool[peekLength];
			for (int i = 0; i < peekLength; i++)
			{
				int n;
				isInt[i] = Int32.TryParse(words[i], intStyle, invariantCulture, out n);

				double d;
				isFloat[i] = Double.TryParse(words[i], floatStyle, invariantCulture, out d);

				isAlpha[i] = !isFloat[i];
			}

			bool parsingSempXyz   = isAlpha[0] && isAlpha[1];
			bool parsingGamessXyz = isAlpha[0] && isInt[1] && isAlpha[5];
			bool parsingOrcaXyz   = isAlpha[0] && isFloat[1] && isAlpha[4];
			bool parsingNumsXyz   = isInt[0] && isFloat[1] && isFloat[2] && isFloat[3] && isInt[4];
			
			Atom[] atoms = null;

			if (parsingOrcaXyz)
				atoms = ParseOrcaXyz(words);
			else
			if (parsingSempXyz)
				atoms = ParseSempXyz(words);
			else
			if (parsingGamessXyz)
				atoms = ParseGamessXyz(words);
			else
			if (parsingNumsXyz)
				atoms = ParseNumsXyz(words);
			else
				throw new Exception("Unknown input file format");

			_atomsCount = atoms.Length;
			return atoms;
		}

		/*
        public override void UploadAndPrepareInputFiles(string ftpInputFolder, TaskFileDescription[] files, ResourceConfig assignedCluster, Dictionary<string, string> paramsTable, Packs package, string method)
		{
			var xyzInputFile = files[0]; // the only input

			string xyzMoleculeDescription = IOProxy.Storage.GetContent(xyzInputFile.storageId);
			string properMoleculeDescription = GetProperMoleculeDescription( XyzToAtmos(xyzMoleculeDescription) );

			var paramsDictRaw = new Dictionary<string, string>();
			paramsDictRaw.Add(ATOMS_COUNT_PARAM_NAME, _atomsCount.ToString());
			int procsCount = assignedCluster.Cores.Aggregate((cur, next) => cur + next);
			paramsDictRaw.Add(PROCS_COUNT_PARAM_NAME, procsCount.ToString());
			foreach (var paramName in paramsTable.Keys)
				paramsDictRaw.Add(paramName, paramsTable[paramName]);

			ModifyParams(paramsDictRaw);

			var paramsDict = new Dictionary<string, string>();
			foreach (string paramName in paramsDictRaw.Keys)
			{
				string paramValue = paramsDictRaw[paramName];

				paramsDict[paramName] = paramValue;
				paramsDict[paramName + "=" + paramValue] = paramName + "=" + paramValue;								
			}
	
			string template = GetInputFileTemplate();
			string content = template.Replace(GetTemplateParamDelim() + MOLECULE_PARAM_NAME + GetTemplateParamDelim(), properMoleculeDescription);
			content = InsertParams(content, paramsDict);
			content = content.Replace("\r\n", "\n");
			IOProxy.Ftp.UploadFileContent(content, ftpInputFolder, GetInputFileNameWithExtension());
		}
        */

		protected string InsertParams(string content, Dictionary<string, string> @params)
		{
			string DELIM = GetTemplateParamDelim();
			string DELIM_REGEX = Regex.Escape(DELIM);

			Regex regex = null;

			using (var checker = new PackageParametersChecker(0, @params))
			{
				checker.MarkParameterAsUsed(ATOMS_COUNT_PARAM_NAME);
				checker.MarkParameterAsUsed(ATOMS_COUNT_PARAM_NAME + "=" + @params[ATOMS_COUNT_PARAM_NAME]);
				checker.MarkParameterAsUsed(PROCS_COUNT_PARAM_NAME);
				checker.MarkParameterAsUsed(PROCS_COUNT_PARAM_NAME + "=" + @params[PROCS_COUNT_PARAM_NAME]);
				checker.MarkParameterAsUsed(CONST.Params.Method);
				checker.MarkParameterAsUsed(CONST.Params.Method + "=" + (@params[CONST.Params.Method] ?? ""));

				foreach (string paramName in @params.Keys)
				{
					if (@params[paramName].ToLower() != "false")
					{
						var contentBuilder = new StringBuilder(content);

						// Вставить значения во все переменные с данным именем:
						// %param% -> value
						contentBuilder.Replace(DELIM + paramName + DELIM, @params[paramName]);

						// Так как параметр нашелся, раскрыть все плюсовые секции:
						// %param+% ... %param+;% -> ...
						contentBuilder.Replace(DELIM + paramName + @"+;" + DELIM, "");
						contentBuilder.Replace(DELIM + paramName + @"+" + DELIM, "");


						string escapedParamName = Regex.Escape(paramName);

						// Выкинуть все минусовые секции с этим параметром:
						// %param-% ... %param-;" -> null
						regex = new Regex(
							DELIM_REGEX + escapedParamName + "-" + DELIM_REGEX  // %param-%
							+ @"[\s\S]*?" +								        // any characters, as little as possible
							DELIM_REGEX + escapedParamName + "-;" + DELIM_REGEX // %param-;%
						);

						string newContent = regex.Replace(contentBuilder.ToString(), "");
						if (newContent != content)
						{
							content = newContent;
							checker.MarkParameterAsUsed(paramName);
							checker.MarkParameterAsUsed(paramName + "=" + @params[paramName]);
						}
					}
				}
			}

			// Нераскрытые блоки, параметры которых не нашли и не надо - раскрываем
			// %param-% ... %param-;% -> ...
			regex = new Regex(DELIM_REGEX + @"[^\s" + DELIM_REGEX + "]*" + "-;" + DELIM_REGEX); // %param-;%
			content = regex.Replace(content, "");

			regex = new Regex(DELIM_REGEX + @"[^\s" + DELIM_REGEX + "]*" + "-" + DELIM_REGEX); // %param-%
			content = regex.Replace(content, "");

			// Нераскрытые блоки, параметры которых не нашли, но которые быть должны - удаляем целиком
			// %param+% ... %param+;% -> null
			regex = new Regex(
				DELIM_REGEX + @"([^\s" + DELIM_REGEX + "]*)" + @"\+" + DELIM_REGEX  // %param+%
				//+ "[^" + DELIM_REGEX + "]*" +                                  // was anything but %
				+ @"[\s\S]*?" +                                                  // any characters, as little as possible
				DELIM_REGEX + @"\1\+;" + DELIM_REGEX                             // %sameparam+;%
			);
			content = regex.Replace(content, "");

			// Все ненайденные подстановочные параметры просто выкинуть:
			// %param% -> null
			regex = new Regex(DELIM_REGEX + @"[^\s" + DELIM_REGEX + "]*" + DELIM_REGEX); // %param%
			content = regex.Replace(content, "");

			return content;
		}

        public override void UploadAndPrepareInputFiles(Task task, string ftpFolder)
        {
            var xyzInputFile = task.InputFiles[0]; // the only input

            string xyzMoleculeDescription = IOProxy.Storage.GetContent(xyzInputFile.StorageId);
            string properMoleculeDescription = GetProperMoleculeDescription(XyzToAtmos(xyzMoleculeDescription));

            var paramsDictRaw = new Dictionary<string, string>();
            paramsDictRaw.Add(ATOMS_COUNT_PARAM_NAME, _atomsCount.ToString());
            int procsCount = task.AssignedTo.Cores.Aggregate((cur, next) => cur + next);
            paramsDictRaw.Add(PROCS_COUNT_PARAM_NAME, procsCount.ToString());
            foreach (var paramName in task.Params.Keys)
                paramsDictRaw.Add(paramName, task.Params[paramName]);

            ModifyParams(paramsDictRaw);

            var paramsDict = new Dictionary<string, string>();
            foreach (string paramName in paramsDictRaw.Keys)
            {
                string paramValue = paramsDictRaw[paramName];

                paramsDict[paramName] = paramValue;
                paramsDict[paramName + "=" + paramValue] = paramName + "=" + paramValue;
            }

            string template = GetInputFileTemplate();
            string content = template.Replace(GetTemplateParamDelim() + MOLECULE_PARAM_NAME + GetTemplateParamDelim(), properMoleculeDescription);
            content = InsertParams(content, paramsDict);
            content = content.Replace("\r\n", "\n");
            IOProxy.Ftp.UploadFileContent(content, ftpFolder, GetInputFileNameWithExtension());
        }
    }
}



