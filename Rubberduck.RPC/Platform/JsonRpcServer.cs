﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using StreamJsonRpc;
using Rubberduck.RPC.Platform.Model;
using Rubberduck.RPC.Proxy.SharedServices.Server.Configuration;
using Rubberduck.RPC.Proxy.SharedServices;

namespace Rubberduck.RPC.Platform
{
    /// <summary>
    /// Represents a server process.
    /// </summary>
    /// <remarks>
    /// Implementation holds the server state for the lifetime of the host process.
    /// </remarks>
    public abstract class JsonRpcServer<TStream, TServerService, TServerProxyClient, TOptions> : BackgroundService, IJsonRpcServer
        where TStream : Stream
        where TServerService : ServerService<TOptions, TServerProxyClient>
        where TOptions : SharedServerCapabilities, new()
        where TServerProxyClient : class, IServerProxyClient
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly IRpcStreamFactory<TStream> _rpcStreamFactory;
        private readonly IEnumerable<Type> _clientProxyTypes;
        protected JsonRpcServer(IServiceProvider serviceProvider, IEnumerable<Type> clientProxyTypes)
        {
            _serviceProvider = serviceProvider;

            _rpcStreamFactory = serviceProvider.GetService<IRpcStreamFactory<TStream>>();
            _clientProxyTypes = clientProxyTypes;
        }

        public abstract ServerState Info { get; }

        /// <summary>
        /// Wait for a client to connect.
        /// </summary>
        protected abstract Task WaitForConnectionAsync(TStream stream, CancellationToken token);

        /// <summary>
        /// Starts the server.
        /// </summary>
        /// <remarks>
        /// Creates a new RPC stream for each new client connection, until cancellation is requested on the token.
        /// </remarks>
        protected override async Task ExecuteAsync(CancellationToken serverToken)
        {
            Console.WriteLine("Registered RPC server targets:");
            foreach (var proxy in _serviceProvider.GetService<IEnumerable<IJsonRpcTarget>>())
            {
                Console.WriteLine($" • {proxy.GetType().Name}");
            }

            Console.WriteLine("Server started. Awaiting client connection...");
            while (!serverToken.IsCancellationRequested)
            {
                try
                {

                    // our stream only buffers a single message, so we need a new one every time:
                    var stream = _rpcStreamFactory.CreateNew();

                    await WaitForConnectionAsync(stream, serverToken);

                    // Because this call is not awaited, execution of the current method continues before the call is completed
#pragma warning disable CS4014

                    // we specifically *DO NOT* want to *wait* for the request processing (so, the response) here.
                    // awaiting this call would block the thread and prevent handling other incoming requests while a response is cooking.

                    SendResponseAsync(stream, serverToken);

#pragma warning restore CS4014
                }
                catch (OperationCanceledException) when (Thread.CurrentThread.IsBackground)
                {
                    await Task.Yield();
                }
            }

            Console.WriteLine("Server has stopped.");
            serverToken.ThrowIfCancellationRequested();
        }

        private static readonly JsonRpcTargetOptions _targetOptions = new JsonRpcTargetOptions
        {
            MethodNameTransform = MethodNameTransform,
            EventNameTransform = EventNameTransform,
            AllowNonPublicInvocation = false,
            ClientRequiresNamedArguments = true,
            DisposeOnDisconnect = true,
            NotifyClientOfEvents = true,
            UseSingleObjectParameterDeserialization = true,
        };

        private async Task SendResponseAsync(Stream stream, CancellationToken token)
        {
            using (var rpc = new JsonRpc(stream))
            {
                var serverProxies = _serviceProvider.GetService<IEnumerable<IJsonRpcTarget>>();
                foreach (var proxy in serverProxies)
                {
                    rpc.AddLocalRpcTarget(proxy, _targetOptions);
                }

                if (_clientProxyTypes.Any())
                {
                    Console.WriteLine("Registered RPC client targets:");
                }
                foreach (var proxyType in _clientProxyTypes)
                {
                    var proxy = rpc.Attach(proxyType);
                    foreach (var serverProxy in serverProxies)
                    {
                        serverProxy.InitializeClientProxy(proxy);
                        Console.WriteLine($" • {proxyType.Name}");
                        break;
                    }
                }

                token.ThrowIfCancellationRequested();

                rpc.StartListening();
                await rpc.Completion;
            }
        }

        private static string EventNameTransform(string name)
        {
            if (RpcMethodNameMappings.IsMappedEvent(name, out var mapped))
            {
                return mapped;
            }

            var camelCased = CommonMethodNameTransforms.CamelCase(name);
            System.Diagnostics.Debug.WriteLine($"Event '{name}' is not mapped to an explicit RPC method name: method is mapped to '{camelCased}'.");

            return camelCased;
        }

        private static string MethodNameTransform(string name)
        {
            if (RpcMethodNameMappings.IsMappedEvent(name, out var mapped))
            {
                return mapped;
            }

            var camelCased = CommonMethodNameTransforms.CamelCase(name);
            System.Diagnostics.Debug.WriteLine($"Method '{name}' is not mapped to an explicit RPC method name: method is mapped to '{camelCased}'.");

            return camelCased;
        }
    }
}