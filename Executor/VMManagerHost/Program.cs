using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMServiceInterlayerManager.Debug;

namespace VMManagerHost
{
    class Program
    {
        static void Main(string[] args)
        {
            Debugging();
        }

        static void Debugging()
        {
           VMLauncher.RunVBox();
            //VMLauncher.AddToResourceBase();
            //VMLauncher.RemoveFromResourceBase();
        }
    }
}
