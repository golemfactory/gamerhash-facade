# Facade test GUI

## Preparing directories

There are 2 options:
- Building package locally
- Downloading public released package from github repository

### Downloading artifacts

You can use `Golem.Package` builder tool to create directory structure for MockGUI app.
Run followin command from top level project directory:
```sh
dotnet run --project Golem.Package -- download --target modules
```

### Building locally

You can use `Golem.Package` builder tool to create directory structure for MockGUI app.
Run followin command from top level project directory:
```sh
dotnet run --project Golem.Package -- build --target modules
```

WARNING! Using this command will overwrite previous content of `modules` directory including
golem data directories.

### Selecting versions

You can choose specific release to be downloaded:
```sh
dotnet run --project Golem.Package -- download --target modules --version pre-rel-v0.1.0-rc5
```

In case of building artifacts locally you can specify `yagna` and `runtimes` versions:
```sh
dotnet run --project Golem.Package -- build --target modules --yagna-version v0.14.0 --runtime-version pre-rel-v0.1.0-rc17
```


## Running

```sh
dotnet run  --project MockGUI --golem modules
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
