
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Golem.Model
{
    public class YagnaAgreementInfo
    {
        public string AgreementID
        {
            get => Id;
            set => Id = value;
        }
        public required string Id { get; set; }
        public DateTime ValidTo { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum AgreementTerminator
    {
        Requestor,
        Provider,
    }

    public enum AgreementEventType
    {
        AgreementApprovedEvent,
        AgreementRejectedEvent,
        AgreementCancelledEvent,
        AgreementTerminatedEvent,
    }

    public class Reason
    {
        public required string Message { get; set; }
        [JsonPropertyName("golem.requestor.code")]
        public string? RequestorCode { get; set; }
        [JsonPropertyName("golem.provider.code")]
        public string? ProviderCode { get; set; }

        [SetsRequiredMembers]
        public Reason(string code, string fullMessage) => (Message, ProviderCode, RequestorCode) = (fullMessage, code, null);
    }

    public class YagnaAgreementEvent
    {
        public required string AgreementID { get; init; }
        public required DateTime EventDate { get; init; }
        public Dictionary<string, string>? Reason { get; init; }
        public AgreementTerminator? Terminator { get; init; }
        public required AgreementEventType EventType { get; init; }


        public string? Message
        {
            get
            {
                return Reason?.GetValueOrDefault("message");
            }
        }

        public string? Code
        {
            get
            {
                return Terminator switch
                {
                    AgreementTerminator.Requestor => Reason?.GetValueOrDefault("golem.requestor.code"),
                    AgreementTerminator.Provider => Reason?.GetValueOrDefault("golem.provider.code"),
                    _ => null,
                };
            }
        }

    }
}

