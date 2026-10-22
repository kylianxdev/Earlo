# Installs the Microsoft Store build from a folder, without signing, to test what only happens in the
# packaged version: the startup task, starting hidden at sign-in, and no updater or driver button.
#
# Needs Developer Mode (Settings > System > For developers). No administrator rights needed.
#   powershell -File tools/test-msix-local.ps1
#   powershell -File tools/test-msix-local.ps1 -Remove
param(
    [string] $Publish = 'publish/store-x64',
    [switch] $Remove
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$identity = Get-Content (Join-Path $root 'store/identity.json') -Raw | ConvertFrom-Json

Get-AppxPackage -Name $identity.IdentityName | Remove-AppxPackage
if ($Remove) {
    Write-Host 'Removed the test package.'
    return
}

$publishPath = Join-Path $root $Publish
if (-not (Test-Path (Join-Path $publishPath 'Earlo.exe'))) {
    dotnet publish (Join-Path $root 'src/Earlo.App') -c Release -r win-x64 --self-contained -p:EarloFlavor=store -o $publishPath
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
}

$stage = Join-Path $root 'publish/msix-local'
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item (Join-Path $publishPath '*') $stage -Recurse
$assets = New-Item -ItemType Directory -Force (Join-Path $stage 'Assets')
Copy-Item (Join-Path $root 'store/Assets/*') $assets
# Without a resource index (makepri) Windows needs the plain file names the manifest uses.
foreach ($name in 'Square150x150Logo', 'Square44x44Logo', 'StoreLogo', 'Wide310x150Logo') {
    Copy-Item (Join-Path $assets "$name.scale-100.png") (Join-Path $assets "$name.png")
}

(Get-Content (Join-Path $root 'store/AppxManifest.xml') -Raw) `
    -replace '\$IdentityName\$', $identity.IdentityName `
    -replace '\$Publisher\$', $identity.Publisher `
    -replace '\$PublisherDisplayName\$', $identity.PublisherDisplayName `
    -replace '\$Version\$', '0.0.1.0' `
    -replace '\$Architecture\$', 'x64' |
    Set-Content (Join-Path $stage 'AppxManifest.xml') -Encoding utf8

Add-AppxPackage -Register (Join-Path $stage 'AppxManifest.xml')
Write-Host 'Installed. Start Earlo from the Start menu, then sign out and in to test the startup task.'
Write-Host 'Remove it again with: powershell -File tools/test-msix-local.ps1 -Remove'
