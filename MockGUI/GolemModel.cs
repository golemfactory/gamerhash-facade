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
using Golem;
using System.Collections.Generic;
using Golem.Yagna.Types;
using GolemLib.Types;


namespace MockGUI.ViewModels
{
    public class GolemViewModel : INotifyPropertyChanged, IAsyncDisposable
    {
        public IGolem Golem { get; init; }
        public DateTime DateSince { get; set; } = DateTime.Now.AddDays(-1);
        public TimeSpan TimeSince { get; set; } = DateTime.Now.TimeOfDay;

        private ObservableCollection<IJob> _jobsHistory;
        public ObservableCollection<IJob> JobsHistory
        {
            get { return _jobsHistory; }
            set { _jobsHistory = value; OnPropertyChanged(); }
        }

        private ObservableCollection<ApplicationEventArgs> _applicationEvents;
        public ObservableCollection<ApplicationEventArgs> ApplicationEvents
        {
            get
            {
                return _applicationEvents;
            }
            set
            {
                _applicationEvents = value;
                OnPropertyChanged();
            }
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

        public GolemViewModel(string modulesDir, IGolem golem, GolemRelay? relay, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<GolemViewModel>();
            WorkDir = modulesDir;
            Golem = golem;
            Relay = relay;
            _jobsHistory = new ObservableCollection<IJob>();
            _applicationEvents = new ObservableCollection<ApplicationEventArgs>();
            golem.ApplicationEvents += ApplicationEventsHandler;
        }

        public static async Task<GolemViewModel> Load(string modulesDir, RelayType relayType, bool mainnet)
        {
            return await Create(modulesDir, (loggerFactory) => LoadLib("Golem.dll", modulesDir, loggerFactory, mainnet, relayType), relayType, mainnet);
        }

        public static async Task<GolemViewModel> CreateStatic(string modulesDir, RelayType relayType, bool mainnet)
        {
            return await Create(modulesDir, (loggerFactory) => new Factory().Create(modulesDir, loggerFactory, mainnet, null, relayType), relayType, mainnet);
        }

        static async Task<GolemViewModel> Create(string modulesDir, Func<ILoggerFactory, Task<IGolem>> createGolem, RelayType relayType, bool mainnet)
        {
            var loggerFactory = createLoggerFactory(modulesDir);
            var golem = await createGolem(loggerFactory);
            if (mainnet)
            {
                ((Golem.Golem)golem).WalletAddress = mainnetWalletAddr();
            }
            var relay = await CreateRelay(modulesDir, relayType, loggerFactory);
            return new GolemViewModel(modulesDir, golem, relay, loggerFactory);
        }

        public static async Task<GolemRelay?> CreateRelay(string modulesDir, RelayType relayType, ILoggerFactory loggerFactory)
        {
            if (relayType == RelayType.Local)
            {
                var logger = loggerFactory.CreateLogger(nameof(GolemRelay));
                var relayDir = Path.Combine(modulesDir, "relay");

                var relay = await GolemRelay.Build(relayDir, logger);
                if (relay.Start())
                {
                    return relay;
                }
                else
                {
                    logger.LogError("Failed to start local relay server");
                    return null;
                };
            }
            else
                return null;

        }

        private static string mainnetWalletAddr()
        {
            var mainnetAddressFilename = "main_address.txt";
            var mainnetAddressReader = PackageBuilder.ReadResource(mainnetAddressFilename);
            return mainnetAddressReader.ReadLine() ?? throw new Exception($"Failed to read from file {mainnetAddressFilename}");
        }

        public static async Task<IGolem> LoadLib(string lib, string modulesDir, ILoggerFactory loggerFactory, bool mainnet, RelayType relayType)
        {
            const string factoryType = "Golem.Factory";

            var binaries = Path.Combine(modulesDir, "golem");
            string dllPath = Path.GetFullPath(Path.Combine(binaries, lib));

            Assembly ass = Assembly.LoadFrom(dllPath);
            Type? t = ass.GetType(factoryType) ?? throw new Exception("Factory Type not found. Lib not loaded: " + dllPath);
            var obj = Activator.CreateInstance(t) ?? throw new Exception("Creating Factory instance failed. Lib not loaded: " + dllPath);
            var factory = obj as IFactoryExt ?? throw new Exception("Cast to IFactory failed.");

            return await factory.Create(modulesDir, loggerFactory, mainnet, null, relayType);
        }

        private static ILoggerFactory createLoggerFactory(string modulesDir)
        {
            var logFile = Path.Combine(modulesDir, "golem-data", "golem-{Date}.log");
            return LoggerFactory.Create(builder =>
                builder.AddFile(logFile)
                    .AddConsole()
            );
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
                App = new FullExample(WorkDir, "Requestor1", _loggerFactory, mainnet: Golem.Mainnet);
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
            List<IJob> jobs;
            try
            {
                _logger.LogInformation("Listing jobs since: " + since);
                jobs = await this.Golem.ListJobs(since);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Listing jobs failure");
                jobs = new List<IJob>();
            }

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

        public async Task Shutdown()
        {
            await StopRelay();
            await StopRequestor();
            await Golem.Stop();
        }

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            await Shutdown();
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        void ApplicationEventsHandler(object? sender, ApplicationEventArgs e)
        {
            ApplicationEvents.Add(e);
        }
    }
}
