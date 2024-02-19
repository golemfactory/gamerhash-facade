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


        public GolemPrice ToPrice()
        {
            return GolemPrice.From(this.InitialPrice, this.UsageCoeffs);
        }
    }

    public class ExeUnit
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
        private readonly IProvider _parent;

        public PresetConfigService(IProvider parent)
        {
            _parent = parent;
        }

        public void InitilizeDefaultPresets()
        {
            var activePresetNames = ActivePresetsNames;

            var presets = AllPresets.ToDictionary(preset => preset.Name, preset => preset);

            var exeUnits = ExeUnits;

            foreach (ExeUnit exeUnit in exeUnits)
            {
                var presetName = defaultPresetName(exeUnit);
                if (!presets.ContainsKey(presetName))
                {
                    var coeffs = new GolemPrice { EnvPerHour = 0, GpuPerHour = 0, NumRequests = 0, StartPrice = 0 };
                    var preset = new Preset(presetName, exeUnit.Name, coeffs.GolemCounters());

                    AddPreset(preset);
                }
                if (!activePresetNames.Contains(presetName))
                {
                    ActivatePreset(presetName);
                    activePresetNames.Add(presetName);
                }
            }

            var defaultPresetNames = exeUnits.Select(exeUnit => defaultPresetName(exeUnit));

            foreach (string preset in activePresetNames)
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

        static String defaultPresetName(ExeUnit exeUnit)
        {
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
            Console.WriteLine($"Deactivate {0}", presetName);
            _parent.ExecToText(args);
        }

        public Preset? GetPreset(string name)
        {
            var preset = AllPresets.Where(p => p.Name == name).SingleOrDefault();
            return preset;
        }

        public string AddPreset(Preset preset)
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
            return _parent.ExecToText(args);
        }

        public void UpdateAllPrices(GolemPrice price)
        {
            var defaultPresetNames = new HashSet<String>(ExeUnits.Select(defaultPresetName));
            foreach (String presetName in defaultPresetNames)
            {
                UpdatePrice(presetName, price);
            }
        }

        public string UpdatePrice(string preset, GolemPrice price)
        {
            var priceArgs = BuildPresetArgs(price);

            var args = "preset update --no-interactive --name".Split().ToList();
            args.Add(preset);
            args.AddRange(priceArgs.ToArray());
            return _parent.ExecToText(args);
        }

        public List<string> BuildPresetArgs(GolemPrice price)
        {
            var priceDict = price.GolemCounters();
            var priceArgs = new List<string>();
            foreach (KeyValuePair<string, decimal> priceKV in priceDict)
            {
                var priceValue = priceKV.Value.ToString(CultureInfo.InvariantCulture);
                priceArgs.Add("--price");
                priceArgs.Add($"{priceKV.Key}={priceValue}");
            }
            return priceArgs;
        }
    }
}
