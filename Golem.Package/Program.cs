using Golem.Tools;

Console.WriteLine("Running Golem AI package builder!");

PackageBuilder.CURRENT_GOLEM_VERSION = "v0.13.2";
PackageBuilder.CURRENT_RUNTIME_VERSION = "pre-rel-v0.1.0-rc16";

Console.WriteLine("Versions:");
Console.WriteLine("Yagna: " + PackageBuilder.CURRENT_GOLEM_VERSION);
Console.WriteLine("AI Runtime: " + PackageBuilder.CURRENT_RUNTIME_VERSION);

var current = Directory.GetCurrentDirectory();
var root = await PackageBuilder.BuildTestDirectory("pack");
var bins = PackageBuilder.BinariesDir(root);
var build_dir = AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? Path.GetTempPath();
var package_dir = Path.Combine(current, "package");

File.Copy(Path.Combine(build_dir, "Golem.dll"), Path.Combine(bins, "Golem.dll"));
File.Copy(Path.Combine(build_dir, "GolemLib.dll"), Path.Combine(bins, "GolemLib.dll"));
PackageBuilder.CopyFilesRecursively(PackageBuilder.ExeUnitsDir(root), bins);

Directory.Delete(Path.Combine(bins, "plugins"), true);
Directory.Delete(PackageBuilder.DataDir(root), true);
Directory.Delete(PackageBuilder.ExeUnitsDir(root), true);
File.Delete(Path.Combine(bins, "golemsp"));

if (Directory.Exists(package_dir))
{
    Directory.Delete(package_dir, true);
}
Directory.Move(bins, package_dir);
