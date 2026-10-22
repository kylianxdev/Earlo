# Removes the Earlo AAP bridge driver. Run in an elevated PowerShell.
#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'

$packages = pnputil /enum-drivers | Out-String
$blocks = $packages -split "(\r?\n){2,}" | Where-Object { $_ -match 'podpilotaap\.inf' }
foreach ($block in $blocks) {
    if ($block -match 'Published Name\s*:\s*(oem\d+\.inf)' -or $block -match ':\s*(oem\d+\.inf)') {
        Write-Host "Removing $($Matches[1])..."
        pnputil /delete-driver $Matches[1] /uninstall /force
    }
}
Write-Host "Done. Turn test signing off again with:  bcdedit /set testsigning off  (then restart)"
