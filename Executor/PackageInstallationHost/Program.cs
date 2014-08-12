using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using PackageInstallationService;
using NLog;

namespace PackageInstallationHost
{
    class Program
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            ServiceHost packageInstallationHost = null;

            try
            {
                packageInstallationHost = StartService();
                Console.WriteLine("PackageInstallationService has started");
                Console.WriteLine("Press <ENTER> to terminate...");

                Console.WriteLine();
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine();
                Console.WriteLine("Unexpected error, press any key to terminate...");

                Console.WriteLine();
                Console.ReadLine();
            }
            finally
            {
                if (packageInstallationHost != null)
                {
                    packageInstallationHost.Close();
                }
            }
        }


        static ServiceHost StartService()
        {
            var host = new ServiceHost(typeof(PackageInstallationServiceImpl));
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
