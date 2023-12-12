using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Golem.Model
{
    public class ActivityStatePair
    {
        (ActivityState.StateType, ActivityState.StateType?) state { get; set; }

        String? reason { get; set; }

        String? error_message { get; set; }
    }
}
