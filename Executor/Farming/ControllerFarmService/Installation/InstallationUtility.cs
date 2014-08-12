using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ControllerFarmService.Installation.Interfaces;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;
using MITP;
using ServiceProxies.PackageInstallationService;
using Tamir.SharpSsh.java;
using Exception = System.Exception;

namespace ControllerFarmService.Installation
{
    static class InstallationUtility
    {
        public const string FILE_NOT_DELETED = "File wasn't deleted";
        public const string FILE_DELETED = "File was deleted";

        public const string NOT_ALLOWED_KEYS_DUPLICATION = "Not allowed to reserve address with stored key.";
        public const string LOCAL_ADDRESS_RESERVED = "Local address was reserved";
        public const string ADDRESS_CONFIRMED = "A download to local was confirmed";
        public const string ENTRY_REMOVED = "Entry was removed";
        public const string KEY_NOT_FOUND = "Given key was not found";
        public const string FILE_FOUND_BY_KEY = "the File was found";
        public const string FILE_IS_DOWNLOADING = "the file is downloading. it will be in access soon";

        public const string BACKGROUND_TEMP_CLEANER_STARTED = "Temp folder's cleaner started";
        public const string BACKGROUND_TEMP_CLEANER_STOPPED = "Temp folder's cleaner stopped";


        public static void RouteToController(ResourceCache resourceCache, InstallationTicket ticket)
        {
            var installer = resourceCache.Controller as IInstallationController;
            if (installer != null)
            {
                var message = string.Format("Installing from PackageAddress: {0}  to Resource: {1}, to Node: {2}, ticketId: {3}",
                    ticket.StorageAddress, resourceCache.Resource.ResourceName, ticket.NodeName, ticket.Id);
                Common.Utility.LogInfo(message);

                var installationManager = InstallationFactory.GetInstance().GetInstallationManager();
                installationManager.InstallByTicket(resourceCache.Resource, ticket, installer);
                return;
            }

            throw new InvalidDataException(string.Format("ResourceController for Resource: {0} doesn't support installation. TicketId: {1}", resourceCache.Resource.ResourceName, ticket.Id));
        }

        public static void ReportResult(string backAddress, TicketResult result)
        {
            if (string.IsNullOrEmpty(backAddress) || string.IsNullOrWhiteSpace(backAddress))
            {
                Common.Utility.LogWarn(string.Format("BackAddress for ticket with ticketId: {0} aren't presented"));
                return;
            }
            using (var backClient = InstallationUtility.GetCallbackServiceClient(backAddress))
            {
                backClient.ReportResult(result);
            }
        }

        public static ResultForTicketServiceClient GetCallbackServiceClient(string backAddress)
        {
            return new ResultForTicketServiceClient("BackReport", backAddress);
        }
    }
}
