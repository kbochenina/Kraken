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
	public abstract class FileTransferAdapter : CommonPackageAdapter
	{
        public override void UploadAndPrepareInputFiles(Task task, string ftpFolder)
        {
            foreach (var file in task.InputFiles)
                IOProxy.Storage.MoveOutside(file.StorageId, ftpFolder + file.FileName);
        }
    }

	public class NtdmftAdapter : FileTransferAdapter
	{
        public override bool Mathces(Task task)
        {
            if (task.Package.ToUpperInvariant() == "NTDMFT")
                return true;

            return false;
        }

        public override IEnumerable<string> GetDefaultInputFileNames()
        {
            return new string[] { "SPDMFT.ini", "SPDMFT.ek" };
        }

        public override void UploadAndPrepareInputFiles(Task task, string ftpFolder)
		{
			base.UploadAndPrepareInputFiles(task, ftpFolder);

			string content = File.ReadAllText(CONST.Path.Templates.NTDMFT);
			IOProxy.Ftp.UploadFileContent(content, ftpFolder, Path.GetFileName(CONST.Path.Templates.NTDMFT));
		}

        public override void SetIncarnationParams(Task task)
        {
            int coresCount = task.AssignedTo.Cores.Sum();
            task.Incarnation.CommandLine = String.Format("mpirun {0} spfemwSEPA.out spfemwSEPA.list", coresCount);
            //task.Incarnation.PackageNameInConfig = "NTDMFT";
        }
    }

	public class MdkmcAdapter : FileTransferAdapter
	{
        public override bool Mathces(Task task)
        {
            string pack = task.Package.ToUpperInvariant();
            if (pack == "MD_KMC" || pack == "MD-KMC" || pack == "MDKMC")
                return true;

            return false;
        }

        public override IEnumerable<string> GetDefaultInputFileNames()
        {
            return new string[] { "G.txt", "input.txt" };
        }

        public override void SetIncarnationParams(Task task)
        {
			int coresCount = task.AssignedTo.Cores.Sum();
			task.Incarnation.CommandLine = String.Format("mpirun {0} mdkmc.x", coresCount);
            //task.Incarnation.PackageNameInConfig = "MD-KMC";
		}
	}

	public class DaltonAdapter : FileTransferAdapter
	{
        public override bool Mathces(Task task)
        {
            if (task.Package.ToUpperInvariant() == "DALTON")
                return true;

            return false;
        }

        public override IEnumerable<string> GetDefaultInputFileNames()
        {
            return new string[] { "input.dal", "input.mol" };
        }

        public override void SetIncarnationParams(Task task)
        {
			task.Incarnation.CommandLine = String.Format("dalton {0}", Path.GetFileNameWithoutExtension(task.InputFiles[0].FileName));
            //task.Incarnation.PackageNameInConfig = "DALTON";
		}
	}

	public class NaenAdapter : FileTransferAdapter
	{
        public override bool Mathces(Task task)
        {
            if (task.Package.ToUpperInvariant() == "NAEN")
                return true;

            return false;
        }

        public override IEnumerable<string> GetDefaultInputFileNames()
        {
            return new string[] { "input.txt" };
        }

        public override void SetIncarnationParams(Task task)
        {
			task.Incarnation.CommandLine = String.Format("naen");
            //task.Incarnation.PackageNameInConfig = "NAEN";
		}
	}

	public class UpconversionAdapter : FileTransferAdapter
	{
        public override bool Mathces(Task task)
        {
            if (task.Package.ToUpperInvariant() == "UPCONVERSION")
                return true;

            return false;
        }

        public override IEnumerable<string> GetDefaultInputFileNames()
        {
            return new string[] { "Init.txt", "x1.txt", "y1.txt", "z1.txt" };
        }

        public override void SetIncarnationParams(Task task)
        {
			int coresCount = task.AssignedTo.Cores.Sum();
			task.Incarnation.CommandLine = String.Format("mpirun -np {0} MC", coresCount);
            //task.Incarnation.PackageNameInConfig = "UPCONVERSION";
		}
	}

    public class TestPAdapter : FileTransferAdapter
    {
        public override bool Mathces(Task task)
        {
            if (task.Package.ToUpperInvariant() == "TESTP")
                return true;

            return false;
        }

        public override IEnumerable<string> GetDefaultInputFileNames()
        {
            return new string[] { "0.in", "1.in" };
        }

        public override void SetIncarnationParams(Task task)
        {
            task.Incarnation.CommandLine = String.Format(
                //"ntestp.sh {3} {0} {1} {2}", 
                "TESTP {3} {0} {1} {2}", 
                task.InputFiles[0].FileName, 
                task.InputFiles[1].FileName,
                (!String.IsNullOrEmpty(task.OutputFiles[0].FileName))? 
                    task.OutputFiles[0].FileName:
                    task.OutputFiles[0].SlotName,                    
                task.Params["operation"]
            );
            //task.Incarnation.PackageNameInConfig = "TESTP";
        }
    }

    public class BelmanAdapter : FileTransferAdapter
    {
        public override bool Mathces(Task task)
        {
            if (task.Package.ToUpperInvariant() == "BELMAN")
                return true;

            return false;
        }

        public override IEnumerable<string> GetDefaultInputFileNames()
        {
            return new string[] { "input.zip" };
        }

        public override void SetIncarnationParams(Task task)
        {
            task.Incarnation.CommandLine = String.Format("runMpi.sh {0}", task.InputFiles[0].FileName);
            //task.Incarnation.PackageNameInConfig = "BELMAN";
        }
    }

}