using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace RExService
{
    [ServiceContract]
    public interface IRExService
    {
        [OperationContract]
        int Exec(ulong taskId);

        [OperationContract]
        bool IsProcessRunning(int pid);
    }
}
