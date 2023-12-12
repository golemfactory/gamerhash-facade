using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Golem.Model
{
    public class ActivityStatePair
    {
        public List<ActivityState.StateType?> state { get; set; }

        public String? reason { get; set; }

        public String? error_message { get; set; }

        public ActivityState.StateType currentState()
        {
            if (state.Count == 0 || state[0] == null) {
                throw new Exception($"Missing current state for activity");
            }
            #pragma warning disable CS8629 // Field is not null.
            return state[0].Value;
            #pragma warning restore CS8629
        }

        public ActivityState.StateType? oldState()
        {
            if (state.Count > 1) {
                return state[1];
            }
            return null;
        }
    }
}
