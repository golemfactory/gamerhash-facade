using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using Xunit.Abstractions;
using Golem.Yagna;
using System.Diagnostics;

namespace App
{
    public class SampleApp
    {

        public Process CreateProcess()
        {
            var app = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "py", "dist", "app", ProcessFactory.BinName("app"));
            return ProcessFactory.CreateProcess(app, " --network goerli --subnet-tag public", true, new Dictionary<string, string>());
        }
    }
}
