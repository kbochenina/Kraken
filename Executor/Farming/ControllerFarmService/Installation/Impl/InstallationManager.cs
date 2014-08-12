using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ControllerFarmService.Installation.Interfaces;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;
using ServiceProxies.PackageInstallationService;

namespace ControllerFarmService.Installation.Impl
{
    class InstallationManager : IInstallationManager
    {
        private IPackageObtainer _packageObtainer;

        public InstallationManager(IPackageObtainer packageObtainer)
        {
            _packageObtainer = packageObtainer;
        }
        
        public void InstallByTicket(Resource resource, InstallationTicket ticket, IInstallationController controller)
        {
            var nodes = resource.Nodes.Where(x => x.NodeName == ticket.NodeName);

            if (nodes.Count() > 1)
            {
                var message = string.Format("Several nodes was founded with given NodeName: {0}. Use first. TicketId: {1} ", ticket.NodeName, ticket.Id);
                Common.Utility.LogWarn(message);
            }
            else if (!nodes.Any())
            {
                var message = string.Format("Node with given NodeName: {0} wasn't founded. TicketId: {1}", ticket.NodeName, ticket.Id);
                Common.Utility.LogError(message);
                ReportResult(ticket, new InvalidDataException(message));
            }

            var node = nodes.First();

            Task.Factory.StartNew(() =>
            {
                Install(ticket,node,controller);
            });
        }

        private void Install(InstallationTicket ticket, ResourceNode node, IInstallationController controller)
        {
            ShouldReportIfError(ticket, () =>
            {
                _packageObtainer.ObtainPackage(ticket.StorageAddress, (address, error) =>
                {
                    if (error != null)
                    {
                        ReportResult(ticket, error);
                        return;
                    }

                    ShouldReport(ticket, () =>
                    {
                        controller.InstallByTicket(ticket, node, address);
                    });

                });
            });
        }


        private void ReportResult(InstallationTicket ticket, Exception error)
        {
            Common.Utility.ExceptionablePlaceWrapper(() =>
            {
               InstallationUtility.ReportResult(ticket.BackAddress,createResult(ticket,error));
            }, string.Format("Failed to report, BackAddress: {0}", ticket.BackAddress),
               string.Format("Succeseded to report, BackAddress: {0}", ticket.BackAddress), false);
        }

        private TicketResult createResult(InstallationTicket ticket, Exception error)
        {
            if (error != null)
            {
                Common.Utility.LogError(string.Format(" Error occured while trying to install package. PackageAddress: {0},  ResourceName: {1}, NodeName: {2}, Exception: {3}, ticketId: {4}",
                                                               ticket.StorageAddress, ticket.ResourceName, ticket.NodeName, error, ticket.Id));
                return new TicketResult()
                {
                    IsSuccessful = false,
                    OccuredException =
                        new Exception(
                            "Installation to node: " + ticket.NodeName +
                            " was failed"),
                    TicketId = ticket.Id,
                    FolderToInstall = ticket.FolderToInstall,
                    PackageName = ticket.PackageName,
                    VersionName = ticket.VersionName,
                    ResourceName = ticket.ResourceName,
                    NodeName = ticket.NodeName
                };
            }
            else
            {
                Common.Utility.LogInfo(string.Format(" Installation was succesful. PackageAddress: {0}, ResourceName: {1}, NodeName: {2}, ticketId: {3}",
                                                                ticket.StorageAddress, ticket.ResourceName, ticket.NodeName, ticket.Id));
                return new TicketResult()
                {
                    IsSuccessful = true,
                    OccuredException = null,
                    TicketId = ticket.Id,
                    FolderToInstall = ticket.FolderToInstall,
                    PackageName = ticket.PackageName,
                    VersionName = ticket.VersionName,
                    ResourceName = ticket.ResourceName,
                    NodeName = ticket.NodeName
                };
            }
        }

        private void ErrorWrap(InstallationTicket ticket, bool onlyIfError, Action code)
        {
            Exception exception = null;
            try
            {
                code();
            }
            catch (Exception ex)
            {
                Common.Utility.LogError(
                        string.Format("Exception occured while installing to node:{0}", ticket.NodeName), ex);
                exception = ex;
            }
            finally
            {
                if (!onlyIfError || exception != null)
                {
                    ReportResult(ticket, exception);
                }
            }
        }

        private void ShouldReport(InstallationTicket ticket, Action code)
        {
            ErrorWrap(ticket, false, code);
        }

        private void ShouldReportIfError(InstallationTicket ticket, Action code)
        {
            ErrorWrap(ticket, true, code);
        }
    }
}
