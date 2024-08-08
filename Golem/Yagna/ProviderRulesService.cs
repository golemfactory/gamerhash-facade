using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

namespace Golem.Yagna
{
    public enum RuleCategory
    {
        Blacklist,
        AllowList
    }

    public class RestrictRule
    {
        [JsonPropertyName("enabled")]
        public required bool Enabled { get; init; }

        [JsonPropertyName("identity")]
        public required List<string> Identity { get; init; }

        [JsonPropertyName("certified")]
        public required List<string> Certified { get; init; }

    }

    public class Rules
    {
        [JsonPropertyName("blacklist")]
        public required RestrictRule Blacklist { get; init; }

        [JsonPropertyName("allow-only")]
        public required RestrictRule AllowOnly { get; init; }
    }


    public class Rule
    {
        private readonly Provider _provider;
        public RuleCategory Type { get; init; }


        public Rule(Provider provider, RuleCategory type)
        {
            _provider = provider;
            Type = type;
        }

        public bool Enabled
        {
            get
            {
                return Type switch
                {
                    RuleCategory.Blacklist => List()?.Blacklist.Enabled ?? false,
                    RuleCategory.AllowList => List()?.AllowOnly.Enabled ?? false,
                    _ => throw new System.Exception("Invalid rule type")
                };
            }

            set => Enable(value);
        }

        public List<string> Identities
        {
            get => List(Type)?.Identity ?? new List<string>();
        }

        public List<string> Certificates
        {
            get => List(Type)?.Certified ?? new List<string>();
        }

        public string AddCertificate(string certPath)
        {
            var args = $"rule add {GetRuleCommand()} certified import-cert".Split().ToList();
            // Path can contains spaces, so we need to add it as separate argument.
            args.Add(certPath);
            return _provider.ExecToText(args);
        }

        public string AddIdentity(string nodeID)
        {
            var args = $"rule add {GetRuleCommand()} identity {nodeID}".Split().ToList();
            return _provider.ExecToText(args);
        }

        public Rules? List()
        {
            var args = $"rule list --json".Split().ToList();
            return _provider.Exec<Rules>(args);
        }

        public RestrictRule? List(RuleCategory type)
        {
            return type switch
            {
                RuleCategory.Blacklist => List()?.Blacklist,
                RuleCategory.AllowList => List()?.AllowOnly,
                _ => throw new System.Exception("Invalid rule type")
            };

        }

        private string Enable(bool enable)
        {
            var enable_str = enable ? "enable" : "disable";
            var args = $"rule {enable_str} {GetRuleCommand()}".Split().ToList();
            return _provider.ExecToText(args);
        }

        private string GetRuleCommand()
        {
            return Type switch
            {
                RuleCategory.Blacklist => "blacklist",
                RuleCategory.AllowList => "allow-only",
                _ => throw new System.Exception("Invalid rule type")
            };
        }
    }
}


