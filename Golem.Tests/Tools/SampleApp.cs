using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using Xunit.Abstractions;
using Golem.Yagna;
using System.Diagnostics;
using Golem.IntegrationTests.Tools;

namespace App
{
    class SampleApp
    {
        public static Process CreateProcess(Dictionary<string, string> env)
        {
            var app = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "app", ProcessFactory.BinName("app"));
            var process = ProcessFactory.CreateProcess(app, " --network goerli --subnet-tag public", true, env);
            GolemRunnable.AddShutdownHook(process);
            return process;
        }
    }
}
