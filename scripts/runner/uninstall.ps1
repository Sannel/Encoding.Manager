#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls the Encoding Manager Runner Windows Service.
.DESCRIPTION
    Stops and removes the Windows Service, then deletes the install directory
    after user confirmation.
#>
[CmdletBinding()]
param(
    [string]$InstallPath
)

$ServiceName = "sannel-encoding-runner"
$DefaultPath = Join-Path $env:ProgramFiles "Encoding Manager\Runner"

if (-not $InstallPath) {
    $InstallPath = Read-Host "Install directory to remove [$DefaultPath]"
    if ([string]::IsNullOrWhiteSpace($InstallPath)) {
        $InstallPath = $DefaultPath
    }
}

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    if ($existingService.Status -eq 'Running') {
        Write-Host "Stopping service..."
        Stop-Service -Name $ServiceName -Force
        $existingService.WaitForStatus('Stopped', '00:01:00')
    }

    Write-Host "Removing service..."
    sc.exe delete $ServiceName | Out-Null
    Write-Host "Service removed."
}
else {
    Write-Host "Service '$ServiceName' not found — skipping service removal."
}

if (Test-Path $InstallPath) {
    $confirm = Read-Host "Delete install directory '$InstallPath'? [y/N]"
    if ($confirm -eq 'y' -or $confirm -eq 'Y') {
        Remove-Item -Path $InstallPath -Recurse -Force
        Write-Host "Directory removed."
    }
    else {
        Write-Host "Directory kept."
    }
}
else {
    Write-Host "Install directory '$InstallPath' not found — nothing to remove."
}

Write-Host "Uninstall complete."
