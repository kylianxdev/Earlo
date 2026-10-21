# Installs the PodPilot AAP bridge driver (test-signed build).
# Run from the unzipped driver folder in an elevated PowerShell:
#   powershell -ExecutionPolicy Bypass -File install-driver.ps1
#
# Test-signed drivers only load when Windows test signing is on, or for one boot after choosing
# "Disable driver signature enforcement" (F7) in Startup Settings. Pass -Force in that case.
# This script does not change any boot or security setting for you.
#Requires -RunAsAdministrator
param([switch] $Force)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

$inf = Join-Path $here 'PodPilotAap.inf'
$cer = Get-ChildItem $here -Filter '*.cer' | Select-Object -First 1
if (-not (Test-Path $inf)) { throw "PodPilotAap.inf not found next to this script." }

$testSigning = (bcdedit /enum '{current}' | Select-String 'testsigning\s+Yes') -ne $null
if (-not $testSigning -and -not $Force) {
    Write-Warning "Windows test signing is OFF, so this test-signed driver will not load."
    Write-Host ""
    Write-Host "Option A, one boot only (works with Secure Boot on):"
    Write-Host "  Shift + Restart > Troubleshoot > Advanced options > Startup Settings > Restart > press F7,"
    Write-Host "  then run:  install-driver.ps1 -Force"
    Write-Host ""
    Write-Host "Option B, permanent (needs Secure Boot off in your UEFI settings; breaks some anti-cheat):"
    Write-Host "  bcdedit /set testsigning on, restart, run this script again. Undo: bcdedit /set testsigning off"
    exit 1
}

if ($cer) {
    Write-Host "Trusting test certificate $($cer.Name)..."
    Import-Certificate -FilePath $cer.FullName -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
    Import-Certificate -FilePath $cer.FullName -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null
}

Write-Host "Installing driver..."
pnputil /add-driver $inf /install
if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 3010) { throw "pnputil failed with exit code $LASTEXITCODE" }

Write-Host ""
Write-Host "Done. Make sure your AirPods are paired with Windows, open the case, then start PodPilot."
Write-Host "If nothing happens, check Device Manager > Bluetooth for 'PodPilot AAP Bridge'."
