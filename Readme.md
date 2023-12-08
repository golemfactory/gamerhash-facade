# Facade usage instruction

## Quick start

Inside `gamerhash-facade` project directory run in command line:
```
dotnet run --project Golem.Package -- download --target modules
dotnet run --project MockGUI --golem modules --use-dll
dotnet run --project ExampleRunner --golem modules
```

In `MockGUI` click Start button to run Golem and wait for tasks.

## Application flow description

First we need to create directory structure for `MockGUI`. Detailed description of possible options can be found [here](MockGUI/readme.md#preparing-directories).

`Golem.Package` project is able to download all necessary binaries and create directory structure for Golem. Result of running `dotnet run --project Golem.Package -- download --target modules` will look like this:
```
```




## Troubleshooting
