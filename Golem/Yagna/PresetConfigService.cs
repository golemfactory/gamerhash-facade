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

    class ExeUnit
    {
        [JsonConstructor]
        public ExeUnit(string name, string version)
        {
            Name = name;
            Version = version;
        }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("exeunit-name")]
        public string Version { get; set; }
    }
    
    public class PresetConfigService
    {
        private readonly Provider _parent;

        internal PresetConfigService(Provider parent)
        {
            _parent = parent;
        }

        public void InitilizeDefaultPresets()
        {
            var presets = ActivePresetsNames;

            foreach (ExeUnit exeUnit in ExeUnits) {
                if (!presets.Contains(defaultPresetName(exeUnit)))
                {

                var coeffs = new Dictionary<string, decimal>
                {
                    { "ai-runtime.requests", 0 },
                    { "golem.usage.duration_sec", 0 },
                    { "golem.usage.gpu-sec", 0 },
                    { "Initial", 0 }
                };

                // name "ai" as defined in plugins/*.json
                var preset = new Preset(defaultPresetName(exeUnit), exeUnit.Name, coeffs);

                AddPreset(preset, out string info);

                }
                ActivatePreset(defaultPresetName(exeUnit));
            }

            var defaultPresetNames = new HashSet<String>(ExeUnits.Select(defaultPresetName));

            foreach (string preset in presets)
            {
                if (!defaultPresetNames.Contains(preset))
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

        public IList<Preset> DefaultPresets
        {
            get
            {
                var defaultPresetNames = ExeUnits.Select(defaultPresetName);
                var defaultPresets = AllPresets.FindAll((preset) => defaultPresetNames.Contains(preset.Name));
                return defaultPresets;
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

        static String defaultPresetName(ExeUnit exeUnit) {
            return $"{exeUnit.Name}";
        }

        List<ExeUnit> ExeUnits
        {
            get
            {
                return _parent.Exec<List<ExeUnit>>("--json exe-unit list".Split()) ?? new List<ExeUnit>();
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

        public void UpdatePrices(IDictionary<string, decimal> prices)
        {
            var defaultPresetNames = new HashSet<String>(ExeUnits.Select(defaultPresetName));

            var priceArgs = new List<string>();
            foreach (KeyValuePair<string, decimal> priceKV in prices)
            {
                var priceValue = priceKV.Value.ToString(CultureInfo.InvariantCulture);
                priceArgs.Add("--price");
                priceArgs.Add($"{priceKV.Key}={priceValue}");
            }

            foreach (String presetName in defaultPresetNames) {
                var args = "preset update --no-interactive --name".Split().ToList();
                args.Add(presetName); 
                args.AddRange(priceArgs.ToArray());
                var _result = _parent.ExecToText(args);
            }
        }
    }
}
