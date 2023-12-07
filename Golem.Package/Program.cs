using CommandLine;

using Golem.Tools;

Console.WriteLine("Running Golem AI package builder!");

var parsed = Parser.Default.ParseArguments<Args>(args).Value;

PackageBuilder.CURRENT_GOLEM_VERSION = parsed.GolemVersion;
PackageBuilder.CURRENT_RUNTIME_VERSION = parsed.RuntimeVersion;

Console.WriteLine("Versions:");
Console.WriteLine("Yagna: " + PackageBuilder.CURRENT_GOLEM_VERSION);
Console.WriteLine("AI Runtime: " + PackageBuilder.CURRENT_RUNTIME_VERSION);

var current = Directory.GetCurrentDirectory();
var root = await PackageBuilder.BuildTestDirectory("pack");
var bins = PackageBuilder.BinariesDir(root);
var build_dir = AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? Path.GetTempPath();
var package_dir = Path.Combine(current, parsed.Target);

File.Copy(Path.Combine(build_dir, "Golem.dll"), Path.Combine(bins, "Golem.dll"));
File.Copy(Path.Combine(build_dir, "GolemLib.dll"), Path.Combine(bins, "GolemLib.dll"));

Directory.Delete(Path.Combine(bins, "plugins"), true);
File.Delete(Path.Combine(bins, "golemsp"));
Directory.Delete(PackageBuilder.DataDir(root), true);

if (Directory.Exists(package_dir))
{
    Directory.Delete(package_dir, true);
}
PackageBuilder.CopyFilesRecursively(Path.Combine(root, "modules"), package_dir);
Directory.Delete(root, true);
