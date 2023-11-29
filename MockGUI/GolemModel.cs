using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Golem.IntegrationTests.Tools;

using GolemLib;
using App;


namespace MockGUI.ViewModels
{
    public class GolemViewModel : INotifyPropertyChanged, IAsyncDisposable
    {
        public IGolem Golem { get; init; }
        public DateTime DateSince { get; set; } = DateTime.Now;
        public TimeSpan TimeSince { get; set; } = DateTime.Now.TimeOfDay;

        private ObservableCollection<IJob> _jobsHistory;
        public ObservableCollection<IJob> JobsHistory
        {
            get { return _jobsHistory; }
            set { _jobsHistory = value; OnPropertyChanged(); }
        }
        private GolemRequestor? Requestor { get; set; }
        private SampleApp? App { get; set; }
        private string WorkDir { get; set; }
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public GolemViewModel(string modulesDir)
        {
            _loggerFactory = LoggerFactory.Create(builder =>
               builder.AddSimpleConsole()
            );
            _logger = _loggerFactory.CreateLogger<GolemViewModel>();

            WorkDir = modulesDir;
            var binaries = Path.Combine(modulesDir, "golem");
            var datadir = Path.Combine(modulesDir, "golem-data");

            Golem = new Golem.Golem(binaries, datadir, _loggerFactory);
            _jobsHistory = new ObservableCollection<IJob>();
        }

        public void OnStartCommand()
        {
            this.Golem.Start();
        }

        public void OnStopCommand()
        {
            this.Golem.Stop();
        }

        public void OnSuspendCommand()
        {
            this.Golem.Suspend();
        }

        public void OnResumeCommand()
        {
            this.Golem.Resume();
        }

        public async void OnRunExample()
        {
            try
            {
                _logger.LogInformation("Starting Requestor daemon");

                var datadir = Path.Combine(WorkDir, "requestor");
                Requestor = await GolemRequestor.BuildRelative(datadir, _loggerFactory.CreateLogger("Requestor"), false);
                Requestor.Start();

                _logger.LogInformation("Initializing payment accounts");
                Requestor.InitAccount();

                _logger.LogInformation("Creating requestor application");
                App = Requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
                App.Start();

                _logger.LogInformation("Application started");
            }
            catch (Exception e)
            {
                _logger.LogInformation("Error starting app: " + e.ToString());
                await StopRequestor();
            }

        }

        public async void OnListJobs()
        {
            var since = this.DateSince.Date + this.TimeSince;
            var jobs = await this.Golem.ListJobs(since);
            this.JobsHistory = new ObservableCollection<IJob>(jobs);
        }

        protected async Task StopRequestor()
        {
            if (App != null)
            {
                _logger.LogInformation("Stopping Example Application");
                await App.Stop(StopMethod.SigInt);
            }

            if (Requestor != null)
            {
                _logger.LogInformation("Stopping Example Requestor");
                await Requestor.Stop(StopMethod.SigInt);
            }
        }

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            await Golem.Stop();
            await StopRequestor();

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }
    }
}
