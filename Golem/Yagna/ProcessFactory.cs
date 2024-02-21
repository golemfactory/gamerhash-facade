using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Medallion.Shell;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Golem.Yagna
{
    public class OutputLogger : TextWriter
    {
        public OutputLogger(ILogger? logger, string prefix, LogLevel lvl = LogLevel.Information)
        {
            _logger = logger ?? NullLogger.Instance;
            _lvl = lvl;
            _prefix = prefix;
            _strBuilder = new StringBuilder();
        }

        private readonly ILogger _logger;
        private readonly LogLevel _lvl;
        private readonly string _prefix;
        private StringBuilder _strBuilder;
        private readonly object _strBuilderLock = new object();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (value == '\n')
            {
                WriteLine();
            }
            else
            {
                lock (_strBuilderLock)
                {
                    _strBuilder.Append(value);
                }
            }
        }

        public override void WriteLine()
        {
            string line;
            lock (_strBuilderLock)
            {
                line = _strBuilder.ToString();
                _strBuilder = new StringBuilder();
            }
            _logger.Log(_lvl, $"{_prefix}: {line}");
        }
    }

    public class ProcessFactory
    {
        public static Process StartProcess(string executable, IEnumerable<object> args, Dictionary<string, string> env, bool redirectOutput = false)
        {

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput,
                CreateNoWindow = true,
            };

            foreach (var arg in args)
            {
                var argTxt = arg.ToString();
                if (!String.IsNullOrWhiteSpace(argTxt))
                    startInfo.ArgumentList.Add(argTxt);
            }

            foreach (var (k, v) in env)
            {
                if (startInfo.EnvironmentVariables.ContainsKey(k))
                    startInfo.EnvironmentVariables[k] = v;
                else
                    startInfo.EnvironmentVariables.Add(k, v);
            }

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };

            return process.Start() ?
                process : throw new GolemProcessException($"Failed to start Golem process: {process}");
        }

        private static void BindOutputEventHandlers(Process proc)
        {
            proc.OutputDataReceived += OnOutputDataRecv;
            proc.ErrorDataReceived += OnErrorDataRecv;
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();
        }

        private static void OnOutputDataRecv(object sender, DataReceivedEventArgs e)
        {
            // _logger.LogInformation($"{e.Data}");
            Console.WriteLine($">>> {e.Data}");
        }
        private static void OnErrorDataRecv(object sender, DataReceivedEventArgs e)
        {
            // _logger.LogInformation($"{e.Data}");
            Console.WriteLine($">>> {e.Data}");
        }

        public static string BinName(string name)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                Path.ChangeExtension(name, ".exe") : Path.GetFileNameWithoutExtension(name);
        }

        // TODO instead of passing logger as param and keeping `cmd` as variable, keep ProcessFactory (with `cmd` and `logger`) as a variable.
        public static async Task<int> StopProcess(Process proc, int stopTimeoutMs = 30_000, ILogger? logger = null)
        {
            if (proc.HasExited)
            {
                logger?.LogWarning("Process has exited already.");
                return proc.ExitCode;
            }
            if (Command.TryAttachToProcess(proc.Id, out var cmd))
            {
                if (!await cmd.TrySignalAsync(CommandSignal.ControlC))
                {
                    logger?.LogWarning("Failed to signal Ctrl-C to process. Killing it.");
                    cmd.Kill();
                }
                else
                {
                    logger?.LogInformation("Signaled process to stop");
                }

                CancellationTokenSource stopTimeoutTokenSrc = new CancellationTokenSource();
                stopTimeoutTokenSrc.CancelAfter(stopTimeoutMs);
                try
                {
                    logger?.LogInformation("Waiting for process to stop.");
                    await proc.WaitForExitAsync();
                    logger?.LogInformation("Process stopped.");
                }
                catch (TaskCanceledException err)
                {
                    logger?.LogWarning($"Failed to stop process. Killing it. Err: {err.Message}");
                    cmd.Kill();
                }
            }
            else
            {
                logger?.LogError("Failed to attach to process. Killing.");
                proc.Kill();
            }
            return proc.ExitCode;
        }
    }

    interface IGolemException { }

    public class GolemProcessException : Exception, IGolemException
    {

        public GolemProcessException(string message)
            : base(message)
        {
        }
    }

    public class GolemException : Exception, IGolemException
    {
        public GolemException(string message)
            : base(message)
        {
        }
    }
}
