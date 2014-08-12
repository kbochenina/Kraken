using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Configuration;
using System.Text;
using ServiceProxies.StatGlobalCache;

namespace ArchiveFileDownloader
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var client = GetArchiveClient())
            {
                var startDate = new DateTime(2013, 9, 3, 12, 22, 0);
                var endDate = new DateTime(2013, 9, 3, 12, 23, 0);
                var ticket = client.GetAllResourcesInfoBetween(startDate, endDate);



                foreach (var fileName in ticket.FileNames)
                {
                    Stream file = null;
                    try
                    {
                        file = client.GetArchiveFile(fileName);
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }


                    if (!file.CanRead)
                    {
                        throw new Exception("Failed to Read");
                    }
                }

                Console.WriteLine("Please press any key to exit...");
                Console.ReadLine();
            }

            
        }

        private static ArchiveFilesServiceClient GetArchiveClient()
        {
            return new ArchiveFilesServiceClient(new BasicHttpBinding(){ MaxReceivedMessageSize = 26214400,
                                                                        MaxBufferSize = 26214400 }, 
                                                 new EndpointAddress("http://localhost:8750/StatisticCacheService/FilesArchive"));
        }
    }
}
