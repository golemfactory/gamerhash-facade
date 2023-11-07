using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GolemUI.Model
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

        public Dictionary<string, float>? Usage { get; set; }

        public string? ExeUnit { get; set; }
    }
}
