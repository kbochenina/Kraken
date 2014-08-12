using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using Easis.PackageBase;
using Easis.PackageBase.Client;
using Easis.PackageBase.Engine;
using Easis.PackageBase.Definition;
using Easis.PackageBase.Types;

namespace MITP
{
    [DataContract]
    public class PackageEngineState : ICloneable
    {
        [DataMember] public PackageEngineContext EngineContext { get; private set; }
        [DataMember] internal string StoragePathBase { get; private set; }

        public CompiledModeDef CompiledDef { get; private set; }
        internal TaskDescription _taskDescription { get; set; }

        public void Init(TaskDescription taskDescription, string storagePathBase)
        {
            StoragePathBase = storagePathBase;

            if (_taskDescription == null)
                _taskDescription = new TaskDescription(taskDescription);

            if (CompiledDef == null)
                CompiledDef = PackageBaseProxy.GetCompiledDef(taskDescription.Package);

            if (EngineContext == null)
            {
                var engine = new PackageEngine(CompiledDef);
                EngineContext = engine.Ctx;
            }
        }

        public PackageEngineState(TaskDescription taskDescription, string storagePathBase)
        {
            Init(taskDescription, storagePathBase);
        }

        private PackageEngineState()
        {
        }

        #region ICloneable Members

        public object Clone()
        {
            var clonedState = new PackageEngineState();

            /**/
            if (this.CompiledDef != null)
                clonedState.CompiledDef = this.CompiledDef;  // immutable
                //clonedState.CompiledDef = (CompiledModeDef) this.CompiledDef.Clone();
            /**/

            if (this.EngineContext != null)
                clonedState.EngineContext = (PackageEngineContext) this.EngineContext.Clone();

            clonedState.Init(this._taskDescription, this.StoragePathBase);

            return clonedState;
        }

        #endregion
    }
}