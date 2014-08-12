using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace MITP
{
    [ServiceContract]
    public interface IResourceBaseService
    {
        [OperationContract]
        Resource[] GetAllResources();

        [OperationContract]
        string[] GetResourceNames();

        [OperationContract]
        Resource GetResourceByName(string resourceName);

        [OperationContract]
        string[] GetNodeNames(string resourceName);

        [OperationContract]
        ResourceNode GetResourceNodeByName(string resourceName, string nodeName);

        [OperationContract]
        Resource[] GetResourcesForFarm(string farmId = null, string dumpingKey = null);

        [OperationContract]
        void AddInstalledPackage(string resourceName, string nodeName, PackageOnNode pack);

        [OperationContract]
        void RemoveInstalledPackage(string resourceName, string nodeName, string packName);

        [OperationContract]
        void AddNewNodeToResource(string resourceName, ResourceNode node);

        [OperationContract]
        void RemoveNodeFromResource(string resourceName, string nodeName);
    }
}
