using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Description;
using RExService;

namespace RExHost
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var host = new ServiceHost(typeof(RExService.RExService) ))//, new Uri("http://localhost:5555")))
            {
                try
                {
                    /*
                    host.AddServiceEndpoint(typeof(IRExService), new WSHttpBinding(), "");
                    var smb = new ServiceMetadataBehavior();
                    smb.HttpGetEnabled = true;
                    host.Description.Behaviors.Add(smb);
                    */
                    
                    host.Open();

                    Console.WriteLine("Service started at {0}", host.BaseAddresses.First());
                    Console.WriteLine("Press <ENTER> to terminate...");
                    Console.WriteLine();
                    Console.ReadLine();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                    Console.WriteLine();
                }
                finally
                {
                    host.Close();
                }
            }
        }
    }
}
