﻿using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using TouchSocket.Core;
using TouchSocket.Resources;
using TouchSocket.Sockets;

namespace TouchSocket.NamedPipe
{
    /// <summary>
    /// 命名管道服务器
    /// </summary>
    public class NamedPipeService : NamedPipeService<NamedPipeSocketClient>
    {
        /// <summary>
        /// 处理数据
        /// </summary>
        public ReceivedEventHandler<NamedPipeSocketClient> Received { get; set; }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="socketClient"></param>
        /// <param name="byteBlock"></param>
        /// <param name="requestInfo"></param>
        protected override void OnReceived(NamedPipeSocketClient socketClient, ByteBlock byteBlock, IRequestInfo requestInfo)
        {
            this.Received?.Invoke(socketClient, byteBlock, requestInfo);
            base.OnReceived(socketClient, byteBlock, requestInfo);
        }
    }
    /// <summary>
    /// 泛型命名管道服务器。
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    public class NamedPipeService<TClient> : NamedPipeServiceBase where TClient : NamedPipeSocketClient, new()
    {
        /// <summary>
        /// 泛型命名管道服务器
        /// </summary>
        public NamedPipeService()
        {
            this.m_getDefaultNewId = () =>
            {
                return Interlocked.Increment(ref this.m_nextId).ToString();
            };
        }

        #region 字段

        private readonly List<NamedPipeMonitor> m_monitors = new List<NamedPipeMonitor>();
        private TouchSocketConfig m_config;
        private IContainer m_container;
        private Func<string> m_getDefaultNewId;
        private int m_maxCount;
        private long m_nextId;
        private IPluginsManager m_pluginsManager;
        private ServerState m_serverState;
        private NamedPipeSocketClientCollection m_socketClients = new NamedPipeSocketClientCollection();
        #endregion 字段

        #region 属性
        /// <inheritdoc/>
        public override string ServerName => this.Config?.GetValue(TouchSocketConfigExtension.ServerNameProperty);

        /// <inheritdoc/>
        public override TouchSocketConfig Config { get => this.m_config; }

        /// <inheritdoc/>
        public override IContainer Container { get => this.m_container; }

        /// <inheritdoc/>
        public override Func<string> GetDefaultNewId => this.m_getDefaultNewId;

        /// <inheritdoc/>
        public override int MaxCount { get => this.m_maxCount; }

        /// <inheritdoc/>
        public override IEnumerable<NamedPipeMonitor> Monitors => this.m_monitors;

        /// <inheritdoc/>
        public override IPluginsManager PluginsManager { get => this.m_pluginsManager; }

        /// <inheritdoc/>
        public override ServerState ServerState => this.m_serverState;

        /// <inheritdoc/>
        public override INamedPipeSocketClientCollection SocketClients { get => this.m_socketClients; }

        #endregion 属性

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="option"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public override void AddListen(NamedPipeListenOption option)
        {
            if (option is null)
            {
                throw new ArgumentNullException(nameof(option));
            }

            this.ThrowIfDisposed();

            var e = new SocketAsyncEventArgs();

            var networkMonitor = new NamedPipeMonitor(option);

            new Thread(this.ThreadBegin)
            {
                IsBackground = true
            }.Start(networkMonitor);
            this.m_monitors.Add(networkMonitor);
        }

        /// <inheritdoc/>
        public override void Clear()
        {
            foreach (var item in this.GetIds())
            {
                if (this.TryGetSocketClient(item, out var client))
                {
                    client.SafeDispose();
                }
            }
        }

        /// <summary>
        /// 获取当前在线的所有客户端
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TClient> GetClients()
        {
            return this.m_socketClients.GetClients()
                  .Select(a => (TClient)a);
        }

        /// <inheritdoc/>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public override bool RemoveListen(NamedPipeMonitor monitor)
        {
            if (monitor is null)
            {
                throw new ArgumentNullException(nameof(monitor));
            }

            if (this.m_monitors.Remove(monitor))
            {
                //monitor.SocketAsyncEvent.SafeDispose();
                //monitor.Socket.SafeDispose();
                throw new Exception();
                return true;
            }
            return false;
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="oldId"></param>
        /// <param name="newId"></param>
        /// <exception cref="ClientNotFindException"></exception>
        /// <exception cref="Exception"></exception>
        public override void ResetId(string oldId, string newId)
        {
            if (string.IsNullOrEmpty(oldId))
            {
                throw new ArgumentException($"“{nameof(oldId)}”不能为 null 或空。", nameof(oldId));
            }

            if (string.IsNullOrEmpty(newId))
            {
                throw new ArgumentException($"“{nameof(newId)}”不能为 null 或空。", nameof(newId));
            }

            if (oldId == newId)
            {
                return;
            }
            if (this.m_socketClients.TryGetSocketClient(oldId, out TClient socketClient))
            {
                socketClient.ResetId(newId);
            }
            else
            {
                throw new ClientNotFindException(TouchSocketResource.ClientNotFind.GetDescription(oldId));
            }
        }

        /// <inheritdoc/>
        public override IService Setup(TouchSocketConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            this.ThrowIfDisposed();

            this.BuildConfig(config);

            this.m_pluginsManager.Raise(nameof(ILoadingConfigPlugin.OnLoadingConfig), this, new ConfigEventArgs(config));
            this.LoadConfig(this.m_config);
            this.m_pluginsManager.Raise(nameof(ILoadedConfigPlugin.OnLoadedConfig), this, new ConfigEventArgs(config));

            this.Logger ??= this.m_container.Resolve<ILog>();
            return this;
        }

        /// <summary>
        ///<inheritdoc/>
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override bool SocketClientExist(string id)
        {
            return this.SocketClients.SocketClientExist(id);
        }

        /// <inheritdoc/>
        public override IService Start()
        {
            if (this.m_config is null)
            {
                throw new ArgumentNullException(nameof(this.Config), "Config为null，请先执行Setup");
            }

            var optionList = new List<NamedPipeListenOption>();
            if (this.Config.GetValue(NamedPipeConfigExtension.NamedPipeListenOptionProperty) is Action<List<NamedPipeListenOption>> action)
            {
                action.Invoke(optionList);
            }

            var pipeName = this.Config.GetValue(NamedPipeConfigExtension.PipeNameProperty);
            if (pipeName != null)
            {
                var option = new NamedPipeListenOption
                {
                    Name = pipeName,
                    Adapter = this.Config.GetValue(NamedPipeConfigExtension.NamedPipeDataHandlingAdapterProperty),
                    SendTimeout = this.Config.GetValue(TouchSocketConfigExtension.SendTimeoutProperty)
                };

                optionList.Add(option);
            }

            if (optionList.Count == 0)
            {
                return this;
            }
            try
            {
                switch (this.m_serverState)
                {
                    case ServerState.None:
                        {
                            this.BeginListen(optionList);
                            break;
                        }
                    case ServerState.Running:
                        {
                            return this;
                        }
                    case ServerState.Stopped:
                        {
                            this.BeginListen(optionList);
                            break;
                        }
                    case ServerState.Disposed:
                        {
                            throw new ObjectDisposedException(this.GetType().Name);
                        }
                }
                this.m_serverState = ServerState.Running;

                this.m_pluginsManager.Raise(nameof(IServerStartedPlugin.OnServerStarted), this, new ServiceStateEventArgs(this.m_serverState, default));
                return this;
            }
            catch (Exception ex)
            {
                this.m_serverState = ServerState.Exception;

                this.m_pluginsManager.Raise(nameof(IServerStartedPlugin.OnServerStarted), this, new ServiceStateEventArgs(this.m_serverState, ex) { Message = ex.Message });
                throw;
            }
        }

        /// <inheritdoc/>
        public override IService Stop()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 尝试获取TClient
        /// </summary>
        /// <param name="id">Id</param>
        /// <param name="socketClient">TClient</param>
        /// <returns></returns>
        public bool TryGetSocketClient(string id, out TClient socketClient)
        {
            return this.m_socketClients.TryGetSocketClient(id, out socketClient);
        }

        /// <summary>
        /// 获取客户端实例
        /// </summary>
        /// <param name="namedPipe"></param>
        /// <param name="monitor"></param>
        /// <returns></returns>
        protected virtual TClient GetClientInstence(NamedPipeServerStream namedPipe, NamedPipeMonitor monitor)
        {
            return new TClient();
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        /// <param name="config"></param>
        protected virtual void LoadConfig(TouchSocketConfig config)
        {
            if (config.GetValue(TouchSocketConfigExtension.GetDefaultNewIdProperty) is Func<string> fun)
            {
                this.m_getDefaultNewId = fun;
            }
            this.m_maxCount = config.GetValue(TouchSocketConfigExtension.MaxCountProperty);
        }

        private void BeginListen(List<NamedPipeListenOption> optionList)
        {
            foreach (var item in optionList)
            {
                this.AddListen(item);
            }
        }

        private void BuildConfig(TouchSocketConfig config)
        {
            this.m_config = config;

            if (!(config.GetValue(TouchSocketCoreConfigExtension.ContainerProperty) is IContainer container))
            {
                container = new Container();
            }

            if (!container.IsRegistered(typeof(ILog)))
            {
                container.RegisterSingleton<ILog, LoggerGroup>();
            }

            if (!(config.GetValue(TouchSocketCoreConfigExtension.PluginsManagerProperty) is IPluginsManager pluginsManager))
            {
                pluginsManager = new PluginsManager(container);
            }

            if (container.IsRegistered(typeof(IPluginsManager)))
            {
                pluginsManager = container.Resolve<IPluginsManager>();
            }
            else
            {
                container.RegisterSingleton<IPluginsManager>(pluginsManager);
            }

            if (config.GetValue(TouchSocketCoreConfigExtension.ConfigureContainerProperty) is Action<IContainer> actionContainer)
            {
                actionContainer.Invoke(container);
            }

            if (config.GetValue(TouchSocketCoreConfigExtension.ConfigurePluginsProperty) is Action<IPluginsManager> actionPluginsManager)
            {
                pluginsManager.Enable = true;
                actionPluginsManager.Invoke(pluginsManager);
            }

            this.m_container = container;
            this.m_pluginsManager = pluginsManager;
        }

        private void OnClientSocketInit(NamedPipeServerStream namedPipe, NamedPipeMonitor monitor)
        {
            var client = this.GetClientInstence(namedPipe, monitor);
            client.InternalSetConfig(this.m_config);
            client.InternalSetContainer(this.m_container);
            client.InternalSetService(this);
            client.InternalSetNamedPipe(namedPipe);
            client.InternalSetPluginsManager(this.m_pluginsManager);

            if (client.CanSetDataHandlingAdapter)
            {
                client.SetDataHandlingAdapter(monitor.Option.Adapter.Invoke());
            }
            client.InternalInitialized();

            var args = new ConnectingEventArgs(null)
            {
                Id = this.GetDefaultNewId()
            };
            client.InternalConnecting(args);//Connecting
            if (args.IsPermitOperation)
            {
                client.InternalSetId(args.Id);
                if (this.m_socketClients.TryAdd(client))
                {
                    client.InternalConnected(new ConnectedEventArgs());

                    client.BeginReceive();
                }
                else
                {
                    throw new Exception($"Id={client.Id}重复");
                }
            }
            else
            {
                namedPipe.SafeDispose();
            }
        }

        private void ThreadBegin(object obj)
        {
            var monitor = (NamedPipeMonitor)obj;
            var option = monitor.Option;
            while (true)
            {
                try
                {
                    if (this.ServerState != ServerState.Running)
                    {
                        return;
                    }
                    var namedPipe = new NamedPipeServerStream(option.Name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0);

                    namedPipe.WaitForConnection();

                    this.OnClientSocketInit(namedPipe, monitor);
                }
                catch (Exception ex)
                {
                    this.Logger.Exception(ex);
                }
            }
        }

        #region 事件

        /// <summary>
        /// 用户连接完成
        /// </summary>
        public ConnectedEventHandler<TClient> Connected { get; set; }

        /// <summary>
        /// 有用户连接的时候
        /// </summary>
        public ConnectingEventHandler<TClient> Connecting { get; set; }

        /// <summary>
        /// 有用户断开连接
        /// </summary>
        public DisconnectEventHandler<TClient> Disconnected { get; set; }

        /// <summary>
        /// 即将断开连接(仅主动断开时有效)。
        /// <para>
        /// 当主动调用Close断开时，可通过<see cref="MsgPermitEventArgs.IsPermitOperation"/>终止断开行为。
        /// </para>
        /// </summary>
        public DisconnectEventHandler<TClient> Disconnecting { get; set; }

        /// <summary>
        /// 当客户端Id被修改时触发。
        /// </summary>
        public IdChangedEventHandler<TClient> IdChanged { get; set; }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="socketClient"></param>
        /// <param name="e"></param>
        protected override sealed void OnClientConnected(INamedPipeSocketClient socketClient, ConnectedEventArgs e)
        {
            this.OnConnected((TClient)socketClient, e);
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="socketClient"></param>
        /// <param name="e"></param>
        protected override sealed void OnClientConnecting(INamedPipeSocketClient socketClient, ConnectingEventArgs e)
        {
            this.OnConnecting((TClient)socketClient, e);
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="socketClient"></param>
        /// <param name="e"></param>
        protected override sealed void OnClientDisconnected(INamedPipeSocketClient socketClient, DisconnectEventArgs e)
        {
            this.OnDisconnected((TClient)socketClient, e);
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="socketClient"></param>
        /// <param name="e"></param>
        protected override sealed void OnClientDisconnecting(INamedPipeSocketClient socketClient, DisconnectEventArgs e)
        {
            this.OnDisconnecting((TClient)socketClient, e);
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="socketClient"></param>
        /// <param name="byteBlock"></param>
        /// <param name="requestInfo"></param>
        protected override sealed void OnClientReceivedData(INamedPipeSocketClient socketClient, ByteBlock byteBlock, IRequestInfo requestInfo)
        {
            this.OnReceived((TClient)socketClient, byteBlock, requestInfo);
        }

        /// <summary>
        /// 客户端连接完成，覆盖父类方法将不会触发事件。
        /// </summary>
        /// <param name="socketClient"></param>
        /// <param name="e"></param>
        protected virtual void OnConnected(TClient socketClient, ConnectedEventArgs e)
        {
            this.Connected?.Invoke(socketClient, e);
        }

        /// <summary>
        /// 客户端请求连接，覆盖父类方法将不会触发事件。
        /// </summary>
        /// <param name="socketClient"></param>
        /// <param name="e"></param>
        protected virtual void OnConnecting(TClient socketClient, ConnectingEventArgs e)
        {
            this.Connecting?.Invoke(socketClient, e);
        }

        /// <summary>
        /// 客户端断开连接，覆盖父类方法将不会触发事件。
        /// </summary>
        /// <param name="socketClient"></param>
        /// <param name="e"></param>
        protected virtual void OnDisconnected(TClient socketClient, DisconnectEventArgs e)
        {
            this.Disconnected?.Invoke(socketClient, e);
        }

        /// <summary>
        /// 即将断开连接(仅主动断开时有效)。
        /// <para>
        /// 当主动调用Close断开时，可通过<see cref="MsgPermitEventArgs.IsPermitOperation"/>终止断开行为。
        /// </para>
        /// </summary>
        /// <param name="socketClient"></param>
        /// <param name="e"></param>
        protected virtual void OnDisconnecting(TClient socketClient, DisconnectEventArgs e)
        {
            this.Disconnecting?.Invoke(socketClient, e);
        }

        /// <summary>
        /// 当收到适配器数据。
        /// </summary>
        /// <param name="socketClient"></param>
        /// <param name="byteBlock"></param>
        /// <param name="requestInfo"></param>
        protected virtual void OnReceived(TClient socketClient, ByteBlock byteBlock, IRequestInfo requestInfo)
        {
        }

        #endregion 事件
    }
}