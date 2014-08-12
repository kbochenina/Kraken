using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Common.VManager
{
    public interface IHost
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

        bool IsConnected
        {
            get;
        }

        DataTypes.Host CreateDescription();

        IMachine[] GetMachines();

        IMachine GetMachine(string machineName);

        bool Connect();

        bool Disconnect();
    }
}
