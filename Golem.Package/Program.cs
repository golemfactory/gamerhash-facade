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

    var build_dir = (args.DllDir != null) 
        ? args.DllDir 
        : AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? Path.GetTempPath();

    var package_dir = Path.Combine(current, args.Target);

    var dll_file_paths = new HashSet<string>();
    foreach (var dll_file_pattern in args.DllFilePatterns.Split(',')) {
        foreach (var dll_file_path in  Directory.GetFiles(build_dir, dll_file_pattern)) {
            dll_file_paths.Add(dll_file_path);
        }
    }

    foreach (var dll_file_path in dll_file_paths)
    {
        var dll_file_name = Path.GetFileName(dll_file_path);
        File.Copy(dll_file_path, Path.Combine(bins, dll_file_name));
    }

    Directory.Delete(Path.Combine(bins, "plugins"), true);
    File.Delete(Path.Combine(bins, "golemsp"));
    Directory.Delete(PackageBuilder.DataDir(root), true);

    if (Directory.Exists(package_dir))
    {
        Directory.Delete(package_dir, true);
    }
    PackageBuilder.CopyFilesRecursively(Path.Combine(root, "modules"), package_dir);

    if (Directory.Exists(root) && !args.DontClean)
    {
        Console.WriteLine($"Removing root dir: {root}");
        try {
            Directory.Delete(root, true);
        } catch (Exception err) {
            Console.WriteLine($"Cannot delete {root}. Err {err}");
        }
    }
}
