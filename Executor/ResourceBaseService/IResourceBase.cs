using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MITP;

namespace MITP
{
    interface IResourceBase
    {
        Resource[] GetAllResources(string userId);

        string[] GetResourceNames(string userId);

        Resource GetResourceByName(string resourceName, string userId);

        string[] GetNodeNames(string resourceName, string userId);

        ResourceNode GetResourceNodeByName(string resourceName, string nodeName, string userId);

        Resource[] GetResourcesForFarm(string userId, string farmId = null, string dumpingKey = null);

        void AddInstalledPackage(string resourceName, string nodeName, PackageOnNode pack, string userId);

        void RemoveInstalledPackage(string resourceName, string nodeName, string packName, string userId);

        bool SaveResource(string resourceDesc, string resourceName, string userId);

        bool DeleteResource(string resourceName, string userId);

        bool IsResourceAlreadyExisted(string resourceName, string userId);

        void AddNewNodeToResource(string resourceName, ResourceNode node, string userId);

        void RemoveNodeFromResource(string resourceName, string nodeName, string userId);
    }
}
