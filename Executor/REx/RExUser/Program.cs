using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RExUser
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var service = new RExService.RExServiceClient())
            {
                int pid = service.Exec(22056);
                Console.WriteLine(pid);
            }
        }
    }
}
