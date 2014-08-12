using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MITP;
using Renci.SshNet;
//using ControllerFarmService.ResourceBaseService;
using ServiceProxies.ResourceBaseService;
using Config = System.Configuration.ConfigurationManager;

namespace ControllerFarmService.Controllers.Entity
{
    public abstract class AConnectionPool<T>
    {
        public static string MAX_SESSION_AMOUNT = "DefaultMaxSession";
        public static readonly TimeSpan SESSION_TIMEOUT = TimeSpan.FromMinutes(3);
        
        ConcurrentStack<T> sshSessions = new ConcurrentStack<T>();
        private IDictionary<T, DateTime> whenConnected = new ConcurrentDictionary<T, DateTime>();
        private int _maxSessionAmount = 2;
        private Object _lockOb = new Object();
        private bool _usedLock;
        private int _currentAmount;

        protected AConnectionPool()
        {
            String amount; 
            if ((amount = Config.AppSettings[MAX_SESSION_AMOUNT]) != null)
            {
                int value;
                if (int.TryParse(amount, out value))
                {
                    _maxSessionAmount = value;
                }
            }
        }

         ~AConnectionPool()
        {
            while (!sshSessions.IsEmpty)
            {
                T session;
                if (sshSessions.TryPop(out session))
                {
                    CloseConnection(session);   
                }
            }
            
        }
        
        public abstract void CloseConnection(T session);

        public abstract T GetNewInstance(ResourceNode node); 

        public T GetSshSession(bool waitUntilFree, ResourceNode node)
        {
            do
            {
                if (!sshSessions.IsEmpty)
                {
                    T ssh;
                    if (sshSessions.TryPop(out ssh))
                    {
                        if (whenConnected[ssh] + SESSION_TIMEOUT < DateTime.Now)
                        {
                            try
                            {
                                CloseConnection(ssh);
                            }
                            catch (Exception closeEx)
                            {
                                Log.Error(String.Format(
                                    "Could not close old connection to {0}.{1}: {2}",
                                    node.ResourceName, node.NodeName,
                                    closeEx
                                ));
                            }
                            finally
                            {
                                whenConnected.Remove(ssh);
                            }

                            ssh = GetNewInstance(node);
                            whenConnected[ssh] = DateTime.Now;
                        }

                        return ssh;
                    }
                }

                var lockHolder = false;
                if (_currentAmount < _maxSessionAmount)
                {
                    lock (_lockOb) 
                    {
                        if (_currentAmount < _maxSessionAmount && !_usedLock)
                        {
                            _usedLock = true;
                            lockHolder = true;
                        }
                    }

                    if (lockHolder)
                    {
                        T ssh;
                        try
                        {
                            ssh = GetNewInstance(node);
                            whenConnected[ssh] = DateTime.Now;
                            _currentAmount++;
                        }
                        catch
                        {
                            throw;
                        }
                        finally
                        {
                            _usedLock = false;
                        }

                        return ssh;
                    }
                } 
                if (waitUntilFree)
                {
                    Console.WriteLine(DateTime.Now.ToString() +  ": waiting for connection in pool for node " + node.ResourceName + "." + node.NodeName);
                    Thread.Sleep(300);    
                }
            } while (waitUntilFree);
            
            return default(T);
        }

        public void PushSession(T ssh)
        {            
            sshSessions.Push(ssh);
        }
    }


}
