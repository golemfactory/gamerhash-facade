using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

using GolemLib.Types;

namespace Golem.Yagna
{
    public class Preset
    {
        [JsonConstructor]
        public Preset(string name, string exeunitName, Dictionary<string, decimal> usageCoeffs)
        {
            Name = name;
            ExeunitName = exeunitName;
            PricingModel = "linear";
            UsageCoeffs = usageCoeffs;
        }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("exeunit-name")]
        public string ExeunitName { get; set; }

        [JsonPropertyName("pricing-model")]
        public string? PricingModel { get; set; }

        [JsonPropertyName("initial-price")]
        public decimal? InitialPrice { get; set; }

        [JsonPropertyName("usage-coeffs")]
        public Dictionary<string, decimal> UsageCoeffs { get; set; }
    }

    public class PresetConfigService
    {
        private readonly Provider _parent;
        public string DefaultPresetName => "ai-dummy";

        internal PresetConfigService(Provider parent)
        {
            _parent = parent;
        }

        public void InitilizeDefaultPreset()
        {
            var presets = ActivePresetsNames;
            if (!presets.Contains(DefaultPresetName))
            {

                var coeffs = new Dictionary<string, decimal>
                {
                    { "ai-runtime.requests", 0 },
                    { "golem.usage.duration_sec", 0 },
                    { "golem.usage.gpu-sec", 0 },
                    { "Initial", 0 }
                };

                // name "ai" as defined in plugins/*.json
                var preset = new Preset(DefaultPresetName, "ai", coeffs);

                AddPreset(preset, out string info);

            }
            ActivatePreset(DefaultPresetName);

            foreach (string preset in presets)
            {
                if (preset != DefaultPresetName)
                {
                    DeactivatePreset(preset);
                }
            }
        }

        public IList<string> ActivePresetsNames
        {
            get
            {
                return _parent.Exec<List<string>>("--json preset active".Split()) ?? new List<string>();
            }
        }

        public List<Preset> AllPresets
        {
            get
            {
                var presets = _parent.Exec<List<Preset>>("preset --json list".Split()) ?? new List<Preset>();
                return presets;
            }
        }

        public string ActivatePreset(string presetName)
        {
            var args = "preset activate".Split().ToList();
            args.Add(presetName);
            return _parent.ExecToText(args);
        }
        public void DeactivatePreset(string presetName)
        {
            var args = "preset deactivate".Split().ToList();
            args.Add(presetName);
            _parent.ExecToText(args);
        }

        public Preset? GetPreset(string name)
        {
            var preset = AllPresets.Where(p => p.Name == name).SingleOrDefault();
            return preset;
        }

        public void AddPreset(Preset preset, out string info)
        {
            List<string> args = "preset create --no-interactive".Split().ToList();
            args.Add("--preset-name");
            args.Add(preset.Name);
            args.Add("--exe-unit");
            args.Add(preset.ExeunitName);
            if (preset.PricingModel != null)
            {
                foreach (KeyValuePair<string, decimal> usageKV in preset.UsageCoeffs)
                {
                    string coeffUsage = usageKV.Value.ToString(CultureInfo.InvariantCulture);
                    args.Add("--price");
                    args.Add($"{usageKV.Key}={coeffUsage}");
                }
            }
            info = _parent.ExecToText(args);
        }

        public void UpdatePreset(Preset preset, out string info)
        {
            List<string> args = "preset create --no-interactive".Split().ToList();

            args.Add("--preset-name");
            args.Add(preset.Name);
            args.Add("--exe-unit");
            args.Add(preset.ExeunitName);
            if (preset.PricingModel != null)
            {
                foreach (KeyValuePair<string, decimal> kv in preset.UsageCoeffs)
                {
                    var coeffsUsage = kv.Value.ToString(CultureInfo.InvariantCulture);
                    args.Add(" --price ");
                    args.Add($"{kv.Key}={coeffsUsage}");
                }
            }
            info = _parent.ExecToText(args);
        }

        public void UpdatePrices(string presetName, IDictionary<string, decimal> prices, out string info)
        {
            List<string> args = "preset update --no-interactive --name".Split().ToList();
            args.Add(presetName);

            foreach (KeyValuePair<string, decimal> priceKV in prices)
            {
                var priceValue = priceKV.Value.ToString(CultureInfo.InvariantCulture);
                args.Add("--price");
                args.Add($"{priceKV.Key}={priceValue}");
            }

            info = _parent.ExecToText(args);
        }
    }
}
