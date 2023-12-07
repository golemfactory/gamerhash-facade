# Facade test GUI

## Preparing directories

You can use `Golem.Package` builder tool to create directory structure for MockGUI app.
Run followin command from top level project directory:
```sh
dotnet run --project Golem.Package -- --target modules
```

You can choose specific versions of `yagna` and `runtimes` to be downloaded:
```sh
dotnet run --project Golem.Package -- --target modules --yagna-version v0.13.2 --runtime-version pre-rel-v0.1.0-rc16
```


## Running

```sh
dotnet run --golem modules
```

Application requires following directory structure:

```
gamerhash-facade/
└── modules
    ├── golem
    │   ├── yagna.exe
    │   └── ya-provider.exe
    ├── golem-data
    └── plugins
        ├── dummy.exe
        ├── ya-dummy-ai.json
        └── ya-runtime-ai.exe
```

## Running from dynamic dlls

Instead of using dlls referenced by the project, they can be downloaded from `modules` directory:
```sh
dotnet run --golem modules --use-dll
```

This example assumes that `modules/golem` contains `Golem.dll` and `GolemLib.dll`.
