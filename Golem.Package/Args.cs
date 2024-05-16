using CommandLine;

[Verb("build", HelpText = "Builds package.")]
public class BuildArgs
{
    [Option('t', "target", Default = "package", Required = false, HelpText = "Directory where binaries will be generated relative to working dir")]
    public required string Target { get; set; }
    [Option('y', "yagna-version", Default = "pre-rel-v0.16.0-ai-rc16", Required = false, HelpText = "Yagna version github tag")]
    public required string GolemVersion { get; set; }
    [Option('r', "runtime-version", Default = "pre-rel-v0.2.2-rc1", Required = false, HelpText = "Runtime version github tag")]
    public required string RuntimeVersion { get; set; }
    [Option('c', "dont-clean", Default = false, Required = false, HelpText = "Remove temporary directories")]
    public required bool DontClean { get; set; }
    [Option('d', "dll-dir", Default = null, Required = false, HelpText = "dll directory")]
    public required string? DllDir { get; set; }
    [Option('p', "dll-file-patterns", Default = "Golem.dll,GolemLib.dll", Required = false, HelpText = "Coma separated dll file name patterns")]
    public required string DllFilePatterns { get; set; }

}

[Verb("download", HelpText = "Downloads package from public repository.")]
public class DownloadArgs
{
    [Option('t', "target", Default = "modules", Required = false, HelpText = "Directory where binaries will be generated relative to working dir")]
    public required string Target { get; set; }
    [Option('p', "version", Default = "v4.0.0", Required = false, HelpText = "Gamerhash module integration package version")]
    public required string PackageVersion { get; set; }
}
