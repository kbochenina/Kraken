using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VMIntegrationTests
{
    class Program
    {
        static void Main(string[] args)
        {
            //CheckDirectory();
            Scenarios.RunVmAndInstallSoftware();
        }

        static void CheckDirectory()
        {
            string host = "192.168.0.87";
            int port = 22;
            string username = "nano";
            string password = "Yt1NyDpQNm";

            var command = string.Format("cd {0}; ls | wc -l;", "/home/nano/installation");
            var result = Utility.SshExec(host, port, username, password, command);
        }
    }
}
