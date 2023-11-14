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

namespace Golem
{
    public class Golem : IGolem, IAsyncDisposable
    {
        private YagnaService Yagna { get; set; }
        private Provider Provider { get; set; }
        private ProviderConfigService ProviderConfig { get; set; }

        private Task? _activityLoop;

        private readonly ILogger? _logger;
        private readonly CancellationTokenSource _tokenSource;

        private readonly HttpClient _httpClient;

        private GolemPrice price;
        public GolemPrice Price
        {
            get
            {
                return price;
            }
            set
            {
                price = value;
                OnPropertyChanged();
            }
        }

        public uint NetworkSpeed { get; set; }


        private GolemStatus status;
        private readonly string golemPath;
        private readonly ILoggerFactory loggerFactory;

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

            set => ProviderConfig.WalletAddress = value;
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
            throw new NotImplementedException();
        }

        public Task Resume()
        {
            throw new NotImplementedException();
        }

        public async Task Start()
        {
            Status = GolemStatus.Starting;

            bool openConsole = false;


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

        public Task<bool> Suspend()
        {
            throw new NotImplementedException();
        }

        public Golem(string golemPath, string? dataDir, ILoggerFactory? loggerFactory = null)
        {
            var prov_datadir = Path.Combine(dataDir, "provider");
            var yagna_datadir = Path.Combine(dataDir, "yagna");
            loggerFactory = loggerFactory == null ? NullLoggerFactory.Instance : loggerFactory;
            _logger = loggerFactory.CreateLogger<Golem>();
            _tokenSource = new CancellationTokenSource();

            Yagna = new YagnaService(golemPath, yagna_datadir, loggerFactory);
            Provider = new Provider(golemPath, prov_datadir, loggerFactory);
            ProviderConfig = new ProviderConfigService(Provider, YagnaOptionsFactory.DefaultNetwork);

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(YagnaOptionsFactory.DefaultYagnaApiUrl)
            };
        }

        public Golem(string golemPath, ILoggerFactory loggerFactory)
        {
            this.golemPath = golemPath;
            this.loggerFactory = loggerFactory;
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

            Thread.Sleep(700);

            //TODO what if activityLoop != null?
            this._activityLoop = StartActivityLoop();

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
                        if (account == null || account.Length == 0)
                            account = meInfo.Identity;
                        break;
                    }
                    throw new Exception("Failed to get key");

                }
                catch (Exception)
                {
                    // consciously swallow the exception... presumably REST call error...
                }
            }

            Yagna.PaymentService.Init(yagnaOptions.Network, PaymentDriver.ERC20.Id, account ?? "");

            return success;
        }

        public bool StartupProvider(YagnaStartupOptions yagnaOptions)
        {
            var presets = Provider.PresetConfig.ActivePresetsNames;
            if (!presets.Contains(Provider.PresetConfig.DefaultPresetName))
            {
                // Duration=0.0001 CPU=0.0001 "Init price=0.0000000000000001"
                var coefs = new Dictionary<string, decimal>
                {
                    { "Duration", 0.0001m },
                    { "CPU", 0.0001m },
                    // { "Init price", 0.0000000000000001m },
                };
                // name "ai" as defined in plugins/*.json
                var preset = new Preset(Provider.PresetConfig.DefaultPresetName, "ai-dummy", coefs);

                Provider.PresetConfig.AddPreset(preset, out string args, out string info);
                Console.WriteLine($"Args {args}");
                Console.WriteLine($"Args {info}");

            }
            Provider.PresetConfig.ActivatePreset(Provider.PresetConfig.DefaultPresetName);

            foreach (string preset in presets)
            {
                if (preset != Provider.PresetConfig.DefaultPresetName)
                {
                    Provider.PresetConfig.DeactivatePreset(preset);
                }
                Console.WriteLine($"Preset {preset}");
            }

            return Provider.Run(yagnaOptions.AppKey, Network.Goerli, yagnaOptions.YagnaApiUrl, true, true);
        }

        void OnYagnaErrorDataRecv(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine($"{e.Data}");
        }
        void OnYagnaOutputDataRecv(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine($"[Data]: {e.Data}");
        }

        private class TrackingEvent
        {
            public DateTime Ts { get; set; }

            public List<ActivityState> Activities { get; set; } = new List<ActivityState>();
        }

        private Task StartActivityLoop()
        {
            var token = _tokenSource.Token;
            token.Register(_httpClient.CancelPendingRequests);
            return new ActivityLoop(_httpClient, token, _logger).Start(EmitJobEvent);
        }

        private void EmitJobEvent(Job? job)
        {
            if (CurrentJob != job)
            {
                CurrentJob = job;
                _logger?.LogInformation("New job: {}", job);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentJob)));
            }
            else
            {
                _logger?.LogInformation("Job has not changed.");
            }
        }

        public async Task<YagnaAgreement?> GetAgreement(string agreementID)
        {
            try
            {
                var txt = await _httpClient.GetStringAsync($"/market-api/v1/agreements/{agreementID}");
                YagnaAgreement? aggr = JsonSerializer.Deserialize<YagnaAgreement>(txt) ?? null;
                return aggr;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to get agreement {}. Err: {}", agreementID, ex.Message);
                return null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await (this as IGolem).Stop();
        }


        class ActivityLoop
        {
            private const string _dataPrefix = "data:";
            private static readonly TimeSpan s_reconnectDelay = TimeSpan.FromSeconds(10);
            private static readonly JsonSerializerOptions s_serializerOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };

            private readonly HttpClient _httpClient;
            private readonly CancellationToken _token;
            private readonly ILogger _logger;

            public ActivityLoop(HttpClient httpClient, CancellationToken token, ILogger logger)
            {
                _httpClient = httpClient;
                _token = token;
                _logger = logger;
            }

            public async Task Start(Action<Job?> EmitJobEvent)
            {
                _logger?.LogInformation("Starting monitoring activities");

                DateTime newReconnect = DateTime.Now;
                try
                {
                    while (!_token.IsCancellationRequested)
                    {
                        _logger?.LogInformation("Monitoring activities");
                        var now = DateTime.Now;
                        if (newReconnect > now)
                        {
                            await Task.Delay(newReconnect - now);
                        }
                        newReconnect = now + s_reconnectDelay;
                        if (_token.IsCancellationRequested)
                        {
                            _token.ThrowIfCancellationRequested();
                        }

                        try
                        {
                            var stream = await _httpClient.GetStreamAsync("/activity-api/v1/_monitor");
                            using StreamReader reader = new StreamReader(stream);

                            await foreach (string json in EnumerateMessages(reader).WithCancellation(_token))
                            {
                                _logger?.LogInformation("got json {0}", json);
                                var activity_state = parseMessage(json);
                                if (activity_state == null)
                                {
                                    EmitJobEvent(null);
                                    continue;
                                }
                                var agreement = await GetAgreement(activity_state.AgreementId);
                                if (agreement == null)
                                {
                                    EmitJobEvent(null);
                                    continue;
                                }
                                var new_job = new Job()
                                {
                                    Id = activity_state.AgreementId,
                                    RequestorId = agreement.Demand.RequestorId
                                };
                                EmitJobEvent(new_job);

                            }
                        }
                        catch (Exception e)
                        {
                            _logger?.LogError(e, "Activity request failure");
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "Activity monitoring loop failure");
                }
                finally
                {
                    EmitJobEvent(null);
                }
            }

            private ActivityState? parseMessage(string message)
            {
                try
                {
                    var trackingEvent = JsonSerializer.Deserialize<TrackingEvent>(message, s_serializerOptions);
                    var _activities = trackingEvent?.Activities ?? new List<ActivityState>();
                    if (!_activities.Any())
                    {
                        _logger?.LogInformation("No activities");
                        return null;
                    }
                    var _active_activities = _activities.FindAll(activity => activity.State != ActivityState.StateType.Terminated);
                    if (!_active_activities.Any())
                    {
                        _logger?.LogInformation("All activities terminated: {}", _activities);
                        return null;
                    }
                    if (_active_activities.Count > 1)
                    {
                        _logger?.LogWarning("Multiple non terminated activities: {}", _active_activities);
                        //TODO what now?
                    }
                    //TODO take latest? the one with specific status?
                    ActivityState _activity = _activities.First();
                    if (_activity.AgreementId == null)
                    {
                        _logger?.LogInformation("Activity without agreement id: {}", _activity);
                        return null;
                    }
                    return _activity;
                }
                catch (JsonException e)
                {
                    _logger?.LogError(e, "Invalid monitoring event: {0}", message);
                    return null;
                }
            }

            private async IAsyncEnumerable<String> EnumerateMessages(StreamReader reader)
            {
                StringBuilder messageBuilder = new StringBuilder();
                while (true)
                {
                    try
                    {
                        String line;
                        while (!String.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                        {
                            if (line.StartsWith(_dataPrefix))
                            {
                                messageBuilder.Append(line.Substring(_dataPrefix.Length).TrimStart());
                                _logger?.LogInformation("got line {0}", line);
                            }
                            else
                            {
                                _logger?.LogError("Unable to deserialize message: {}", line);
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        _logger?.LogError("Failed to read message: {}", error);
                        break;
                    }
                    yield return messageBuilder.ToString();
                    messageBuilder.Clear();
                }
                yield break;
            }

            public async Task<YagnaAgreement?> GetAgreement(string agreementID)
            {
                try
                {
                    var response = await _httpClient.GetStringAsync($"/market-api/v1/agreements/{agreementID}");
                    _logger?.LogInformation("got agreement {0}", response);
                    YagnaAgreement? agreement = JsonSerializer.Deserialize<YagnaAgreement>(response, s_serializerOptions) ?? null;
                    return agreement;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed GetAgreementInfo: " + ex.Message);
                    return null;
                }
            }
        }
    }
}
