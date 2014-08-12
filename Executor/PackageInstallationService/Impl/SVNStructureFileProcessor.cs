using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PackageInstallationService.Interfaces;

namespace PackageInstallationService.Impl
{
    class SVNStructureFileProcessor : IStructureFileProcessor
    {
        private string metadataFileName;

        public SVNStructureFileProcessor(string metadataFileName)
        {
            if (string.IsNullOrEmpty(metadataFileName.Trim()))
            {
                throw new InvalidDataException("Argument metadataFileName is incorrect");
            }

            this.metadataFileName = metadataFileName.Trim();
        }

        public List<PackageInfo> Process(string pathToStructureFile)
        {
            //{PackageName}/{OS_Name}/tags/{Version_Name}/metadata.xml || {PackageFileName}
            //{PackageName}/{OS_Name}/trunk/metadata.xml || {PackageFileName}
            var data = File.ReadLines(pathToStructureFile).Where(x => !string.IsNullOrEmpty(x)).
                Select(x => x.Split('/').Where(y => !(string.IsNullOrEmpty(y) || string.IsNullOrWhiteSpace(y))).Select(y => y.Trim()).ToArray())
                .Where(x => ((x.Count() == 5 && x[2] == "tags") || (x.Count() == 4 && x[2] == "trunk")) && x.Last() != "metadata.xml");


            var buffer = new Dictionary<string, PackageInfo>();

            foreach (var repPath in data)
            {
                var pinfo = CommonDataTypes.Utility.Utility.GetOrCreate(buffer, repPath[0], new PackageInfo(){ PackageName = repPath[0]});

                var list = CommonDataTypes.Utility.Utility.GetOrCreate(pinfo.VersionedInstances, repPath[1],
                    new List<PackageInfo.NamedInstance>());

                var nInst = new PackageInfo.NamedInstance();
                list.Add(nInst);

                if (repPath[2] == "tags")
                {
                    nInst.VersionName = repPath[3];
                    nInst.FileName = repPath[4];
                }
                else
                {
                    nInst.VersionName = null;
                    nInst.FileName = repPath[3];
                }
            }

            return buffer.Values.ToList();
        }
    }
}
