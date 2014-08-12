using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using NetLibrary;
using PackageInstallationService.Interfaces;

namespace PackageInstallationService.Impl
{
    class SVNStorageDriver : IPackageStorageDriver
    {
        public string RepositoryAddress { get; private set; }

        public string RepositoryStructureFileAddress { get; private set; }

        public IMetadataProccessor MetadataProcessor { get; private set; }

        public IStructureFileProcessor StructureFileProcessor { get; private set; }

        public string MetadataFileName { get; private set; }

        public SVNStorageDriver(string repositoryAddress, 
                                string repositoryStructureFileAddress, 
                                IMetadataProccessor metadataProccessor, 
                                IStructureFileProcessor structureFileProcessor,
                                string metadataFileName)
        {
            RepositoryAddress = repositoryAddress;

            RepositoryStructureFileAddress = repositoryStructureFileAddress;

            MetadataProcessor = metadataProccessor;

            StructureFileProcessor = structureFileProcessor;

            MetadataFileName = metadataFileName;
            //
//            var page = client.DownloadString("http://fonhorst-C2Q:18080/svn/TestRepo/trunk/test.txt");
//            client.DownloadFile("http://fonhorst-C2Q:18080/svn/TestRepo/trunk/test.txt", "D:/testdir/dotNet-test.txt");
        }

        public List<PackageInfo> GetPackagesInfo()
        {
            List<PackageInfo> result = null;

            Common.Utility.ExceptionablePlaceWrapper(() =>
            {
                string structeFilePath = Utility.DownloadFileFromSVN(RepositoryStructureFileAddress);

                var infos = CreateInfosByStructureFile(structeFilePath);

                Utility.RemoveFile(structeFilePath);

                result = ObtainAndFillMetadata(infos);
            }, "", "GetPackagesInfo succeded");

            return result;
        }

        public string CreateDownloadPackageTicket(PackageInfo.PackageChoice choice)
        {
            return ConstructDownloadAddress(choice.PackageName, choice.OSName, choice.VersionName, choice.FileName);
        }

        private string ConstructDownloadAddress(string packageName, string OSName,  string versionName, string fileName)
        {
            return RepositoryAddress + "/" + packageName + "/" + OSName + "/" +
                   (string.IsNullOrEmpty(versionName) ? "trunk/" : "tags/" + versionName + "/") + fileName;
        }   

        private List<PackageInfo> CreateInfosByStructureFile(string structeFilePath)
        {
            return StructureFileProcessor.Process(structeFilePath);
        }

        private List<PackageInfo> ObtainAndFillMetadata(List<PackageInfo> infos)
        {
            foreach (var packageInfo in infos)
            {
                foreach (var instPair in packageInfo.VersionedInstances)
                {
                    foreach (var inst in instPair.Value)
                    {
                        var addr = new Uri(RepositoryAddress);

                        var address = RepositoryAddress + (RepositoryAddress.EndsWith("/")?"":"/") + packageInfo.PackageName + "/" + instPair.Key + "/" +
                                  (inst.VersionName == null ? "trunk" : "tags/" + inst.VersionName) +
                                  "/" + MetadataFileName;

                        string filePath = null;
                        Common.Utility.ExceptionablePlaceWrapper(() =>
                        {
                            filePath = Utility.DownloadFileFromSVN(address);    
                        }, string.Format("Attempt to get metadata by Address: {0} failed", address),"", false);

                        if (filePath == null){continue;}

                        MetadataProcessor.Process(filePath, inst);
                    }
                    
                }
            }
            return infos;
        } 

    }
}
