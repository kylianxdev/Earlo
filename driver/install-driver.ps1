# Installs the PodPilot AAP bridge driver (test-signed build).
# Run from the unzipped driver folder in an elevated PowerShell:
#   powershell -ExecutionPolicy Bypass -File install-driver.ps1
#
# Test-signed drivers only load when Windows test signing is on. This script does not change
# that setting for you; see the instructions it prints.
#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

$inf = Join-Path $here 'PodPilotAap.inf'
$cer = Get-ChildItem $here -Filter '*.cer' | Select-Object -First 1
if (-not (Test-Path $inf)) { throw "PodPilotAap.inf not found next to this script." }

$testSigning = (bcdedit /enum '{current}' | Select-String 'testsigning\s+Yes') -ne $null
if (-not $testSigning) {
    Write-Warning "Windows test signing is OFF, so this test-signed driver will not load."
    Write-Host ""
    Write-Host "To enable it (this lowers security and can conflict with Secure Boot and game anti-cheat):"
    Write-Host "  1. Run as administrator:  bcdedit /set testsigning on"
    Write-Host "  2. Restart Windows"
    Write-Host "  3. Run this script again"
    Write-Host ""
    Write-Host "Undo later with:  bcdedit /set testsigning off"
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
