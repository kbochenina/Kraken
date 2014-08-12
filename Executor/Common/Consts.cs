using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Configuration;

// disable documentation-related warnings
#pragma warning disable 1591

// REV: переименовать MITP в Easis
namespace MITP
{
    // REV: имя класса большими буквами?
    public abstract class CONST
	{
		public const string ManualMethodName = "Manual";

		public static type Dirty<type>(type value)
		{
			//TODO : [future] throw new Exception("Using of dirty constants detected!");
			return value;
		}

        public abstract class Providers
        {
            public static string Metacluster { get { return "Cluster"; } }
            public static string GridNns { get { return "Grid NNN"; } }
            public static string WinPc { get { return "Win PC"; } }
        }

		public abstract class Params
		{
			//public const string Package = "package";
            // zn: problem with method in ruby, all method were renamed to calc_method
			public const string Method = "calc_method";
		}

		public abstract class ErrorMessage
		{
			public const string UnknownPackage = "Указанный пакет не поддерживается!";
			public const string WrongServiceNamesConfig = "Ошибка конфигурации имен сервисов для EntryPoint в PES.";
		}

		public abstract class Path
		{
			static Path()
			{
				// todo: [future] _homeFolder = AppDomain.CurrentDomain.BaseDirectory;

				/* */
				string codeBase = Assembly.GetAssembly(typeof(Path)).CodeBase;
				string homeUrl = System.IO.Path.GetDirectoryName(codeBase);
				string[] separators = new string[] { @"://", @":\\", @":/", @":\" };

				_homeFolder = homeUrl;
				foreach (string separator in separators)
				{
					int startIndex = _homeFolder.IndexOf(separator);
					if (startIndex >= 0)
						_homeFolder = _homeFolder.Substring(startIndex + separator.Length);
				}
				/* */
			}

			private static readonly string _homeFolder;
			public static string HomeFolder { get { return _homeFolder; } }

            public static string LogFile { get { return HomeFolder + @"\..\..\ExecutionLogs\log.txt"; } }


            public static string OverFile { get { return HomeFolder + @"\..\..\ExecutionLogs\over.txt"; } }
            public static string OverCsvFile { get { return HomeFolder + @"\..\..\ExecutionLogs\over.csv"; } }
            public static string OverAvgCsvFile { get { return HomeFolder + @"\..\..\ExecutionLogs\over_avg.csv"; } }
            public static string OverHeadersFile { get { return HomeFolder + @"\..\..\ExecutionLogs\over_head.txt"; } }

            public static string OverSpecFile { get { return HomeFolder + @"\..\..\ExecutionLogs\over_pb.csv"; } }
            public static string OverSpecHeadersFile { get { return HomeFolder + @"\..\..\ExecutionLogs\over_pb_head.txt"; } }

            public static string ModelCoefCsvFile { get { return HomeFolder + @"\..\..\ExecutionLogs\model_coef.csv"; } }
            public static string ModelCoefHeadersFile { get { return HomeFolder + @"\..\..\ExecutionLogs\model_coef_head.txt"; } }

            public static string GridJobsFolder { get { return HomeFolder + @"\..\..\ExecutionJobs\"; } }

			public static string ManualTemplatesFolder { get { return HomeFolder + @"\manual\"; } }

			public abstract class PersistenceFileNames
			{
                public static string Folder { get { return HomeFolder + @"\..\..\ExecutionSaves\"; } }

				public static string Task { get { return Folder + "task_{0}"; } }
				public static string SequenceRun { get { return Folder + "seq_{0}_run"; } }
				public static string SequenceInfo { get { return Folder + "seq_{0}_info"; } }
			}

            // REV: вынести это во внешний конфиг
            public abstract class Templates
			{
				public static string ORCA { get { return HomeFolder + @"\Templates\orca.inp"; } }
				public static string GAMESS { get { return HomeFolder + @"\Templates\gamess.inp"; } }

				public static string SEMP { get { return HomeFolder + @"\Templates\semp.inp"; } }

				public static string Plasmon { get { return HomeFolder + @"\Templates\Plasmon.dataList"; } }
				public static string PlasmonInitialSpectr { get { return HomeFolder + @"\Templates\PlasmonInitialSpectr.dat"; } }

				public static string PlasmonOld { get { return HomeFolder + @"\Templates\PlasmonOld.dataList"; } }

				public static string QDLaser { get { return HomeFolder + @"\Templates\QDLaser.dataList"; } }
				public static string JAggregate { get { return HomeFolder + @"\Templates\JAggregate.dataList"; } }

				public static string NanoFlow { get { return HomeFolder + @"\Templates\NanoFlow\inInfo1.txt"; } }

				public static string NTDMFT { get { return HomeFolder + @"\Templates\SPDMFT.ek"; } }

				public static string[] NanoFlowConstFiles { get { return new string[] {
					HomeFolder + @"\Templates\NanoFlow\InterMol.txt",
					HomeFolder + @"\Templates\NanoFlow\Moleculs.txt",
					HomeFolder + @"\Templates\NanoFlow\MTable.txt",
					HomeFolder + @"\Templates\NanoFlow\strTube.txt"
				};}}
			}

			public static string TestMolecule { get { return HomeFolder + @"\Templates\test.xyz"; } }

            public static string PsExec { get { return HomeFolder + @"\PsTools\PsExec.exe"; } }
            public static string PsList { get { return HomeFolder + @"\PsTools\PsList.exe"; } }
		}
	}
}

