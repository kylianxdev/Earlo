# Builds the Microsoft Store package (an .msixbundle with x64 and arm64) from Store publish folders.
#
#   dotnet publish src/PodPilot.App -c Release -r win-x64 --self-contained -p:PodPilotFlavor=store -o publish/store-x64
#   dotnet publish src/PodPilot.App -c Release -r win-arm64 --self-contained -p:PodPilotFlavor=store -o publish/store-arm64
#   powershell -File tools/make-msix.ps1 -Version 0.3.0
#
# The bundle is unsigned: the Store signs it after upload. Needs MakeAppx/MakePri from the Windows SDK.
param(
    [Parameter(Mandatory)] [string] $Version,
    [string] $PublishRoot = 'publish',
    [string] $Output = 'PodPilot-store.msixbundle'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Find-SdkTool([string]$name) {
    $tool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\$name" -ErrorAction SilentlyContinue |
        Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1
    if (-not $tool) { throw "$name not found; install the Windows SDK." }
    return $tool.FullName
}

$makeAppx = Find-SdkTool 'makeappx.exe'
$makePri = Find-SdkTool 'makepri.exe'
$identity = Get-Content (Join-Path $root 'store/identity.json') -Raw | ConvertFrom-Json
$msixVersion = "$Version.0"   # the Store requires four parts with the last one 0

$work = Join-Path $PublishRoot 'msix'
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
$packages = New-Item -ItemType Directory -Force (Join-Path $work 'packages')

foreach ($arch in 'x64', 'arm64') {
    $publish = Join-Path $PublishRoot "store-$arch"
    if (-not (Test-Path (Join-Path $publish 'PodPilot.exe'))) { throw "Missing $publish\PodPilot.exe" }

    $stage = New-Item -ItemType Directory -Force (Join-Path $work "stage-$arch")
    Copy-Item (Join-Path $publish '*') $stage -Recurse
    Copy-Item (Join-Path $root 'store/Assets') (Join-Path $stage 'Assets') -Recurse

    (Get-Content (Join-Path $root 'store/AppxManifest.xml') -Raw) `
        -replace '\$IdentityName\$', $identity.IdentityName `
        -replace '\$Publisher\$', $identity.Publisher `
        -replace '\$PublisherDisplayName\$', $identity.PublisherDisplayName `
        -replace '\$Version\$', $msixVersion `
        -replace '\$Architecture\$', $arch |
        Set-Content (Join-Path $stage 'AppxManifest.xml') -Encoding utf8

    # Resource index so Windows picks the right logo scale.
    $priConfig = Join-Path $work "priconfig-$arch.xml"
    & $makePri createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o | Out-Null
    & $makePri new /pr $stage /cf $priConfig /mn (Join-Path $stage 'AppxManifest.xml') /of (Join-Path $stage 'resources.pri') /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makepri failed for $arch" }

    & $makeAppx pack /d $stage /p (Join-Path $packages "PodPilot-$arch.msix") /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed for $arch" }
}

& $makeAppx bundle /d $packages /p $Output /bv $msixVersion /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makeappx bundle failed' }
Write-Host ("Wrote {0} ({1:N1} MB)" -f $Output, ((Get-Item $Output).Length / 1MB))
