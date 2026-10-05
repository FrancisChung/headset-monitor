[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$')]
    [string] $AppVersion = '0.1.0',
    [string] $HeadsetControlDirectory,
    [string] $InnoCompiler
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($HeadsetControlDirectory)) {
    $HeadsetControlDirectory = Join-Path $repositoryRoot 'packaging\headsetcontrol'
}

$headsetControlExecutable = Join-Path $HeadsetControlDirectory 'headsetcontrol.exe'
if (-not (Test-Path -LiteralPath $headsetControlExecutable -PathType Leaf)) {
    throw "Required packaging input is missing: $headsetControlExecutable"
}

$publishDirectory = Join-Path $repositoryRoot 'artifacts\publish\win-x64'
$installerOutput = Join-Path $repositoryRoot 'artifacts\installer'
$project = Join-Path $repositoryRoot 'src\HeadsetMonitor\HeadsetMonitor.csproj'

dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    -p:PublishProfile=win-x64 `
    -p:Version=$AppVersion `
    -m:1
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$publishedExe = Join-Path $publishDirectory 'HeadsetMonitor.exe'
if (-not (Test-Path -LiteralPath $publishedExe -PathType Leaf)) {
    throw "The expected published executable was not produced: $publishedExe"
}

if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        $InnoCompiler = $command.Source
    }
    else {
        $candidateCompilers = @(
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
        )
        foreach ($candidateCompiler in $candidateCompilers) {
            if (Test-Path -LiteralPath $candidateCompiler -PathType Leaf) {
                $InnoCompiler = $candidateCompiler
                break
            }
        }
    }
}
if ([string]::IsNullOrWhiteSpace($InnoCompiler) -or -not (Test-Path -LiteralPath $InnoCompiler -PathType Leaf)) {
    throw 'Inno Setup compiler (ISCC.exe) was not found. Install Inno Setup 6 or 7, add ISCC.exe to PATH, or pass -InnoCompiler.'
}

New-Item -ItemType Directory -Force -Path $installerOutput | Out-Null
$script = Join-Path $PSScriptRoot 'HeadsetMonitor.iss'
& $InnoCompiler `
    "/DSourceDir=$publishDirectory" `
    "/DHeadsetControlDir=$HeadsetControlDirectory" `
    "/DAppVersion=$AppVersion" `
    "/DOutputDir=$installerOutput" `
    $script
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Installer created in $installerOutput"
