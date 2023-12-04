using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using Xunit.Abstractions;
using Golem.Yagna;
using System.Diagnostics;
using Golem.IntegrationTests.Tools;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace App
{
    public class SampleApp : GolemRunnable
    {
        private readonly Dictionary<string, string> _env;

        public SampleApp(string dir, Dictionary<string, string> env, ILogger logger) : base(dir, logger)
        {
            _env = env;
            var app_filename = ProcessFactory.BinName("app");
            var app_src = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, app_filename);
            var app_dst = Path.Combine(dir, "modules", "golem", app_filename);
            File.Copy(app_src, app_dst, true);
        }

        public override bool Start()
        {
            var working_dir = Path.Combine(_dir, "modules", "golem-data", "yagna");
            Directory.CreateDirectory(working_dir);
            return StartProcess("app", Path.Combine(_dir, "modules", "golem-data", "yagna"), "--network goerli --subnet-tag public", _env, true);
        }
    }

    public class FullExample : IAsyncDisposable, INotifyPropertyChanged
    {
        private GolemRequestor? Requestor { get; set; }
        private SampleApp? App { get; set; }
        private string WorkDir { get; set; }
        private string Name { get; set; }
        private readonly ILogger _logger;

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

        public FullExample(string datadir, string name, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(name);
            WorkDir = Path.Combine(datadir, name);
            Name = name;
            _message = "";
        }

        public async Task Run()
        {
            try
            {
                _logger.LogInformation("Starting Requestor daemon: " + Name);
                Message = "Starting Daemon";

                Requestor = await GolemRequestor.BuildRelative(WorkDir, _logger, false);
                Requestor.Start();

                Message = "Funding accounts";
                _logger.LogInformation("Initializing payment accounts for: " + Name);
                Requestor.InitAccount();

                _logger.LogInformation("Creating requestor application: " + Name);
                Message = "Starting Application";

                App = Requestor?.CreateSampleApp() ?? throw new Exception("Requestor '" + Name + "' not started yet");
                App.Start();

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
            if (App != null)
            {
                _logger.LogInformation("Stopping Example Application: " + Name);
                Message = "Stopping App";

                await App.Stop(StopMethod.SigInt);
                App = null;

                Message = "App Stopped";
            }

            if (Requestor != null)
            {
                _logger.LogInformation("Stopping Example Requestor: " + Name);
                Message = "Stopping Daemon";

                await Requestor.Stop(StopMethod.SigInt);
                Requestor = null;

                Message = "Daemon Stopped";
            }

            Message = "Example stopped";
        }

        public async ValueTask DisposeAsync()
        {
            await this.Stop();

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }
    }
}
