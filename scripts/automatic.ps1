param (
    [string]$automatic_runtime_package_url = "https://modelserve-automatic1111.s3.eu-central-1.amazonaws.com/sd-webui-gh-v0.2.7.zip",
    [string]$automatic_package_dir = "package",
    [bool]$compress = 0,
    [bool]$cleanup = 0
)

# Progress slows down download
$ProgressPreference = 'SilentlyContinue'

$bin_dir = "bin"
if (($cleanup) -and (Test-Path $bin_dir)) {
    Remove-Item -Path $bin_dir -Force -Recurse
}
New-Item -Path $bin_dir -Force -ItemType Directory

$automatic_runtime_package = "$($bin_dir)\automatic_runtime_package.zip"
$automatic_runtime_unpacked = "$($bin_dir)\automatic_unpacked"

if (!(Test-Path $automatic_runtime_package)) {
    Invoke-WebRequest $automatic_runtime_package_url -OutFile $automatic_runtime_package
}

if (!(Test-Path $automatic_runtime_unpacked)) {
    Expand-Archive $automatic_runtime_package -DestinationPath $automatic_runtime_unpacked
}

$plugins_package_dir = "$($automatic_package_dir)\plugins"
$automatic_runtime_dir = "$($plugins_package_dir)\ya-automatic-ai"

if (Test-Path $automatic_runtime_dir) {
    Remove-Item -Path $automatic_runtime_dir -Force -Recurse
}

$automatic_runtime_dir_subdir = "$($automatic_runtime_dir)\automatic"
New-item $automatic_runtime_dir_subdir -ItemType Directory -Force

$automatic_runtime_unpacked_pattern = "$($automatic_runtime_unpacked)\*"
Copy-Item -Path $automatic_runtime_unpacked_pattern -Destination $automatic_runtime_dir_subdir -Recurse
$automatic_runtime_dir_dst = "$($automatic_runtime_dir)\"
Copy-Item "scripts\config.json" -Destination $automatic_runtime_dir_dst


$automatic_runtime_package_descriptor = "$($plugins_package_dir)\ya-automatic-ai.json"
if (Test-Path $automatic_runtime_package_descriptor) {
    Remove-Item -Path $automatic_runtime_package_descriptor -Force -Recurse
}
Copy-Item "scripts\ya-automatic-ai.json" -Destination $automatic_runtime_package_descriptor

if ($compress) {
    $workspace = $(Get-Location)
    $automatic_dist_package = "$($workspace)\$($bin_dir)\dist_package.zip"
    if (Test-Path $automatic_dist_package) {
        Remove-Item $automatic_dist_package -Force -Recurse
    }
    Set-Location $automatic_package_dir
    tar.exe -a -cf $automatic_dist_package .\*
    Set-Location $workspace
}
