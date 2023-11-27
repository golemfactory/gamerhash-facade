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
using Microsoft.Extensions.Logging;

namespace App
{
    public class SampleApp: GolemRunnable
    {
        private readonly Dictionary<string, string> _env;

        public SampleApp(string dir, Dictionary<string, string> env, ILogger logger) : base(dir, logger)
        {
            _env = env;
            var app_filename = ProcessFactory.BinName("app");
            var app_src = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "app", app_filename);
            var app_dst = Path.Combine(dir, "modules", "golem", app_filename);
            File.Copy(app_src, app_dst);
        }

        public override bool Start()
        {
            var working_dir = Path.Combine(_dir, "modules", "golem-data", "yagna");
            Directory.CreateDirectory(working_dir);
            return StartProcess("app", Path.Combine(_dir, "modules", "golem-data", "yagna"), "--network goerli --subnet-tag public", _env, true);
        }
    }
}
