using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Configuration;
using ServiceProxies.ResourceBaseService;
using Config = System.Web.Configuration.WebConfigurationManager;

namespace MITP
{
    public abstract class Discovery
	{
        private const string STORAGE_CONFIG_NAME = "Storage";

        // todo : [!] Get URL's from Discovery service

        public static ServiceProxies.ResourceBaseService.ResourceBaseServiceClient
            GetResourceBase()
        {
            var service = new ServiceProxies.ResourceBaseService.ResourceBaseServiceClient();
            return service;
        }

        public static ServiceProxies.ControllerFarmService.ControllerFarmServiceClient
            GetControllerFarm(string url)
        {
            Log.Debug("Getting controller with URL = " + url);
            var service = new ServiceProxies.ControllerFarmService.ControllerFarmServiceClient();
            service.Endpoint.Address = new EndpointAddress(url);
            return service;
        }

        public static ServiceProxies.ControllerFarmService.ControllerFarmServiceClient
            GetControllerFarm(Resource resource)
        {
            return GetControllerFarm(resource.Controller.Url);
        }

        public static ServiceProxies.UserManagementService.UmClientClient
            GetRightsService()
        {
            return new ServiceProxies.UserManagementService.UmClientClient();
        }

        public static ServiceProxies.AccountingService.AccountingServiceClient
            GetAccountingService()
        {
            return new ServiceProxies.AccountingService.AccountingServiceClient();
        }

        public static ServiceProxies.ClustersService.IntegratorServiceSoapClient 
            GetClustersService(string url = null)
		{
            /*
            if (String.IsNullOrEmpty(url))
                url = CONST.EntryPointUrl;

            var service = new ServiceProxies.ClustersService.IntegratorServiceSoapClient();
            service.Endpoint.Address = new EndpointAddress(GetEntryPointService(url).GetServiceUri(ServiceNames.Integrator.ToString()));
            */

            return new ServiceProxies.ClustersService.IntegratorServiceSoapClient();
		}

        public static ServiceProxies.IOService.NanoIOSystemAPIInterfaceClient GetIOService(string url = null)
		{
            /*
            if (String.IsNullOrEmpty(url))
                url = CONST.EntryPointUrl;
            
            string ioServiceUrl = GetEntryPointService(url).GetServiceUri(ServiceNames.Data.ToString());
            var address = new EndpointAddress(ioServiceUrl);
            */

            
            var address = new EndpointAddress(Config.AppSettings[STORAGE_CONFIG_NAME]);

			var mtomBind = new MtomMessageEncodingBindingElement();
			mtomBind.MessageVersion = MessageVersion.Soap11;
			var httpBind = new HttpTransportBindingElement();
			var binding = new CustomBinding(new BindingElement[] { mtomBind, httpBind });

			var service = new ServiceProxies.IOService.NanoIOSystemAPIInterfaceClient(binding, address);
			return service;
		}

        public static ServiceProxies.EventingService.EventingBrokerServiceClient GetEventingService(string url = null)
        {
            return new ServiceProxies.EventingService.EventingBrokerServiceClient();
        }

        /*public static ServiceProxies.RExService.RExServiceClient GetREx(string rexUrl)
        {
            var service = new ServiceProxies.RExService.RExServiceClient();
            service.Endpoint.Address = new EndpointAddress(rexUrl);
            return service;
        }*/

        public static ServiceProxies.SchedulerService.SchedulerServiceClient GetSchedulerService(string url = null)
        {
            var service = new ServiceProxies.SchedulerService.SchedulerServiceClient();
            return service; 
        }
	}
}
