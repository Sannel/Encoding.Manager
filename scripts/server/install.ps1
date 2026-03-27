#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs or updates the Encoding Manager Server as a Windows Service.
.DESCRIPTION
    Copies the application files to the install directory and registers
    a Windows Service. If the service already exists, it is stopped,
    updated in place, and restarted.
#>
[CmdletBinding()]
param(
    [string]$InstallPath
)

$ServiceName = "sannel-encoding-server"
$ServiceDisplayName = "Sannel Encoding Manager Server"
$DefaultPath = Join-Path $env:ProgramFiles "Encoding Manager\Server"
$ExeName = "Sannel.Encoding.Manager.Web.exe"

if (-not $InstallPath) {
    $InstallPath = Read-Host "Install directory [$DefaultPath]"
    if ([string]::IsNullOrWhiteSpace($InstallPath)) {
        $InstallPath = $DefaultPath
    }
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$SourceFiles = Join-Path $ScriptDir "*"
$ExePath = Join-Path $InstallPath $ExeName

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Host "Existing service detected — updating..."
    if ($existingService.Status -eq 'Running') {
        Write-Host "Stopping service..."
        Stop-Service -Name $ServiceName -Force
        $existingService.WaitForStatus('Stopped', '00:01:00')
    }

    Write-Host "Copying files to $InstallPath..."
    Copy-Item -Path $SourceFiles -Destination $InstallPath -Recurse -Force

    Write-Host "Starting service..."
    Start-Service -Name $ServiceName
    Write-Host "Service updated and started successfully."
}
else {
    Write-Host "Installing Encoding Manager Server..."

    if (-not (Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    }

    Write-Host "Copying files to $InstallPath..."
    Copy-Item -Path $SourceFiles -Destination $InstallPath -Recurse -Force

    Write-Host "Registering Windows Service..."
    New-Service -Name $ServiceName `
        -BinaryPathName $ExePath `
        -DisplayName $ServiceDisplayName `
        -StartupType Automatic `
        -Description "Sannel Encoding Manager Server — web UI and API for encoding workflow management."

    Write-Host "Starting service..."
    Start-Service -Name $ServiceName
    Write-Host "Installation complete. Service is running."
}

Write-Host ""
Write-Host "Install path : $InstallPath"
Write-Host "Service name : $ServiceName"
Write-Host "Executable   : $ExePath"
