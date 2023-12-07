using CommandLine;

using Golem.Tools;


await Parser.Default.ParseArguments<BuildArgs, DownloadArgs>(args)
    .MapResult(
        (BuildArgs options) => Build(options),
        (DownloadArgs options) => Download(options),
        errors => Task.FromResult(1)
    );


static async Task Download(DownloadArgs args)
{
    Console.WriteLine("Running Golem AI package downloader!");

    Console.WriteLine("Versions:");
    Console.WriteLine("AI Facade: " + args.PackageVersion);

    var current = Directory.GetCurrentDirectory();
    var target = Path.Combine(current, args.Target);

    Directory.CreateDirectory(target);
    await PackageBuilder.DownloadExtractPackage(target, "gamerhash-ai-facade", "golemfactory/gamerhash-facade", args.PackageVersion);
}

static async Task Build(BuildArgs args)
{
    Console.WriteLine("Running Golem AI package builder!");

    PackageBuilder.CURRENT_GOLEM_VERSION = args.GolemVersion;
    PackageBuilder.CURRENT_RUNTIME_VERSION = args.RuntimeVersion;

    Console.WriteLine("Versions:");
    Console.WriteLine("Yagna: " + PackageBuilder.CURRENT_GOLEM_VERSION);
    Console.WriteLine("AI Runtime: " + PackageBuilder.CURRENT_RUNTIME_VERSION);

    var current = Directory.GetCurrentDirectory();
    var root = await PackageBuilder.BuildTestDirectory("pack");
    var bins = PackageBuilder.BinariesDir(root);
    var build_dir = AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? Path.GetTempPath();
    var package_dir = Path.Combine(current, args.Target);

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
}

