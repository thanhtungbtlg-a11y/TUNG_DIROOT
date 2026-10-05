[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & $script:dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$dotnet = $env:TUNG_DIROOT_DOTNET
if ([string]::IsNullOrWhiteSpace($dotnet)) {
    $dotnetCommand = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue
    if ($null -ne $dotnetCommand) {
        $dotnet = $dotnetCommand.Source
    }
}

if ([string]::IsNullOrWhiteSpace($dotnet)) {
    throw "No usable .NET SDK was found. Set TUNG_DIROOT_DOTNET to a dotnet executable or add a .NET SDK to PATH."
}

try {
    $sdkList = & $dotnet --list-sdks 2>&1
    $sdkExitCode = $LASTEXITCODE
}
catch {
    throw "Unable to run '$dotnet'. Set TUNG_DIROOT_DOTNET to a usable dotnet executable or add a .NET SDK to PATH."
}

if ($sdkExitCode -ne 0 -or [string]::IsNullOrWhiteSpace(($sdkList -join [Environment]::NewLine))) {
    throw "'$dotnet' does not provide a usable .NET SDK. Set TUNG_DIROOT_DOTNET to a dotnet executable with an SDK installed."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "TUNG_DIROOT.sln"

Invoke-DotNet @("restore", $solutionPath)
Invoke-DotNet @("build", $solutionPath, "--configuration", "Release", "--no-restore")
Invoke-DotNet @("test", $solutionPath, "--configuration", "Release", "--no-build")
