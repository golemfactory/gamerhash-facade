using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Golem;
using Golem.Tools;
using Golem.Yagna;
using Golem.Yagna.Types;
using GolemLib;
using GolemLib.Types;
using GolemUI.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Golem
{
    public class Golem : IGolem, IAsyncDisposable
    {
        private YagnaService Yagna { get; set; }
        private Provider Provider { get; set; }

        private Task? activityLoop;

        private readonly ILogger? _logger;
        private readonly CancellationTokenSource _tokenSource;
        
        private readonly HttpClient HttpClient;

        public GolemPrice Price { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string WalletAddress { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public uint NetworkSpeed { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }


        private GolemStatus status;
        private string golemPath;
        private ILoggerFactory loggerFactory;

        public GolemStatus Status
        {
            get { return status; }
            set {  status = value; OnPropertyChanged(); }
        }

        public IJob? CurrentJob { get; private set; }

        public string NodeId => throw new NotImplementedException();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        Task IGolem.BlacklistNode(string node_id)
        {
            throw new NotImplementedException();
        }

        Task<List<IJob>> IGolem.ListJobs(DateTime since)
        {
            throw new NotImplementedException();
        }

        Task IGolem.Resume()
        {
            throw new NotImplementedException();
        }

        async Task IGolem.Start()
        {
            Status = GolemStatus.Starting;

            bool openConsole = false;
            
            var yagnaOptions = YagnaOptionsFactory.CreateStartupOptions(openConsole);

            var success = await StartupYagnaAsync(yagnaOptions);

            if (success)
            {
                var defaultKey = Yagna.AppKeyService.Get("default");
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
        }

        async Task IGolem.Stop()
        {
            await Provider.Stop();
            await Yagna.Stop();
            _tokenSource.Cancel();
            Status = GolemStatus.Off;
        }

        Task<bool> IGolem.Suspend()
        {
            throw new NotImplementedException();
        }

        public Golem(string golemPath, string? dataDir=null, ILoggerFactory? loggerFactory = null)
        {
            loggerFactory = loggerFactory == null ? NullLoggerFactory.Instance : loggerFactory;
            _logger = loggerFactory.CreateLogger<Golem>();
            _tokenSource = new CancellationTokenSource();

            Yagna = new YagnaService(golemPath, loggerFactory);
            Provider = new Provider(golemPath, dataDir, loggerFactory);

            HttpClient = new HttpClient
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

            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", yagnaOptions.AppKey);

            Thread.Sleep(700);

            this.activityLoop = StartActivityLoop();

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
                    var response = HttpClient.GetAsync($"/me").Result;
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
                        return meInfo.Identity != null;
                    }
                    throw new Exception("Failed to get key");

                }
                catch (Exception)
                {
                    // consciously swallow the exception... presumably REST call error...
                }
            }

            return success;
        }

        public bool StartupProvider(YagnaStartupOptions yagnaOptions)
        {
            var preset_name = "ai-dummy";
            var presets = Provider.ActivePresets;
            if (!presets.Contains(preset_name))
            {
                // Duration=0.0001 CPU=0.0001 "Init price=0.0000000000000001"
                var coefs = new Dictionary<string, decimal>
                {
                    { "Duration", 0.0001m },
                    { "CPU", 0.0001m },
                    //{ "Init price", 0.0000000000000001m }
                };
                // name "ai" as defined in plugins/*.json
                var preset = new Preset(preset_name, "ai", coefs);

                Provider.AddPreset(preset, out string args, out string info);
                Console.WriteLine($"Args {args}");
                Console.WriteLine($"Args {info}");

            }
            Provider.ActivatePreset(preset_name);

            foreach (string preset in presets)
            {
                Console.WriteLine($"Preset {preset}");
            }
            
            return Provider.Run(yagnaOptions.AppKey, Network.Goerli, yagnaOptions.YagnaApiUrl, true, true);
        }

        void OnYagnaErrorDataRecv(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine($"[Error]: {e.Data}");
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

        private async Task StartActivityLoop()
        {
            _logger?.LogInformation("Starting monitoring activities");
            var token = _tokenSource.Token;

            var options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };

            token.Register(HttpClient.CancelPendingRequests);

            DateTime newReconnect = DateTime.Now;
            while (!token.IsCancellationRequested)
            {
                _logger?.LogInformation("Monitoring activities");
                var now = DateTime.Now;
                if (newReconnect > now)
                {
                    await Task.Delay(newReconnect - now);
                }
                newReconnect = now + TimeSpan.FromMinutes(2);
                if (token.IsCancellationRequested)
                {
                    token.ThrowIfCancellationRequested();
                }

                try
                {
                    var stream = await HttpClient.GetStreamAsync("/activity-api/v1/_monitor");
                    using StreamReader reader = new StreamReader(stream);
                    token.Register(() =>
                    {
                        _logger?.LogInformation("stop");
                        reader.Close();
                    });
                    StringBuilder dataBuilder = new StringBuilder();
                    while (true)
                    {
                        if (token.IsCancellationRequested)
                        {
                            token.ThrowIfCancellationRequested();
                        }
                        var line = await reader.ReadLineAsync();
                        if (line.StartsWith("data:"))
                        {
                            dataBuilder.Append(line.Substring(5).TrimStart());
                            _logger?.LogInformation("got line {0}", line);
                        }
                        else if (line == "")
                        {
                            var json = dataBuilder.ToString();
                            dataBuilder.Clear();
                            _logger?.LogInformation("got json {0}", json);
                            try
                            {
                                var ev = JsonSerializer.Deserialize<TrackingEvent>(json, options);
                                var _activities = ev?.Activities ?? new List<ActivityState>();
                                if (_activities.Any()) {
                                    this.CurrentJob = null;
                                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.CurrentJob)));
                                }
                                try
                                {
                                    OnPropertyChanged("Activities");
                                }
                                catch (Exception e)
                                {
                                    _logger?.LogError(e, "Failed to send notification");
                                }
                            }
                            catch (JsonException e)
                            {
                                _logger?.LogError(e, "Invalid monitoring event: {0}", json);
                                break;
                            }
                        }
                    }
                }
                catch (HttpRequestException e)
                {
                    _logger?.LogError(e, "failed to get exe-units status");
                }
                catch (IOException e)
                {
                    _logger?.LogError(e, "status loop failure");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await (this as IGolem).Stop();
        }
    }
}
