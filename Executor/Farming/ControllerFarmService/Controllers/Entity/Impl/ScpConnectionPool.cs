using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;
using MITP;
using Tamir.SharpSsh;
using Renci.SshNet;

namespace ControllerFarmService.Controllers.Entity
{
    class SecureCopier
    {
        private static int DEFAULT_SCP_PORT = 22;

        // Madness?! THIS IS SPARTAAAAAAAAAAAA!!!!!!!!!!!!!
        private Renci.SshNet.SftpClient _sftpClient;
        private Tamir.SharpSsh.Scp _scp;

        public SecureCopier(ResourceNode node)
        {
            Log.Info("SCP: Establishing connection to node " + node.ResourceName + "." + node.NodeName);

            var nodeAddress = node.NodeAddress; // todo : OR ExecutionUrl????!!!!!
            var addrParts = node.NodeAddress.Split(':');

            int port = DEFAULT_SCP_PORT;

            if (addrParts.Length == 2)
            {
                int.TryParse(addrParts[1], out port);
                nodeAddress = addrParts[0];
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
                connectionInfo = new ConnectionInfo(nodeAddress, port, node.Credentials.Username,
                    new PrivateKeyAuthenticationMethod(
                        node.Credentials.Username,
                        new PrivateKeyFile(node.Credentials.CertFile, node.Credentials.Password)
                    )
                );
            }
            else
            {
                connectionInfo = new ConnectionInfo(nodeAddress, port, node.Credentials.Username,
                    new PasswordAuthenticationMethod(node.Credentials.Username, node.Credentials.Password),
                    interactiveAuthMethod
                );
            }

            try
            {
                _sftpClient = new SftpClient(connectionInfo);
                _sftpClient.Connect();

                _scp = null;
            }
            catch (Exception e)
            {
                Log.Warn("Unable to use sftp. Rolling bask to SCP for resource node " + node.ResourceName + "." + node.NodeName + ": " + e.ToString());
                _sftpClient = null;

                _scp = new Scp(nodeAddress, node.Credentials.Username, node.Credentials.Password);
                _scp.Connect(port);
            }
        }

        public void Disconnect()
        {
            if (_sftpClient != null)
                _sftpClient.Disconnect();

            if (_scp != null)
                _scp.Close();
        }

        public void UploadFile(ResourceNode node, string remotePath, string localPath)
        {
            if (_sftpClient != null)
            {
                using (var stream = File.OpenRead(localPath))
                {
                    _sftpClient.UploadFile(stream, remotePath);
                }
            }
            else
            if (_scp != null)
            {
                _scp.Put(localPath, remotePath);
            }
            else
                throw new Exception("No connection established to node for copy");

            //scp.Upload(new FileInfo(localPath), remotePath);
            //scp.ConnectionInfo.ChannelRequests.
            //scp.Upload(new FileInfo(localPath), "tmp/" + Path.GetFileName(remotePath));
        }

        public void DownloadFile(ResourceNode node, string remotePath, string localPath)
        {
            if (_sftpClient != null)
            {
                using (var stream = File.OpenWrite(localPath))
                {
                    _sftpClient.DownloadFile(remotePath, stream);
                }

                //scp = _scpPool.GetSshSession(true, node);
                //scp.Recursive = recursive;
                //scp.Download(remotePath, new FileInfo(localPath));
            }
            else
            if (_scp != null)
            {
                _scp.Get(remotePath, localPath);
            }
            else
                throw new Exception("No connection established to node for copy");
        }
    }
    

    class CopierConnectionPool : AConnectionPool<SecureCopier/*SftpClient/*ScpClient*/>
    {
        public override void CloseConnection(/*ScpClient*SftpClient*/SecureCopier session)
        {
            session.Disconnect();
        }

        public override /*ScpClient*SftpClient*/SecureCopier GetNewInstance(ResourceNode node)
        {
            return new SecureCopier(node);
        }
    }       
}



    /*
    class ScpConnectionPool : AConnectionPool<Scp>
    {
        public static int DEFAULT_SCP_PORT = 22;

        public override void CloseConnection(Scp session)
        {
            session.Close();
        }

        public override Scp GetNewInstance(ResourceNode node)
        {
            Log.Info("SCP: Establishing connection to node " + node.ResourceName + "." + node.NodeName);

            var nodeAddress = node.NodeAddress;
            var addrParts = node.NodeAddress.Split(':');

            int port = DEFAULT_SCP_PORT;

            if (addrParts.Length == 2)
            {
                int.TryParse(addrParts[1], out port);
                nodeAddress = addrParts[0];
            }

            var scp = new Scp(nodeAddress, node.Credentials.Username, node.Credentials.Password);

            scp.Connect(port);

            return scp;
        }
    }
    */


                    #region SCP Tamir
        /*
        protected void ScpCopy(ResourceNode node, string remotePath, string localPath, bool recursive = true)
        {
            Ssh.Scp scp = null;

            try
            {
                scp = _scpPool.GetSshSession(true, node);
                scp.Recursive = recursive;
                scp.Put(localPath, remotePath);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                if (scp != null)
                    _scpPool.PushSession(scp);
            }
        }

        protected void ScpGet(ResourceNode node, string remotePath, string localPath, bool recursive = true)
        {
            Ssh.Scp scp = null;

            try
            {
                scp = _scpPool.GetSshSession(true, node);
                scp.Recursive = recursive;
                scp.Get(remotePath, localPath);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                if (scp != null)
                    _scpPool.PushSession(scp);
            }
        }
        */
        #endregion

