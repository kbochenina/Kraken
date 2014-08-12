using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace MITP
{
    public enum CopyPhase
    {
        In,
        Out,
        None
    }

    [DataContract]
    public class IncarnationParams
    {
        private const string TASK_TOKEN  = "{task}";
        private const string PHASE_TOKEN = "{phase}";

        [DataMember] public string CommandLine { get; set; }
        [DataMember] public string PackageName { get; set; }

        [DataMember] public string UserCert { get; set; }

        //[DataMember]
        //public string ProvidedTaskId { get; set; } // todo : XML comments on ProvidedTaskId etc.

        [DataMember]
        public IEnumerable<TaskFileDescription> FilesToCopy { get; set; }

        [DataMember] public IEnumerable<string> ExpectedOutputFileNames { get; set; }
        [DataMember] public bool CanExpectMoreFiles { get; set; }

        public static string IncarnatePath(string path, ulong taskId, string farmId, CopyPhase phase)
        {
            string res;
            if (path != null) {
                res = IncarnatePath(path, taskId, phase);
                var slash = res.Substring(res.Length - 2);
                res.Replace(taskId.ToString(), (farmId + slash + taskId));
            } else
            {
                res = taskId.ToString();
            }
            return res;
        }

        public static string IncarnatePath(string path, ulong taskId, CopyPhase phase)
        {
            string phaseString = (phase == CopyPhase.None)? "": phase.ToString().ToLowerInvariant();
            path = path.Replace(TASK_TOKEN, taskId.ToString()).Replace(PHASE_TOKEN, phaseString);

            string slash = path.Contains(@"\")? @"\": @"/";
            if (!path.EndsWith(slash))
                path = path + slash;

            return path;
        }

        public IncarnationParams(IncarnationParams otherIncarnation)
            : this()
        {
            if (otherIncarnation != null)
            {
                this.CommandLine = otherIncarnation.CommandLine;
                //this.StdInFile   = otherIncarnation.StdInFile;
                //this.StdOutFile  = otherIncarnation.StdOutFile;

                //this.PackageNameInConfig = otherIncarnation.PackageNameInConfig;

                this.PackageName = otherIncarnation.PackageName;
                //this.ProvidedTaskId = otherIncarnation.ProvidedTaskId;

                if (otherIncarnation.FilesToCopy.Any())
                    FilesToCopy = otherIncarnation.FilesToCopy.Select(descr => descr).ToArray(); // todo : maybe Incarnation.FilesToCopy IEnumerable -> array[]?

                if (otherIncarnation.ExpectedOutputFileNames.Any())
                    ExpectedOutputFileNames = otherIncarnation.ExpectedOutputFileNames.Select(name => name).ToArray(); // todo : maybe Incarnation.ExpectedOutputFileNames IEnumerable -> array[]?
                CanExpectMoreFiles = otherIncarnation.CanExpectMoreFiles;

                //this.InputFolderPath = otherIncarnation.InputFolderPath;
                //this.OutputFolderPath = otherIncarnation.OutputFolderPath;
            }
        }

        public IncarnationParams()
        {
            FilesToCopy = new TaskFileDescription[0];
            ExpectedOutputFileNames = new string[0];
            CanExpectMoreFiles = false;
        }
    }
}


