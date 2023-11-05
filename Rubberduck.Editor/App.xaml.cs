﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rubberduck.Editor.EditorServer;
using Rubberduck.ServerPlatform;
using System.IO.Pipes;
using System.Threading;
using System;
using System.Windows;
using OmniSharpLanguageServer = OmniSharp.Extensions.LanguageServer.Server.LanguageServer;
using NLog.Extensions.Logging;
using System.Globalization;
using NLog;
using OmniSharp.Extensions.LanguageServer.Server;
using Nerdbank.Streams;
using Rubberduck.InternalApi.ServerPlatform;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Rubberduck.InternalApi.Common;
using System.Diagnostics;
using System.Threading.Tasks;
using Rubberduck.Editor.RPC.EditorServer.Handlers.Lifecycle;
using Rubberduck.Editor.RPC.EditorServer.Handlers.Workspace;
using Rubberduck.InternalApi.Settings;
using Rubberduck.SettingsProvider.Model;
using Rubberduck.InternalApi.Extensions;

namespace Rubberduck.Editor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly ServerStartupOptions _options;
        private readonly CancellationTokenSource _tokenSource;

        private NamedPipeServerStream? _pipe;

        private ILogger<ServerApp>? _logger;
        private IServiceProvider? _serviceProvider;

        private OmniSharpLanguageServer _languageServer = default!;
        private EditorServerState _serverState = default!;

        public App(ServerStartupOptions options, CancellationTokenSource tokenSource)
        {
            _options = options;
            _tokenSource = tokenSource;
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();
            services.AddLogging(ConfigureLogging);
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();
            _logger = _serviceProvider.GetRequiredService<ILogger<ServerApp>>();
            _serverState = _serviceProvider.GetRequiredService<EditorServerState>();

            _languageServer = await OmniSharpLanguageServer.From(ConfigureServer, _serviceProvider, _tokenSource.Token);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }

        private void ConfigureLogging(ILoggingBuilder builder)
        {
            builder.AddNLog(provider =>
            {
                var factory = new LogFactory
                {
                    AutoShutdown = true,
                    ThrowConfigExceptions = true,
                    ThrowExceptions = true,
                    DefaultCultureInfo = CultureInfo.InvariantCulture,
                    GlobalThreshold = NLog.LogLevel.Trace,
                };
                factory.Setup(builder =>
                {
                    builder.LoadConfigurationFromFile("NLog-editor.config");
                });

                return factory;
            });
        }

        private void ConfigureServices(IServiceCollection services)
        {

        }

        private void ConfigureServer(LanguageServerOptions options)
        {
            ConfigureTransport(options);

            var assembly = GetType().Assembly.GetName();
            var name = assembly.Name ?? throw new InvalidOperationException();
            var version = assembly.Version?.ToString(3);

            var info = new ServerInfo
            {
                Name = name,
                Version = version
            };

            var loggerProvider = new NLogLoggerProvider();
            loggerProvider.LogFactory.Setup().LoadConfigurationFromFile("NLog-editor.config");
            options.LoggerFactory = new NLogLoggerFactory(loggerProvider);

            options
                .WithServerInfo(info)
                .OnInitialize((ILanguageServer server, InitializeParams request, CancellationToken token) =>
                {
                    _logger?.LogDebug("Received Initialize request.");
                    token.ThrowIfCancellationRequested();
                    if (TimedAction.TryRun(() =>
                    {
                        _serverState.Initialize(request);
                    }, out var elapsed, out var exception))
                    {
                        _logger?.LogPerformance(TraceLevel.Verbose, "Handling initialize...", elapsed);
                    }
                    else if (exception != null)
                    {
                        _logger?.LogError(TraceLevel.Verbose, exception);
                    }
                    _logger?.LogDebug("Completed Initialize request.");
                    return Task.CompletedTask;
                })

                .OnInitialized((ILanguageServer server, InitializeParams request, InitializeResult response, CancellationToken token) =>
                {
                    _logger?.LogDebug("Received Initialized notification.");
                    token.ThrowIfCancellationRequested();

                    _logger?.LogDebug("Handled Initialized notification.");
                    return Task.CompletedTask;
                })

                .OnStarted((ILanguageServer server, CancellationToken token) =>
                {
                    _logger?.LogDebug("Language server started.");
                    token.ThrowIfCancellationRequested();

                    // todo: read workspace folder from _serverState.RootUri
                    // todo: parse all .vba/.vb6 files in folder
                    // todo: this needs to return immediately so the server can start without killing things
                    //       it also needs to capture the `server` param (or otherwise retain access to the server object)
                    //       to send notifications to the client (e.g. parsing, parse ended, resolving, inspecting, etc.)

                    _logger?.LogDebug("Handled Started notification.");
                    return Task.CompletedTask;
                })
                .WithHandler<ShutdownHandler>()
                .WithHandler<ExitHandler>()
                .WithHandler<DidChangeConfigurationHandler>()
                ;
        }

        private void ConfigureTransport(LanguageServerOptions options)
        {
            switch (_options.TransportType)
            {
                case TransportType.StdIO:
                    ConfigureStdIO(options);
                    break;

                case TransportType.Pipe:
                    ConfigurePipe(options);
                    break;

                default:
                    _logger?.LogWarning("An unsupported transport type was specified.");
                    throw new UnsupportedTransportTypeException(_options.TransportType);
            }
        }

        private void ConfigureStdIO(LanguageServerOptions options)
        {
            _logger?.LogInformation($"Configuring language server transport to use standard input/output streams...");

            options.WithInput(Console.OpenStandardInput());
            options.WithOutput(Console.OpenStandardOutput());
        }

        private void ConfigurePipe(LanguageServerOptions options)
        {
            var ioOptions = (PipeServerStartupOptions)_options;
            _logger?.LogInformation("Configuring language server transport to use a named pipe stream (mode: {mode})...", ioOptions.Mode);

            _pipe = new NamedPipeServerStream(ioOptions.PipeName, PipeDirection.InOut, 1, ioOptions.Mode, PipeOptions.Asynchronous);
            options.WithInput(_pipe.UsePipeReader());
            options.WithOutput(_pipe.UsePipeWriter());
        }

        public void Dispose()
        {
            if (_pipe is not null)
            {
                _logger?.LogDebug("Disposing NamedPipeServerStream...");
                _pipe.Dispose();
                _pipe = null;
            }

            if (_languageServer is not null)
            {
                _logger?.LogDebug("Disposing LanguageServer...");
                _languageServer.Dispose();
                _languageServer = null!;
            }
        }
    }
}
