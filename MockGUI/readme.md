# Facade test GUI

## Running

```sh
./modules.sh .exe;
dotnet run --golem modules`
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
        └── ya-runtime-ai.exe
```

## Running from dynamic dlls

This example assumes that `modules/golem` contains Golem.dll.
```sh
./modules.sh .exe;
dotnet run --golem modules --use-dll`
```
