using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
using System.Net;

namespace MITP
{
    public enum AdapterType
    {
        Package,
        Machine,
        Mixed,
    }

    public abstract class AbstractAdapter : IComparable
    {
        public abstract AdapterType Type { get; }

        public abstract bool Mathces(Task task); // todo : Mathces -> Matches

        public abstract void OnStart(Task task, string ftpFolder);
        public abstract void OnManualStart(Task task, string ftpFolder);

        public abstract void OnFinish(Task task, string ftpFolder);

        #region IComparable Members

        public virtual int CompareTo(object obj)
        {
            return String.Compare(this.ToString(), obj.ToString());
        }

        #endregion
    }

    public abstract class AbstractPackageAdapter : AbstractAdapter
    {
        public override AdapterType Type
        {
            get { return AdapterType.Package; }
        }
    }

    public abstract class AbstractMachineAdapter : AbstractAdapter
    {
        public override AdapterType Type
        {
            get { return AdapterType.Machine; }
        }

        public override void OnManualStart(Task task, string ftpFolder)
        {
            OnStart(task, ftpFolder);
        }
    }
}

