using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using NLog;
using StatisticsCacheService;

namespace StatisticCacheHost
{
    class Program
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            ServiceHost cacheHost = null;

            try
            {

                cacheHost = StartCacheService();
                Console.WriteLine("Press <ENTER> to terminate...");

                Console.WriteLine();
                Console.ReadLine();
            }
            finally
            {
                if (cacheHost != null)
                {
                    cacheHost.Close();
                }
            }

        }

       
        static ServiceHost StartCacheService()
        {
            var host = new ServiceHost(typeof(StatisticalGlobalCacheServiceImpl));
            StartHost(host);
            return host;
        }

       

        private static void StartHost(ServiceHost host)
        {
            try
            {
                host.Open();
                Log.Info("Service started at {0}", host.BaseAddresses.First());
            }
            catch (Exception e)
            {
                Log.Info((e.Message));
            }
        }
    }
}
