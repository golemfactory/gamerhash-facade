using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Golem;
using Golem.GolemUI.Src;
using Golem.Tools;
using Golem.Yagna;
using Golem.Yagna.Types;
using GolemLib;
using GolemLib.Types;
using Golem.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Specialized;

namespace Golem
{


    public class Golem : IGolem, IAsyncDisposable
    {
        private YagnaService Yagna { get; set; }
        private Provider Provider { get; set; }
        private ProviderConfigService ProviderConfig { get; set; }

        private Task? _activityLoop;
        private Task? _invoiceEventsLoop;
        private CancellationTokenSource? _tokenSource;

        private readonly ILogger _logger;
<<<<<<< HEAD
        private CancellationTokenSource _tokenSource;
=======
>>>>>>> master

        private readonly HttpClient _httpClient;

        private readonly GolemPrice _golemPrice;

        private readonly Jobs _jobs;

        public GolemPrice Price
        {
            get
            {
                // var price = ProviderConfig.GolemPrice;
                // _golemPrice.StartPrice = price.StartPrice;
                // _golemPrice.GpuPerHour = price.GpuPerHour;
                // _golemPrice.EnvPerHour = price.EnvPerHour;
                // _golemPrice.NumRequests = price.NumRequests;
                return _golemPrice;
            }
            set
            {
                _golemPrice.StartPrice = value.StartPrice;
                _golemPrice.GpuPerHour = value.GpuPerHour;
                _golemPrice.EnvPerHour = value.EnvPerHour;
                _golemPrice.NumRequests = value.NumRequests;

                OnPropertyChanged();
            }
        }

        private uint _networkSpeed;

        public uint NetworkSpeed
        {
            get => _networkSpeed;
            set
            {
                _networkSpeed = value;
                OnPropertyChanged();
            }
        }

        private GolemStatus status;

        public GolemStatus Status
        {
            get { return status; }
            set
            {
                status = value;
                OnPropertyChanged();
            }
        }

        public IJob? CurrentJob { get; private set; }
        public string? RecentJobId { get; private set; }

        public string NodeId
        {
            get { return Yagna.Id?.NodeId ?? ""; }
        }

        public string WalletAddress
        {
            get
            {
                var walletAddress = ProviderConfig.WalletAddress;
                if (walletAddress == null || walletAddress.Length == 0)
                    walletAddress = Yagna.Id?.NodeId;
                return walletAddress ?? "";
            }

            set
            {
                ProviderConfig.WalletAddress = value;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public Task BlacklistNode(string node_id)
        {
            throw new NotImplementedException();
        }

        public Task<List<IJob>> ListJobs(DateTime since)
        {
            return _jobs.List();
        }

        public async Task Resume()
        {
            await Start();
        }

        public async Task Start()
        {
            Status = GolemStatus.Starting;

            bool openConsole = true;

            var yagnaOptions = YagnaOptionsFactory.CreateStartupOptions(openConsole);

            var success = await StartupYagnaAsync(yagnaOptions);

            if (success)
            {
                var defaultKey = Yagna.AppKeyService.Get("default") ?? Yagna.AppKeyService.Get("autoconfigured");
                if (defaultKey is not null)
                {
                    if (StartupProvider(yagnaOptions))
                    {
                        Status = GolemStatus.Ready;
                    }
                    else
                    {
                        Status = GolemStatus.Error;
                    }
                }
            }
            else
            {
                Status = GolemStatus.Error;
            }

            OnPropertyChanged("WalletAddress");
            OnPropertyChanged("NodeId");
        }

        public async Task Stop()
        {
            _logger.LogInformation("Stopping Golem");
            await Provider.Stop();
            await Yagna.Stop();
            _tokenSource?.Cancel();
            Status = GolemStatus.Off;
        }

        public async Task<bool> Suspend()
        {
            await Stop();
            return false;
        }

        public Golem(string golemPath, string? dataDir, ILoggerFactory? loggerFactory = null)
        {
            var prov_datadir = dataDir != null ? Path.Combine(dataDir, "provider") : null;
            var yagna_datadir = dataDir != null ? Path.Combine(dataDir, "yagna") : null;
            loggerFactory ??= NullLoggerFactory.Instance;

            _logger = loggerFactory.CreateLogger<Golem>();
            _tokenSource = new CancellationTokenSource();

            Yagna = new YagnaService(golemPath, yagna_datadir, loggerFactory);
            Provider = new Provider(golemPath, prov_datadir, loggerFactory);
            ProviderConfig = new ProviderConfigService(Provider, YagnaOptionsFactory.DefaultNetwork);
            _golemPrice = ProviderConfig.GolemPrice;
            _jobs = new Jobs(setCurrentJob, loggerFactory);

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(YagnaOptionsFactory.DefaultYagnaApiUrl)
            };

            this.Price.PropertyChanged += GolemPrice_PropertyChangedHandler;
        }

        private void GolemPrice_PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is GolemPrice price)
            {
                ProviderConfig.GolemPrice = price;
            }
        }

        private async Task<bool> StartupYagnaAsync(YagnaStartupOptions yagnaOptions)
        {
            var success = Yagna.Run(yagnaOptions);

            if (!yagnaOptions.OpenConsole)
            {
                Yagna.BindErrorDataReceivedEvent(OnYagnaErrorDataRecv);
                Yagna.BindOutputDataReceivedEvent(OnYagnaOutputDataRecv);
            }

            if (!success)
                return false;

            var account = WalletAddress;

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", yagnaOptions.AppKey);

            account = await WaitForIdentityAsync();

<<<<<<< HEAD
            //TODO what if activityLoop != null?
            this._tokenSource = new CancellationTokenSource();
            this._activityLoop = StartActivityLoop();
            this._invoiceEventsLoop = StartInvoiceEventsLoop();
=======
            resetToken();
            _activityLoop = StartActivityLoop();
            _invoiceEventsLoop = StartInvoiceEventsLoop();
>>>>>>> master

            Yagna.PaymentService.Init(yagnaOptions.Network, PaymentDriver.ERC20.Id, account ?? "");

            return success;
        }

        public bool StartupProvider(YagnaStartupOptions yagnaOptions)
        {
            Provider.PresetConfig.InitilizeDefaultPreset();

            return Provider.Run(yagnaOptions.AppKey, Network.Goerli, yagnaOptions.YagnaApiUrl, yagnaOptions.OpenConsole, true);
        }

        async Task<string?> WaitForIdentityAsync()
        {
            string? identity = null;

            //yagna is starting and /me won't work until all services are running
            for (int tries = 0; tries < 300; ++tries)
            {
                Thread.Sleep(300);

                if (Yagna.HasExited) // yagna has stopped
                {
                    throw new Exception("Failed to start yagna ...");
                }

                try
                {
                    var response = _httpClient.GetAsync($"/me").Result;
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new Exception("Unauthorized call to yagna daemon - is another instance of yagna running?");
                    }
                    var txt = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptionsBuilder()
                                    .WithJsonNamingPolicy(JsonNamingPolicy.CamelCase)
                                    .Build();

                    MeInfo? meInfo = JsonSerializer.Deserialize<MeInfo>(txt, options) ?? null;
                    //sanity check
                    if (meInfo != null)
                    {
                        if (identity == null || identity.Length == 0)
                            identity = meInfo.Identity;
                        break;
                    }
                    throw new Exception("Failed to get key");

                }
                catch (Exception)
                {
                    // consciously swallow the exception... presumably REST call error...
                }
            }
            return identity;
        }

        void OnYagnaErrorDataRecv(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine($"{e.Data}");
        }
        void OnYagnaOutputDataRecv(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine($"[Data]: {e.Data}");
        }

        private Task StartActivityLoop()
        {
            var token = _tokenSource.Token;
            token.Register(_httpClient.CancelPendingRequests);
            return new ActivityLoop(_httpClient, token, _logger).Start(_jobs.ApplyJob, _jobs.UpdateUsage);
        }

        private Task StartInvoiceEventsLoop()
        {
            var token = _tokenSource.Token;
            token.Register(_httpClient.CancelPendingRequests);
            return new InvoiceEventsLoop(_httpClient, token, _logger).Start(_jobs.UpdatePaymentStatus, _jobs.UpdatePaymentConfirmation);
        }

        private void setCurrentJob(Job? job)
        {
            if (CurrentJob != job && (CurrentJob == null || !CurrentJob.Equals(job)))
            {
                CurrentJob = job;
                RecentJobId = job?.Id ?? RecentJobId;
                _logger.LogInformation("New job. Id: {0}, Requestor id: {1}, Status: {2}", job?.Id, job?.RequestorId, job?.Status);
                OnPropertyChanged(nameof(CurrentJob));
            }
            else
            {
                _logger.LogInformation("Job has not changed.");
            }
        }

<<<<<<< HEAD
        private void UpdateUsage(string jobId, GolemUsage usage)
        {
            if(Jobs.TryGetValue(jobId, out var job))
            {
                job.CurrentUsage = usage;
                OnPropertyChanged(nameof(CurrentJob));
            }
            else
            {
                _logger.LogError("Job not found: {}", jobId);
            }
        }

=======
>>>>>>> master
        public async ValueTask DisposeAsync()
        {
            await (this as IGolem).Stop();
        }

        private void resetToken()
        {
            //TODO lock access to token or use something else
            if (_tokenSource != null && !_tokenSource.IsCancellationRequested)
            {
                try
                {
                    _tokenSource?.Cancel();
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Failed to cancel token.");
                }
            }
            _tokenSource = new CancellationTokenSource();
        }
    }
}
