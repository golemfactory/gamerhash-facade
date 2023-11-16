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

        public IList<string> ActivePresetsNames
        {
            get
            {
                return _parent.Exec<List<string>>("--json preset active") ?? new List<string>();
            }
        }

        public List<Preset> AllPresets
        {
            get
            {
                var presets = _parent.Exec<List<Preset>>("preset --json list") ?? new List<Preset>();
                return presets;
            }
        }

        public string ActivatePreset(string presetName)
        {
            return _parent.ExecToText($"preset activate {presetName}");
        }
        public void DeactivatePreset(string presetName)
        {
            _parent.ExecToText($"preset deactivate {presetName}");
        }

        public Preset? GetPreset(string name)
        {
            var preset = AllPresets.Where(p => p.Name == name).SingleOrDefault();
            return preset;
        }

        public void AddPreset(Preset preset, out string args, out string info)
        {
            StringBuilder cmd = new StringBuilder("preset create --no-interactive", 60);

            cmd.Append(" --preset-name \"").Append(preset.Name).Append('"');
            cmd.Append(" --exe-unit \"").Append(preset.ExeunitName).Append('"');
            if (preset.PricingModel != null)
            {
                foreach (KeyValuePair<string, decimal> kv in preset.UsageCoeffs)
                {
                    cmd.Append(" --price ").Append(kv.Key).Append("=").Append(kv.Value.ToString(CultureInfo.InvariantCulture));
                }
            }
            args = cmd.ToString();
            info = _parent.ExecToText(cmd.ToString());
        }

        public void UpdatePreset(Preset preset, out string args, out string info)
        {
            StringBuilder cmd = new StringBuilder("preset create --no-interactive", 60);

            cmd.Append(" --preset-name \"").Append(preset.Name).Append('"');
            cmd.Append(" --exe-unit \"").Append(preset.ExeunitName).Append('"');
            if (preset.PricingModel != null)
            {
                foreach (KeyValuePair<string, decimal> kv in preset.UsageCoeffs)
                {
                    cmd.Append(" --price ").Append(kv.Key).Append("=").Append(kv.Value.ToString(CultureInfo.InvariantCulture));
                }
            }
            args = cmd.ToString();
            info = _parent.ExecToText(cmd.ToString());
        }

        public void UpdatePrices(string presetName, IDictionary<string, decimal> prices)
        {
            var pargs = String.Join(" ", from e in prices select $"--price {e.Key}={e.Value.ToString(CultureInfo.InvariantCulture)}");

            string args = $"preset update --no-interactive --name {presetName} {pargs}";
            var _result = _parent.ExecToText(args);
        }
    }
}