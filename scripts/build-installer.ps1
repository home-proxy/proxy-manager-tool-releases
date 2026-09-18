param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [ValidateSet("SelfContained", "FrameworkDependent")]
    [string]$DeploymentMode = "SelfContained"
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "Version must use numeric major.minor.patch or major.minor.patch.build format, for example 1.0.0."
}

$versionParts = $Version.Split('.')
$assemblyVersion = if ($versionParts.Count -eq 3) { "$Version.0" } else { $Version }

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src/NetAgent.ProxyManager.App/NetAgent.ProxyManager.App.csproj"
$appProjectDir = Split-Path -Parent $project
$deploymentModeSuffix = switch ($DeploymentMode) {
    "SelfContained" { "self-contained" }
    "FrameworkDependent" { "framework-dependent" }
}
$outputSuffix = "$RuntimeIdentifier-$deploymentModeSuffix"
$publishDir = Join-Path $root "artifacts/publish/$outputSuffix"
$publishRoot = Join-Path $root "artifacts/publish"
$installerScript = Join-Path $root "installer/HomeProxy.ProxyManager.iss"
$isSelfContained = $DeploymentMode -eq "SelfContained"
$requiresDotNetRuntime = $DeploymentMode -eq "FrameworkDependent"
$isccCommand = Get-Command "iscc" -ErrorAction SilentlyContinue
$iscc = if ($isccCommand) { $isccCommand.Source } else { $null }

if (-not $iscc) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs/Inno Setup 7/ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 7/ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 7/ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs/Inno Setup 6/ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6/ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6/ISCC.exe")
    )

    $iscc = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $iscc) {
    throw "Cannot find ISCC.exe. Install Inno Setup or add ISCC.exe to PATH."
}

$resolvedPublishRoot = [System.IO.Path]::GetFullPath($publishRoot)
$resolvedPublishDir = [System.IO.Path]::GetFullPath($publishDir)
if (-not $resolvedPublishDir.StartsWith($resolvedPublishRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean publish directory outside artifacts/publish: $resolvedPublishDir"
}

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

$selfContainedValue = $isSelfContained.ToString().ToLowerInvariant()
$publishArgs = @(
    "publish", $project,
    "-c", $Configuration,
    "-r", $RuntimeIdentifier,
    "--self-contained", $selfContainedValue,
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:Version=$Version",
    "-p:InformationalVersion=$Version",
    "-p:IncludeSourceRevisionInInformationalVersion=false",
    "-p:FileVersion=$assemblyVersion",
    "-p:AssemblyVersion=$assemblyVersion",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-o", $publishDir
)

if ($isSelfContained) {
    $publishArgs += "-p:EnableCompressionInSingleFile=true"
}

& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$proxifierInstaller = Join-Path $appProjectDir "Installers/ProxifierSetup.exe"
if (Test-Path -LiteralPath $proxifierInstaller) {
    $publishInstallersDir = Join-Path $publishDir "Installers"
    New-Item -ItemType Directory -Path $publishInstallersDir -Force | Out-Null
    Copy-Item -LiteralPath $proxifierInstaller -Destination (Join-Path $publishInstallersDir "ProxifierSetup.exe") -Force
}

$pdbFiles = Get-ChildItem -LiteralPath $publishDir -Recurse -Filter "*.pdb" -File -ErrorAction SilentlyContinue
if ($pdbFiles) {
    $pdbFiles | Remove-Item -Force
}

$requiresDotNetRuntimeValue = $requiresDotNetRuntime.ToString().ToLowerInvariant()

& $iscc `
    "/DAppVersion=$Version" `
    "/DSourceDir=$publishDir" `
    "/DDeploymentMode=$DeploymentMode" `
    "/DRequiresDotNetRuntime=$requiresDotNetRuntimeValue" `
    "/DOutputSuffix=$outputSuffix" `
    $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compiler failed with exit code $LASTEXITCODE."
}
