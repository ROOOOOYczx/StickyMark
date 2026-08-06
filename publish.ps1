$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$tempRoot = Join-Path $projectRoot "..\..\work\dotnet-temp"
$packageRoot = Join-Path $projectRoot "..\..\work\nuget-cache"
$releaseRoot = Join-Path $projectRoot "release-webview2"
New-Item -ItemType Directory -Force $tempRoot, $packageRoot, $releaseRoot | Out-Null
$env:TEMP = $tempRoot
$env:TMP = $tempRoot

dotnet restore (Join-Path $projectRoot "StickyMark.Native.csproj") --source https://api.nuget.org/v3/index.json --packages $packageRoot -r win-x64
dotnet publish (Join-Path $projectRoot "StickyMark.Native.csproj") --configuration Release --runtime win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $releaseRoot
Write-Host "已生成：$releaseRoot\StickyMark.exe"
