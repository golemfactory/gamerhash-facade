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
        public static Command CreateProcess<OUT_WRITER, ERR_WRITER>(string executable, IEnumerable<object>? args, Dictionary<string, string> env, OUT_WRITER stdOut, ERR_WRITER errOut)
        where OUT_WRITER : TextWriter
        where ERR_WRITER : TextWriter
        {
            var argList = args?.ToList();
            argList?.RemoveAll(s => string.IsNullOrWhiteSpace((string?)s));
            var executablePath = Path.GetFullPath(executable);
            var workDir = Directory.GetParent(executablePath)?.ToString() ?? "";

            return Command
                .Run(executablePath, argList, options => updateOptions(options, workDir, env))
                .RedirectTo(stdOut)
                .RedirectStandardErrorTo(errOut);
        }

        static Shell.Options updateOptions(Shell.Options options, string workDir, Dictionary<string, string> env)
        {
            options = options
                .EnvironmentVariables(env)
                .WorkingDirectory(workDir)
                .ThrowOnError(true)
                .DisposeOnExit(false)
                .StartInfo(info =>
                {
                    info.CreateNoWindow = true;
                });
            return options;
        }

        public static string BinName(string name)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.ChangeExtension(name, ".exe");
            }
            else
            {
                return Path.GetFileNameWithoutExtension(name);
            }
        }

        // TODO instead of passing logger as param and keeping `cmd` as variable, keep ProcessFactory (with `cmd` and `logger`) as a variable.
        public static async Task<int> StopCmd(Command cmd, int stopTimeoutMs = 30_000, ILogger? logger = null)
        {
            if (!cmd.Process.HasExited)
            {
                if (!await cmd.TrySignalAsync(CommandSignal.ControlC))
                {
                    cmd.Kill();
                }

                CancellationTokenSource stopTimeoutTokenSrc = new CancellationTokenSource();
                var stopTimeoutToken = stopTimeoutTokenSrc.Token;
                stopTimeoutTokenSrc.CancelAfter(stopTimeoutMs);
                try
                {
                    await cmd.Process.WaitForExitAsync(stopTimeoutToken);
                }
                catch (TaskCanceledException err)
                {
                    logger?.LogWarning($"Failed to stop process. Killing it. Err: {err.Message}");
                    cmd.Kill();
                }
            }
            return cmd.Process.ExitCode;
        }
    }
}
