using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MITP;
using Renci.SshNet;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;

namespace ControllerFarmService.Controllers.Entity
{
    internal class SshConnectionPool : AConnectionPool<SshClient>
    {
        public static int DEFAULT_SSH_PORT = 22;

        public SshConnectionPool() : base()
        {
            //Console.WriteLine();
        }

        public override void CloseConnection(SshClient sshClient)
        {
            if (sshClient.IsConnected)
                sshClient.Disconnect();
        }

        public override SshClient GetNewInstance(ResourceNode node)
        {
            Log.Info("SSH: Establishing connection to node " + node.ResourceName + "." + node.NodeName);

            string url = node.Services.ExecutionUrl;
            int port = DEFAULT_SSH_PORT;
            
            if (node.Services.ExecutionUrl.Contains(":")) {
                url = node.Services.ExecutionUrl.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)[0];
                port = Int32.Parse(node.Services.ExecutionUrl.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)[1]);
            }

            // if resource asks us for password interactively:
            var interactiveAuthMethod = new KeyboardInteractiveAuthenticationMethod(node.Credentials.Username);
            interactiveAuthMethod.AuthenticationPrompt += delegate(object sender, Renci.SshNet.Common.AuthenticationPromptEventArgs e)
            {
                foreach (var prompt in e.Prompts)
                {
                    Log.Debug("Interactive request by resource node " + node.NodeName + ": '" + prompt.Request + "'");

                    if (prompt.Request.ToLowerInvariant().Contains("password"))
                    {
                        Log.Debug("Responding by password");
                        prompt.Response = node.Credentials.Password;
                    }
                }
            };

            ConnectionInfo connectionInfo;
            if (!String.IsNullOrWhiteSpace(node.Credentials.CertFile))
            {
                connectionInfo = new ConnectionInfo(url, port, node.Credentials.Username,
                    new PrivateKeyAuthenticationMethod(
                        node.Credentials.Username, 
                        new PrivateKeyFile(node.Credentials.CertFile, node.Credentials.Password)
                    )
                );
            }
            else
            {
                connectionInfo = new ConnectionInfo(url, port, node.Credentials.Username,
                    new PasswordAuthenticationMethod(node.Credentials.Username, node.Credentials.Password),
                    interactiveAuthMethod
                );
            }

            var ssh = new SshClient(connectionInfo);
            ssh.Connect();

            /*  ******* OLD WAY: *******
            var ssh = new SshClient(url, port, node.Credentials.Username, node.Credentials.Password);

            try
            {
                ssh.Connect();
            }
            catch (Renci.SshNet.Common.SshAuthenticationException authEx)
            {
                Log.Warn("Exception on connect to node " + node.NodeName + ": " + authEx.ToString());
                Log.Info("Trying KeyboardInteractive authentication for node " + node.NodeName);

                var connectionInfo = new KeyboardInteractiveConnectionInfo(url, port, node.Credentials.Username);
                connectionInfo.AuthenticationPrompt += delegate(object sender, Renci.SshNet.Common.AuthenticationPromptEventArgs e)
                {
                    foreach (var prompt in e.Prompts)
                    {
                        Log.Debug("Interactive request by resource node " + node.NodeName + ": '" + prompt.Request + "'");

                        if (prompt.Request.ToLowerInvariant().Contains("password"))
                        {
                            Log.Debug("Responding by password");
                            prompt.Response = node.Credentials.Password;
                        }
                    }
                };
                ssh = new SshClient(connectionInfo);
                ssh.Connect();
            }
            */

            return ssh;
        }
    }
}
