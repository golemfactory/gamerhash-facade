using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using Xunit.Abstractions;
using Golem.Yagna;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Golem.Tools;
using Golem.Yagna.Types;
using Golem;

namespace App
{
    public class SampleApp : GolemRunnable
    {
        private readonly Dictionary<string, string> _env;
        private readonly string? _extraArgs;
        private readonly Network _network;

        public SampleApp(string dir, Dictionary<string, string> env, Network network, ILogger logger, string? extraArgs = null) : base(dir, logger)
        {
            _env = env;
            _network = network;
            _extraArgs = extraArgs;
            var app_filename = ProcessFactory.BinName("app");
            var app_src = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? "", app_filename);
            var app_dst = Path.Combine(dir, "modules", "golem", app_filename);
            File.Copy(app_src, app_dst, true);
        }

        public override bool Start()
        {
            var working_dir = Path.Combine(_dir, "modules", "golem-data", "app");
            Directory.CreateDirectory(working_dir);

            var args = $"--network {_network.Id} --driver {PaymentDriver.ERC20.Id} --subnet-tag public {_extraArgs}";
            return StartProcess("app", working_dir, args, _env, true);
        }
    }

    public class FullExample : IAsyncDisposable, INotifyPropertyChanged
    {
        private GolemRequestor? Requestor { get; set; }
        private SampleApp? App { get; set; }
        private string WorkDir { get; set; }
        private string Name { get; set; }
        private readonly ILogger _logger;
        private readonly bool _mainnet;

        private readonly string _runtime;

        private string _message;
        public string Message
        {
            get => _message;
            private set
            {
                if (_message != value)
                {
                    _message = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public FullExample(string datadir, string name, ILoggerFactory loggerFactory, string runtime = "dummy", bool mainnet = false)
        {
            _logger = loggerFactory.CreateLogger(name);
            WorkDir = Path.Combine(datadir, name);
            Name = name;
            _message = "";
            _runtime = runtime;
            _mainnet = mainnet;
        }

        private string GetNodeDescriptor()
        {
            return PackageBuilder.ResourcePath("example-runner.mainnet.signed.json");
        }

        private string ExtraArgs()
        {
            var args = $"--runtime {_runtime}";
            if (_mainnet)
            {
                args += $" --descriptor {GetNodeDescriptor()}";
            }
            return args;
        }

        public async Task Run()
        {
            try
            {
                _logger.LogInformation("Starting Requestor daemon: " + Name);
                Message = "Starting Daemon";

                Requestor = await Task.Run(async () => await GolemRequestor.BuildRelative(WorkDir, _logger, cleanupData: false, _mainnet));
                await Task.Run(() => Requestor.Start());

                Message = "Payment Initialization";
                _logger.LogInformation("Initializing payment accounts for: " + Name);
                await Task.Run(() => Requestor.InitPayment());

                _logger.LogInformation("Creating requestor application: " + Name);
                Message = "Starting Application";

                App = Requestor?.CreateSampleApp(extraArgs: ExtraArgs())
                    ?? throw new Exception("Requestor '" + Name + "' not started yet");
                await Task.Run(() => App.Start());

                _logger.LogInformation("Application started: " + Name);
                Message = "App running";
            }
            catch (Exception e)
            {
                _logger.LogInformation("Error starting app: " + Name + " Error: " + e.ToString());
                Message = "Error";
                await this.Stop();
                throw;
            }

        }

        public async Task Stop()
        {
            try
            {
                if (App != null)
                {
                    _logger.LogInformation("Stopping Example Application: " + Name);
                    Message = "Stopping App";

                    await App.Stop(StopMethod.SigInt);
                    App = null;

                    _logger.LogInformation("Stopped Example Application: " + Name);
                    Message = "App Stopped";
                }

                if (Requestor != null)
                {
                    _logger.LogInformation("Stopping Example Requestor Daemon: " + Name);
                    Message = "Stopping Daemon";

                    await Requestor.Stop(StopMethod.SigInt);
                    Requestor = null;

                    _logger.LogInformation("Requestor Daemon stopped: " + Name);
                    Message = "Daemon Stopped";
                }

                Message = "Example stopped";
            }
            catch (Exception e)
            {
                _logger.LogInformation("Error stopping app: " + Name + " Error: " + e.ToString());
                Message = "Error";
                throw;
            }
        }

        public async Task Kill()
        {
            _logger.LogInformation("Requested hard kill of: " + Name);

            if (App != null)
            {
                _logger.LogInformation("Killing Example Application: " + Name);
                Message = "Killing App";

                await App.Stop(StopMethod.SigKill);
                App = null;
            }

            if (Requestor != null)
            {
                _logger.LogInformation("Killing Example Requestor Daemon: " + Name);
                Message = "Killing Daemon";

                await Requestor.Stop(StopMethod.SigKill);
                Requestor = null;
            }

            _logger.LogInformation("Example killed");
        }

        public Task WaitForFinish()
        {
            if (App != null)
                return App.WaitForFinish();
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await this.Stop();

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }
    }
}
