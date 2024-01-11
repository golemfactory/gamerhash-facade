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
        public OutputLogger(ILogger? logger, LogLevel lvl, string prefix)
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

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (value == '\n') {
                WriteLine();
            } else {
                _strBuilder.Append(value);
            }
        }

        public override void WriteLine(string? value)
        {
            _logger.Log(_lvl, $"{_prefix}: {value}");
        }

        public override void WriteLine()
        {
            var line = _strBuilder.ToString();
            _strBuilder = new StringBuilder();
            _logger.Log(_lvl, $"{_prefix}: {line}");
        }
    }

    public class ProcessFactory
    {
        public static Command CreateProcess<OUT_WRITER, ERR_WRITER>(string executable, string args, Dictionary<string, string> env, OUT_WRITER stdOut, ERR_WRITER errOut)
        where OUT_WRITER : TextWriter
        where ERR_WRITER: TextWriter
        {
            var args_list = args.Split(null).ToList();
            args_list.RemoveAll(string.IsNullOrEmpty);
            return CreateProcess(executable, args_list, env, stdOut, errOut);
        }

        public static Command CreateProcess<OUT_WRITER, ERR_WRITER>(string executable, IEnumerable<object>? args, Dictionary<string, string> env, OUT_WRITER stdOut, ERR_WRITER errOut) 
        where OUT_WRITER : TextWriter
        where ERR_WRITER: TextWriter
        {
            var executablePath = Path.GetFullPath(executable);
            var workDir = Directory.GetParent(executablePath)?.ToString();

            return Command
                .Run(executablePath, args, options => updateOptions(options, workDir, env))
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
                    // info.RedirectStandardOutput = true;
                    // info.RedirectStandardError = true;
                    info.CreateNoWindow = true;
                    // info.UseShellExecute = false;
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
        public static async Task<int> StopCmd(Command cmd) {
                if (!cmd.Process.HasExited) {
                    if (!await cmd.TrySignalAsync(CommandSignal.ControlC))
                    {
                        cmd.Kill();
                    }
                    await cmd.Process.WaitForExitAsync();
                }
                return cmd.Process.ExitCode;
        }
    }
}
