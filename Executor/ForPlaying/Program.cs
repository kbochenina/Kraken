using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using CommonDataTypes.RExService.Service.Entity.Info;
using ServiceProxies.StatGlobalCache;

namespace ForPlaying
{
    class Program
    {
        static void Main(string[] args)
        {
           
            Ask();
            //DTSerializerTest();
        }

        private class NodeInfoEqualityComparer : IComparer<int>
        {
            public int Compare(int x, int y)
            {
                return -1*x.CompareTo(y);
            }
        }

        static void DTSerializerTest()
        {

            var mock = ProfilingSamples.createMiddleSizeMockData();

            MemoryStream stream1 = new MemoryStream();


            //Serialize the Record object to a memory stream using DataContractSerializer.
            DataContractSerializer serializer = new DataContractSerializer(typeof(Dictionary<string, Dictionary<string, List<NodeInfo>>>));
            serializer.WriteObject(stream1, mock);

           // stream1.Close();

            stream1.Position = 0;

            var deserData = (Dictionary<string, Dictionary<string, List<NodeInfo>>>)serializer.ReadObject(stream1);

            Console.WriteLine(deserData.Count);
        }
    
        static void Ask()
        {
             var client = new StatisticalServiceClient();
            Dictionary<string,Dictionary<string,List<NodeInfo>>> result = null;
            try
            {
               

                result = client.GetAllResourcesInfoStartedWith(new DateTime());
            }
            finally
            {
                client.Close();
            }
        }
    }
}
