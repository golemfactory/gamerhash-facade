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

            Golem = LoadLib(WorkDir, _loggerFactory);
            _jobsHistory = new ObservableCollection<IJob>();
        }

        public IGolem LoadLib(string modulesDir, ILoggerFactory? loggerFactory)
        {
            const string _golemNamespace = "Golem.Golem";

            var binaries = Path.Combine(modulesDir, "golem");
            var datadir = Path.Combine(modulesDir, "golem-data");

            string dllPath = Path.GetFullPath(Path.Combine(binaries, "Golem.dll"));

            Assembly ass = Assembly.LoadFrom(dllPath);
            Type? t = ass.GetType(_golemNamespace);
            if (t == null)
            {
                throw new Exception("Type not found. Lib not loaded: " + dllPath);
            }
            object? obj = Activator.CreateInstance(t, binaries, datadir, loggerFactory);
            obj = obj ?? throw new Exception("Creating instance failed. Lib not loaded: " + dllPath);
            return obj as IGolem ?? throw new Exception("Cast to IGolem failed.");
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

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            await StopRequestor();
            await Golem.Stop();

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }
    }
}
