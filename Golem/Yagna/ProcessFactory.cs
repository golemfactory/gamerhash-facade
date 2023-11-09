using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Golem.Yagna
{
    public class ProcessFactory
    {
        public static Process CreateProcess(string fileName, string args, bool openConsole, string exeUnitPath)
        {
            var initStartInfo = () => CreateProcessStartInfo(fileName, args, openConsole);
            return CreateProcess(fileName, initStartInfo, openConsole, exeUnitPath);
        }

        public static Process CreateProcess(string fileName, List<string> args, bool openConsole, string exeUnitPath)
        {
            var initStartInfo = () => CreateProcessStartInfo(fileName, args, openConsole);
            return CreateProcess(fileName, initStartInfo, openConsole, exeUnitPath);
        }

        private static Process CreateProcess(string fileName, Func<ProcessStartInfo> initStartInfo, bool openConsole, string exeUnitPath)
        {
            var startInfo = initStartInfo();

            foreach (var (k, v) in GetEnvironmentVariables(exeUnitPath))
            {
                startInfo.EnvironmentVariables.Add(k, v);
            }

            var process = new Process
            {
                StartInfo = startInfo,
            };

            return process;
        }

        private static Dictionary<string, string> GetEnvironmentVariables(string exeUnitPath)
        {
            var env = new Dictionary<string, string>
            {
                { "GSB_URL", "tcp://127.0.0.1:11501" },
                { "YAGNA_API_URL", "http://127.0.0.1:11502" },
                { "SUBNET", "testnet" },
                { "YA_PAYMENT_NETWORK_GROUP", "testnet" },
                { "YA_NET_BIND_URL", "udp://0.0.0.0:12503" },
                { "EXE_UNIT_PATH", exeUnitPath },
                //{ "YA_NET_RELAY_HOST", "10.0.2.2:7464" },
            };
            return env;
        }

        private static ProcessStartInfo CreateProcessStartInfo(string fileName, string args, bool openConsole)
        {
            var startInfo = CreateProcessStartInfo(fileName, openConsole);
            startInfo.Arguments = args;

            return startInfo;
        }

        private static ProcessStartInfo CreateProcessStartInfo(string fileName, List<string> args, bool openConsole)
        {
            var startInfo = CreateProcessStartInfo(fileName, openConsole);
            args.ForEach(startInfo.ArgumentList.Add);

            return startInfo;
        }

        private static ProcessStartInfo CreateProcessStartInfo(string fileName, bool openConsole)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
            };

            if (openConsole)
            {
                startInfo.RedirectStandardOutput = false;
                startInfo.RedirectStandardError = false;
                startInfo.CreateNoWindow = false;
            }
            else
            {
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.CreateNoWindow = true;
            }

            return startInfo;
        }
    }
}
