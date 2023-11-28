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

        private readonly ILogger _logger;
        private readonly CancellationTokenSource _tokenSource;

        private readonly HttpClient _httpClient;

        private readonly GolemPrice _golemPrice;
        
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
            get =>_networkSpeed;
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
        public Dictionary<string, Job> Jobs { get; private set; } = new Dictionary<string, Job>();

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

        public async Task<List<IJob>> ListJobs(DateTime since)
        {
            return await Task.FromResult(Jobs.Values.Select(j => j as IJob).ToList());
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
            _tokenSource.Cancel();
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

            //TODO what if activityLoop != null?
            this._activityLoop = StartActivityLoop();
            this._invoiceEventsLoop = StartInvoiceEventsLoop();

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
            return new ActivityLoop(_httpClient, token, _logger).Start(EmitJobEvent, UpdateUsage);
        }

        private Task StartInvoiceEventsLoop()
        {
            var token = _tokenSource.Token;
            token.Register(_httpClient.CancelPendingRequests);
            return new InvoiceEventsLoop(_httpClient, token, _logger).Start(UpdatePaymentStatus, UpdatePaymentConfirmation);
        }

        private void UpdatePaymentStatus(string id, GolemLib.Types.PaymentStatus paymentStatus)
        {
            if(Jobs.TryGetValue(id, out var job))
            {
                _logger.LogInformation("New payment status for job {}: {}", job.Id, paymentStatus);
                Console.WriteLine($"New payment status for job {job.Id}: {paymentStatus} requestor: {job.RequestorId}");
                job.PaymentStatus = paymentStatus;
            }
            else
            {
                _logger.LogError("Job not found: {}", id);
            }
        }

        private void UpdatePaymentConfirmation(string jobId, List<Payment> payments)
        {
            if(jobId != null && Jobs.TryGetValue(jobId, out var job))
            {
                _logger.LogInformation("Payments confirmation for job {}:", job.Id);

                job.PaymentConfirmation = payments;
            }
            else
            {
                _logger.LogError("No ewcent job found: {}", RecentJobId);
            }
        }

        private void EmitJobEvent(Job? job)
        {
            if (CurrentJob != job)
            {
                CurrentJob = job;
                RecentJobId = job?.Id ?? RecentJobId;
                var id = job?.Id ?? "";
                if(!Jobs.ContainsKey(id) && job!=null)
                {
                    Jobs.Add(id, job);
                }
                _logger.LogInformation("New job. Id: {0}, Requestor id: {1}, Status: {2}", job?.Id, job?.RequestorId, job?.Status);
                OnPropertyChanged(nameof(CurrentJob));
            }
            else
            {
                _logger.LogInformation("Job has not changed.");
            }
        }

        private void UpdateUsage(string jobId, GolemUsage usage)
        {
            if(Jobs.TryGetValue(jobId, out var job))
            {
                job.CurrentUsage = usage;
            }
            else
            {
                _logger.LogError("Job not found: {}", jobId);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await (this as IGolem).Stop();
        }
    }
}
