using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Common.VManager
{
    public interface IMachine
    {
        string Name
        {
            get;
        }

        string Description
        {
            get;
        }

        string Type
        {
            get;
        }

        DataTypes.MachineState State
        {
            get;
        }

        DataTypes.Machine CreateDescription();

        bool IsGuestOSRunning
        {
            get;
        }

        bool Start(Dictionary<string, string> parameters);

        bool Restart();

        bool Suspend();

        bool Wake();

        void Stop();
    }
}
