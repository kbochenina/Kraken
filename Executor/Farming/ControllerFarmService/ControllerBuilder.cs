using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;

namespace MITP
{
    internal class ControllerBuilder
    {
        public static IStatelessResourceController Build(Resource resource)
        {
            try
            {
                var assembly = Assembly.GetAssembly(typeof(IStatelessResourceController));
                var controllerTypes = assembly.GetExportedTypes().Where(type => type.IsClass && !type.IsAbstract && type.GetInterfaces().Contains(typeof(IStatelessResourceController)));

                var controllerType = controllerTypes.Single(t => t.Name.ToLowerInvariant() == resource.Controller.Type.ToLowerInvariant());
                var controller = (IStatelessResourceController)controllerType.GetConstructor(Type.EmptyTypes).Invoke(null);

                return controller;
            }
            catch (Exception e)
            {
                Log.Error(String.Format("Exception while building controller: {0}\n{1}",
                    e.Message, e.StackTrace
                ));
                throw new Exception("Unknown controller " + resource.Controller.Type, e);
            }
        }
    }
}