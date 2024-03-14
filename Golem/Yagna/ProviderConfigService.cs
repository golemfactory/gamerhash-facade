using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Golem
{
    using global::Golem.Yagna.Types;
    using global::Golem.Yagna;
    using GolemLib.Types;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    namespace GolemUI.Src
    {
        public class ProviderConfigService
        {
            private readonly Provider _provider;

            private readonly ILogger _logger;

            public Network Network { get; private set; }

            public ProviderConfigService(Provider provider, Network network, ILoggerFactory loggerFactory)
            {
                _provider = provider;
                loggerFactory ??= NullLoggerFactory.Instance;
                _logger = loggerFactory.CreateLogger<ProviderConfigService>();
                Network = network;
            }

            public string WalletAddress
            {
                get
                {
                    return _provider.Config?.Account ?? "";
                }
            }

            public GolemPrice GolemPrice
            {
                get
                {
                    var presets = _provider.PresetConfig.DefaultPresets;

                    if (presets.Count == 0)
                        return new GolemPrice();

                    var golemPrice = presets[0].ToPrice();

                    if (presets.Count == 1)
                        return golemPrice;

                    // All preset prices should have the same value. Unify if they don't.
                    for (int i = 1; i < presets.Count; i++)
                    {
                        var otherPreset = presets[i];
                        var otherPrice = otherPreset.ToPrice();

                        if (!golemPrice.Equals(otherPrice))
                        {
                            _provider.PresetConfig.UpdatePrice(otherPreset.Name, golemPrice);
                        }
                    }

                    return golemPrice;
                }

                set
                {
                    _provider.PresetConfig.InitilizeDefaultPresets();
                    _provider.PresetConfig.UpdateAllPrices(value);
                }
            }

            Dictionary<string, decimal> golemPriceToDict(GolemPrice price)
            {
                return new Dictionary<string, decimal>
                    {
                        { "ai-runtime.requests", price.NumRequests },
                        { "golem.usage.duration_sec", price.EnvPerSec },
                        { "golem.usage.gpu-sec", price.GpuPerSec },
                        { "Initial", price.StartPrice }
                    };
            }

            private GolemPrice presetIntoPrice(Preset preset)
            {
                if (!preset.UsageCoeffs.TryGetValue("ai-runtime.requests", out var numRequests))
                    numRequests = 0;
                if (!preset.UsageCoeffs.TryGetValue("golem.usage.duration_sec", out var duration))
                    duration = 0;
                if (!preset.UsageCoeffs.TryGetValue("golem.usage.gpu-sec", out var gpuSec))
                    gpuSec = 0;

                var initPrice = preset.InitialPrice ?? 0m;

                return new GolemPrice
                {
                    EnvPerSec = duration,
                    StartPrice = initPrice,
                    GpuPerSec = gpuSec,
                    NumRequests = numRequests
                };
            }

            public void UpdateAccount(string? account, Action update)
            {
                _logger.LogInformation($"Updating provider account to {account}");
                var config = _provider.Config;
                if (config != null)
                {
                    if (config.Account == account)
                    {
                        _logger.LogInformation("Provider account has not changed");
                        return;
                    }
                    config.Account = account;
                    _logger.LogInformation($"Set Provider account '{account}'");
                    _provider.Config = config;
                    update();
                }
                else
                {
                    _logger.LogWarning("Unable to get provider config");
                }
            }
        }
    }

}
