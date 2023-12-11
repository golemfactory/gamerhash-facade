using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using GolemLib;
using App;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Golem.Tools;


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
        private FullExample? _app;
        public FullExample? App
        {
            get => _app;
            private set
            {
                if (_app != value)
                {
                    _app = value;
                    OnPropertyChanged();
                }
            }
        }
        private GolemRelay? Relay { get; set; }

        private string WorkDir { get; set; }
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public GolemViewModel(string modulesDir, IGolem golem, GolemRelay? relay, ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<GolemViewModel>();

            WorkDir = modulesDir;
            Golem = golem;
            Relay = relay;
            _jobsHistory = new ObservableCollection<IJob>();
        }

        public static async Task<GolemViewModel> Load(string modulesDir, RelayType relayType)
        {
            var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddSimpleConsole()
            );

            var golem = await LoadLib("Golem.dll", modulesDir, loggerFactory);
            var relay = await CreateRelay(modulesDir, relayType, loggerFactory);
            return new GolemViewModel(modulesDir, golem, relay, loggerFactory);
        }

        public static async Task<GolemViewModel> CreateStatic(string modulesDir, RelayType relayType)
        {
            var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddSimpleConsole()
            );

            var binaries = Path.Combine(modulesDir, "golem");
            var datadir = Path.Combine(modulesDir, "golem-data");

            var golem = new Golem.Golem(binaries, datadir, loggerFactory);
            var relay = await CreateRelay(modulesDir, relayType, loggerFactory);
            return new GolemViewModel(modulesDir, golem, relay, loggerFactory);
        }

        public static async Task<GolemRelay?> CreateRelay(string modulesDir, RelayType relayType, ILoggerFactory loggerFactory)
        {
            GolemRelay.SetEnv(relayType);

            if (relayType == RelayType.Local)
            {
                var logger = loggerFactory.CreateLogger(nameof(GolemRelay));
                var relayDir = Path.Combine(modulesDir, "relay");
                return await GolemRelay.Build(relayDir, logger);
            }
            else
                return null;

        }

        public static async Task<IGolem> LoadLib(string lib, string modulesDir, ILoggerFactory? loggerFactory)
        {
            const string factoryType = "Golem.Factory";

            var binaries = Path.Combine(modulesDir, "golem");
            string dllPath = Path.GetFullPath(Path.Combine(binaries, lib));

            Assembly ass = Assembly.LoadFrom(dllPath);
            Type? t = ass.GetType(factoryType) ?? throw new Exception("Factory Type not found. Lib not loaded: " + dllPath);
            var obj = Activator.CreateInstance(t) ?? throw new Exception("Creating Factory instance failed. Lib not loaded: " + dllPath);
            var factory = obj as IFactory ?? throw new Exception("Cast to IFactory failed.");

            return await factory.Create(modulesDir, loggerFactory);
        }

        public Task OnStartCommand()
        {
            return Task.Run(Golem.Start);
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
                App = new FullExample(WorkDir, "Requestor1", _loggerFactory);
                await App.Run();
            }
            catch (Exception e)
            {
                _logger.LogInformation("Error starting app: " + e.ToString());
                App = null;
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
                await App.Stop();
                App = null;
            }
        }

        protected async Task StopRelay()
        {
            if (Relay != null)
            {
                await Relay.Stop(StopMethod.SigInt);
                Relay = null;
            }
        }

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            await StopRequestor();
            await Golem.Stop();
            await StopRelay();

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }
    }
}
