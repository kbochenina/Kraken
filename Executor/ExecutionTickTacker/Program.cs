using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ExecutionTickTacker
{
    class Program
    {
        const string TIME_FORMAT = "H:mm:ss.fff";
        const int TICK = 200; // minimum time to update in milliseconds
        const int TACK = 700; // maximum time to update in milliseconds

        static void Main(string[] args)
        {
            var rnd = new System.Random();

            while (true)
            {
                try
                {
                    using (var service = new ExecutionBrokerService.ExecutionBrokerServiceClient())
                    {
                        while (true)
                        {
                            Console.Write("{0} - ", DateTime.Now.ToString(TIME_FORMAT));
                            service.Update();
                            Console.WriteLine(DateTime.Now.ToString(TIME_FORMAT));

                            Thread.Sleep(rnd.Next(TICK, TACK));
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Thread.Sleep(5000);
                }
            }
        }
    }
}
