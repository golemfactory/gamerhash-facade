using CommandLine;

[Verb("build", HelpText = "Builds package.")]
public class BuildArgs
{
    [Option('t', "target", Default = "package", Required = false, HelpText = "Directory where binaries will be generated relative to working dir")]
    public required string Target { get; set; }
    [Option('y', "yagna-version", Default = "pre-rel-v0.14.1-rc7_combined", Required = false, HelpText = "Yagna version github tag")]
    public required string GolemVersion { get; set; }
    [Option('r', "runtime-version", Default = "pre-rel-v0.1.0-rc25_waiting_for_api", Required = false, HelpText = "Runtime version github tag")]
    public required string RuntimeVersion { get; set; }
    [Option('c', "dont-clean", Default = false, Required = false, HelpText = "Remove temporary directories")]
    public required bool DontClean { get; set; }
}

[Verb("download", HelpText = "Downloads package from public repository.")]
public class DownloadArgs
{
    [Option('t', "target", Default = "modules", Required = false, HelpText = "Directory where binaries will be generated relative to working dir")]
    public required string Target { get; set; }
    [Option('p', "version", Default = "pre-rel-v0.1.4_rc3", Required = false, HelpText = "Yagna version github tag")]
    public required string PackageVersion { get; set; }
}
