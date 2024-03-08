using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Golem.Model
{
    public class ActivityState
    {
        public enum StateType
        {
            New,
            Initialized,
            Deployed,
            Ready,
            Terminated,
            Unresponsive,
        }

        public string? Id { get; set; }

        public string? AgreementId { get; set; }

        public StateType State { get; set; }

        public Dictionary<string, decimal>? Usage { get; set; }

        public string? ExeUnit { get; set; }
    }

    public class TrackingEvent
    {
        public DateTime Ts { get; set; }

        public List<ActivityState> Activities { get; set; } = new List<ActivityState>();
    }
}
