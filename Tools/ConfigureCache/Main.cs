// Copyright (c) 2015 Alachisoft
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Alachisoft.NCache.Common.Configuration;
using Alachisoft.NCache.Config.Dom;
using Alachisoft.NCache.Config.NewDom;
using Alachisoft.NCache.Management;
using Alachisoft.NCache.Management.ClientConfiguration;
using Alachisoft.NCache.Management.Management.Util;
using Alachisoft.NCache.Management.ServiceControl;
using Alachisoft.NCache.Tools.Common;
using CacheServer = Alachisoft.NCache.Management.ClientConfiguration.Dom.CacheServer;
using CacheServerConfig = Alachisoft.NCache.Config.NewDom.CacheServerConfig;
using Channel = Alachisoft.NCache.Config.NewDom.Channel;
using Cluster = Alachisoft.NCache.Config.NewDom.Cluster;

namespace Alachisoft.NCache.Tools.CreateCache
{
    internal class Application
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                ConfigureCacheTool.Run(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    /// <summary>
    ///     Summary description for ConfigureCacheTool.
    /// </summary>
    public class ConfigureCacheParam : CommandLineParamsBase
    {
        private const string LOCAL_TOPOLOGY_NAME = "local";
        private const string PARTITIONED_TOPOLOGY_NAME = "partitioned";
        private const string REPLICATED_TOPOLOGY_NAME = "replicated";
        private int _port = -1;
        private string _repStrategy = "async";
        private string _topology = string.Empty;


        [Argument("", "")]
        public string CacheId { get; set; } = string.Empty;

        [Argument(@"/S", @"/cache-size")]
        public long CacheSize { get; set; } = 1024;

        [Argument(@"/i", @"/interval")]
        public int CleanupInterval { get; set; } = -1;

        [Argument(@"/C", @"/cluster-port")]
        public int ClusterPort { get; set; } = -1;

        [Argument(@"/d", @"/def-priority")]
        public string DefaultPriority { get; set; } = string.Empty;

        [Argument(@"/y", @"/evict-policy")]
        public string EvictionPolicy { get; set; } = string.Empty;


        public string FileName { get; set; } = string.Empty;

        [Argument(@"/I", @"/inproc", false)]
        public bool IsInProc { get; set; } = false;

        [Argument(@"/T", @"/path")]
        public string Path { get; set; } = string.Empty;

        [Argument(@"/p", @"/port")]
        public int Port
        {
            get => _port;
            set
            {
                if (_port < 1)
                {
                    throw new ArgumentException("Invalid port(/p) value: cannot be less than 0");
                }
                _port = value;
            }
        }

        [Argument(@"/o", @"/ratio")]
        public decimal Ratio { get; set; } = -1;

        [Argument(@"/s", @"/server")]
        public string Server { get; set; } = string.Empty;

        [Argument(@"/t", @"/topology")]
        public string Topology
        {
            get { return _topology; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (IsValidTopologyName(value))
                    {
                        _topology = value;
                    }
                    else
                    {
                        throw new ArgumentException("Invalid topology name(/t)");
                    }
                }
                else
                {
                    throw new ArgumentException("Topology name(/t) not specified");
                }
            }
        }


        private static bool IsValidTopologyName(string topology)
        {
            var topologyList = new ArrayList();

            topologyList.Add(PARTITIONED_TOPOLOGY_NAME);
            topologyList.Add(REPLICATED_TOPOLOGY_NAME);

            topologyList.Add(LOCAL_TOPOLOGY_NAME);

            if (topologyList.Contains(topology.ToLower()))
            {
                return true;
            }
            return false;
        }
    }

    internal sealed class ConfigureCacheTool
    {
        private static readonly CacheServerConfig _SimpleCacheConfig = new CacheServerConfig();

        private static ConfigureCacheParam ccParam = new ConfigureCacheParam();
        private static readonly NCacheRPCService NCache = new NCacheRPCService("");

        public static ArrayList GetServers(string servers)
        {
            var serverList = new ArrayList();
            var st = servers.Split(',');
            for (var i = 0; i < st.Length; i++)
            {
                serverList.Add(new ServerNode(st[i]));
            }
            return serverList;
        }

        /// <summary>
        ///     The main entry point for the tool.
        /// </summary>
        public static void Run(string[] args)
        {
            var failedNodes = string.Empty;
            NCache.Port = NCache.UseTcp ? CacheConfigManager.NCacheTcpPort : CacheConfigManager.HttpPort;
            CacheServerConfig[] caches = null;
            ICacheServer cacheServer = null;
            CacheServerConfig _cacheConfig = null;

            try
            {
                object param = new ConfigureCacheParam();
                CommandLineArgumentParser.CommandLineParser(ref param, args);
                ccParam = (ConfigureCacheParam) param;

                if (ccParam.IsUsage)
                {
                    AssemblyUsage.PrintLogo(ccParam.IsLogo);
                    AssemblyUsage.PrintUsage();
                    return;
                }

                if (!ValidateParameters())
                {
                    return;
                }

                if (ccParam.Port != -1)
                {
                    NCache.Port = ccParam.Port;
                }

                if (ccParam.Port == -1)
                {
                    NCache.Port = NCache.UseTcp ? CacheConfigManager.NCacheTcpPort : CacheConfigManager.HttpPort;
                }

                if (ccParam.Path != null && ccParam.Path != string.Empty)
                {
                    if (Path.HasExtension(ccParam.Path))
                    {
                        var extension = Path.GetExtension(ccParam.Path);

                        if (!extension.Equals(".ncconf") && !extension.Equals(".xml"))
                        {
                            throw new Exception("Incorrect file format. Only .ncconf and .xml are supported.");
                        }
                    }
                    else
                    {
                        throw new Exception("Incorrect configuration file path specified.");
                    }

                    var builder = new ConfigurationBuilder(ccParam.Path);
                    builder.RegisterRootConfigurationObject(typeof(CacheServerConfig));
                    builder.ReadConfiguration();

                    if (builder.Configuration != null)
                    {
                        caches = new CacheServerConfig[builder.Configuration.Length];
                        builder.Configuration.CopyTo(caches, 0);
                    }
                    else
                    {
                        throw new Exception("Configuration cannot be loaded.");
                    }
                    var validator = new ConfigurationValidator();
                    var _isConfigValidated = validator.ValidateConfiguration(caches);

                    _cacheConfig = caches[0];

                    if (_cacheConfig.CacheSettings.Name == null)
                    {
                        _cacheConfig.CacheSettings.Name = ccParam.CacheId;
                    }

                    if (_cacheConfig.CacheSettings.Storage == null || _cacheConfig.CacheSettings.Storage.Size == -1)
                    {
                        throw new Exception("Cache size is not specified.");
                    }

                    if (_cacheConfig.CacheSettings.EvictionPolicy == null)
                    {
                        _cacheConfig.CacheSettings.EvictionPolicy = new EvictionPolicy();
                        _cacheConfig.CacheSettings.EvictionPolicy.Policy = "priority";
                        _cacheConfig.CacheSettings.EvictionPolicy.DefaultPriority = "normal";
                        _cacheConfig.CacheSettings.EvictionPolicy.EvictionRatio = 5;
                        _cacheConfig.CacheSettings.EvictionPolicy.Enabled = true;
                    }

                    if (_cacheConfig.CacheSettings.Cleanup == null)
                    {
                        _cacheConfig.CacheSettings.Cleanup = new Cleanup();
                        _cacheConfig.CacheSettings.Cleanup.Interval = 15;
                    }

                    if (_cacheConfig.CacheSettings.Log == null)
                    {
                        _cacheConfig.CacheSettings.Log = new Log();
                    }

                    if (_cacheConfig.CacheSettings.PerfCounters == null)
                    {
                        _cacheConfig.CacheSettings.PerfCounters = new PerfCounters();
                        _cacheConfig.CacheSettings.PerfCounters.Enabled = true;
                    }

                    if (_cacheConfig.CacheSettings.CacheType == "clustered-cache")
                    {
                        if (_cacheConfig.CacheSettings.CacheTopology.ClusterSettings == null)
                        {
                            throw new Exception("Cluster settings not specified for the cluster cache.");
                        }

                        if (_cacheConfig.CacheSettings.CacheTopology.ClusterSettings.Channel == null)
                        {
                            throw new Exception("Cluster channel related settings not specified for cluster cache.");
                        }

                        if (_cacheConfig.CacheSettings.CacheTopology.ClusterSettings.Channel.TcpPort == -1)
                        {
                            throw new Exception("Cluster port not specified for cluster cache.");
                        }
                    }
                }
                else
                {
                    _SimpleCacheConfig.CacheSettings = new CacheServerConfigSetting();
                    _SimpleCacheConfig.CacheSettings.Name = ccParam.CacheId;
                    _SimpleCacheConfig.CacheSettings.Storage = new Storage();
                    _SimpleCacheConfig.CacheSettings.EvictionPolicy = new EvictionPolicy();
                    _SimpleCacheConfig.CacheSettings.Cleanup = new Cleanup();
                    _SimpleCacheConfig.CacheSettings.Log = new Log();
                    _SimpleCacheConfig.CacheSettings.PerfCounters = new PerfCounters();
                    _SimpleCacheConfig.CacheSettings.PerfCounters.Enabled = true;
                    _SimpleCacheConfig.CacheSettings.Storage.Type = "heap";
                    _SimpleCacheConfig.CacheSettings.Storage.Size = ccParam.CacheSize;
                    _SimpleCacheConfig.CacheSettings.EvictionPolicy.Policy = "priority";
                    _SimpleCacheConfig.CacheSettings.EvictionPolicy.DefaultPriority = "normal";
                    _SimpleCacheConfig.CacheSettings.EvictionPolicy.EvictionRatio = 5;
                    _SimpleCacheConfig.CacheSettings.EvictionPolicy.Enabled = false;
                    _SimpleCacheConfig.CacheSettings.Cleanup.Interval = 15;
                    _SimpleCacheConfig.CacheSettings.CacheTopology = new CacheTopology();

                    if (string.IsNullOrEmpty(ccParam.Topology))
                    {
                        _SimpleCacheConfig.CacheSettings.CacheTopology.Topology = "Local";
                    }
                    else
                    {
                        _SimpleCacheConfig.CacheSettings.CacheTopology.Topology = ccParam.Topology;
                    }

                    if (ccParam.IsInProc && _SimpleCacheConfig.CacheSettings.CacheTopology.Topology.Equals("local-cache"))
                    {
                        _SimpleCacheConfig.CacheSettings.InProc = true;
                    }


                    if (_SimpleCacheConfig.CacheSettings.CacheType == "clustered-cache")
                    {
                        _SimpleCacheConfig.CacheSettings.CacheTopology.ClusterSettings = new Cluster();
                        _SimpleCacheConfig.CacheSettings.CacheTopology.ClusterSettings.Channel = new Channel();

                        _SimpleCacheConfig.CacheSettings.CacheTopology.ClusterSettings.Channel.TcpPort = ccParam.ClusterPort;
                        _SimpleCacheConfig.CacheSettings.CacheTopology.ClusterSettings.StatsRepInterval = 600;
                        if (_SimpleCacheConfig.CacheSettings.CacheTopology.Topology == "partitioned-replica")
                        {
                            _SimpleCacheConfig.CacheSettings.CacheTopology.ClusterSettings.Channel.PortRange = 2;
                        }
                    }

                    if (ccParam.EvictionPolicy != null && ccParam.EvictionPolicy != string.Empty)
                    {
                        _SimpleCacheConfig.CacheSettings.EvictionPolicy.Policy = ccParam.EvictionPolicy;
                        _SimpleCacheConfig.CacheSettings.EvictionPolicy.Enabled = true;
                    }

                    if (ccParam.Ratio != -1)
                    {
                        _SimpleCacheConfig.CacheSettings.EvictionPolicy.EvictionRatio = ccParam.Ratio;
                    }

                    if (ccParam.CleanupInterval != -1)
                    {
                        _SimpleCacheConfig.CacheSettings.Cleanup.Interval = ccParam.CleanupInterval;
                    }

                    if (ccParam.DefaultPriority != null && ccParam.DefaultPriority != string.Empty)
                    {
                        _SimpleCacheConfig.CacheSettings.EvictionPolicy.DefaultPriority = ccParam.DefaultPriority;
                        _SimpleCacheConfig.CacheSettings.EvictionPolicy.Enabled = true;
                    }
                    _cacheConfig = _SimpleCacheConfig;
                }
                try
                {
                    _cacheConfig.CacheSettings.Name = ccParam.CacheId;

                    if (_cacheConfig.CacheSettings.CacheType == "clustered-cache")
                    {
                        if (_cacheConfig.CacheDeployment == null)
                        {
                            _cacheConfig.CacheDeployment = new CacheDeployment();
                            _cacheConfig.CacheDeployment.Servers = new ServersNodes();
                        }
                        _cacheConfig.CacheDeployment.Servers.NodesList = GetServers(ccParam.Server);
                    }

                    var serverList = new Dictionary<int, CacheServer>();
                    var serverCount = 0;
                    foreach (ServerNode node in GetServers(ccParam.Server))
                    {
                        var tempServer = new CacheServer();
                        tempServer.ServerName = node.IP;
                        serverList.Add(serverCount, tempServer);
                        serverCount++;
                    }
                    var servers = new CacheServerList(serverList);
                    var serversToUpdate = new List<string>();
                    foreach (ServerNode node in GetServers(ccParam.Server))
                    {
                        NCache.ServerName = node.IP;

                        Console.WriteLine(AppendBlankLine("\nCreating cache") + " '{0}' on server '{1}' ", _cacheConfig.CacheSettings.Name, NCache.ServerName);
                        try
                        {
                            cacheServer = NCache.GetCacheServer(new TimeSpan(0, 0, 0, 30));
                            if (cacheServer != null)
                            {
                                var serverConfig = cacheServer.GetNewConfiguration(_cacheConfig.CacheSettings.Name);

                                if (serverConfig != null)
                                {
                                    throw new Exception("Specified cache already exists.");
                                }

                                if (serverConfig != null && ccParam.IsOverWrite)
                                {
                                    NCache.ServerName = node.IP;

                                    if (serverConfig.CacheDeployment != null)
                                    {
                                        if (serverConfig.CacheDeployment.ClientNodes != null)
                                        {
                                            _cacheConfig.CacheDeployment.ClientNodes = serverConfig.CacheDeployment.ClientNodes;
                                        }
                                    }
                                }

                                cacheServer.RegisterCache(_cacheConfig.CacheSettings.Name, _cacheConfig, "", ccParam.IsOverWrite, ccParam.IsHotApply);
                                cacheServer.UpdateClientServersList(_cacheConfig.CacheSettings.Name, servers, "NCACHE");
                                serversToUpdate.Add(node.IP);

                                Console.WriteLine("Cache '{0}' successfully created on server {1}:{2} .", _cacheConfig.CacheSettings.Name, NCache.ServerName, NCache.Port);
                            }
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                    }
                    ManagementWorkFlow.UpdateServerMappingConfig(serversToUpdate.ToArray());
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    if (cacheServer != null)
                    {
                        cacheServer.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(AppendBlankLine("Failed") + " to create cache on server '{0}'. ", ccParam.Server);
                Console.Error.WriteLine("Error Detail: '{0}'. ", ex.Message);
                LogEvent(ex.Message);
            }
            finally
            {
                NCache.Dispose();
            }
        }

        private static string AppendBlankLine(string message)
        {
            var afterAppending = "\n" + message;
            return afterAppending;
        }

        ////<summary>
        ////Log an event in event viewer.
        ////</summary>
        private static void LogEvent(string msg)
        {
            var type = EventLogEntryType.Error;
            using (var ncLog = new EventLog("Application"))
            {
                ncLog.Source = "NCache: ConfigureCache Tool";
                ncLog.WriteEntry(msg, type);
            }
        }

        /// <summary>
        ///     Sets the application level parameters to those specified at the command line.
        /// </summary>
        /// <param name="args">array of command line parameters</param>
        /// <summary>
        ///     Validate all parameters in property string.
        /// </summary>
        private static bool ValidateParameters()
        {
            // Validating CacheId
            if (string.IsNullOrEmpty(ccParam.CacheId))
            {
                Console.Error.WriteLine(AppendBlankLine("Error: Cache name not specified."));
                return false;
            }


            if (string.IsNullOrEmpty(ccParam.Server))
            {
                Console.Error.WriteLine(AppendBlankLine("Error: Server IP not specified."));
                return false;
            }
            if (string.IsNullOrEmpty(ccParam.Topology))
            {
                ccParam.Topology = "local";
                return true;
            }

            if (!string.IsNullOrEmpty(ccParam.Topology) && ccParam.IsInProc)
            {
                if (!ccParam.Topology.Equals("local"))
                {
                    Console.Error.WriteLine(AppendBlankLine("Error: Cluster Cache cannot be InProc."));
                    return false;
                }
            }

            if (string.IsNullOrEmpty(ccParam.Path))
            {
                if (ccParam.Topology != null || ccParam.Topology != string.Empty)
                {
                    if (ccParam.CacheSize == -1)
                    {
                        Console.Error.WriteLine(AppendBlankLine("Error: Cache size not specified."));
                        return false;
                    }
                    if (ccParam.ClusterPort == -1 && !ccParam.Topology.Equals("local"))
                    {
                        Console.Error.WriteLine(AppendBlankLine("Error: Cluster port not specified."));
                        return false;
                    }
                }
                else
                {
                    Console.Error.WriteLine(AppendBlankLine("Error: Config path not specified. (For simple case specify topology)"));
                    return false;
                }
            }

            if (ccParam.Server == null || ccParam.Server == string.Empty)
            {
                Console.Error.WriteLine(AppendBlankLine("Error: Server IP not specified."));
                return false;
            }

            AssemblyUsage.PrintLogo(ccParam.IsLogo);

            return true;
        }
    }
}