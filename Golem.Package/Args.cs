using CommandLine;

public class Args
{
    [Option('t', "target", Default = "package", Required = false, HelpText = "Directory where binaries will be generated")]
    public required string Target { get; set; }
    [Option('y', "yagna-version", Default = "v0.13.2", Required = false, HelpText = "Yagna version github tag")]
    public required string GolemVersion { get; set; }
    [Option('r', "runtime-version", Default = "pre-rel-v0.1.0-rc16", Required = false, HelpText = "Runtime version github tag")]
    public required string RuntimeVersion { get; set; }
}