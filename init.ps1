[CmdletBinding()]
param()

$Env:ROOTDIR = $PSScriptRoot

# Make sure that paket is available
$paketDir = Join-Path -Path $PSScriptRoot -ChildPath ".paket"
$paket = Join-Path -Path $paketDir -ChildPath "paket.exe"

if (-not (Test-Path -Path $paket)) {
    Write-Warning -Message "Downloading paket.exe"
    & "$PSScriptRoot\.paket\paket.bootstrapper.exe"
}

$paketCommand = Get-Command -Name paket.exe -ErrorAction SilentlyContinue
if ($null -eq $paketCommand) {
    $Env:PATH += ";$paketDir"
}
elseif (-not ($paketCommand.Source.Equals($paket, [StringComparison]::InvariantCultureIgnoreCase))) {
    $Env:PATH = "$paketDir;" + $Env:PATH
}

# Make sure that npm is available; this is used to put some structure in the commit messages
if (Get-Command -Name npm -ErrorAction SilentlyContinue) {
    $commitizen_path = Join-Path -Path $PSScriptRoot -ChildPath node_modules | `
                       Join-Path -ChildPath commitizen

    if (-not (Test-Path -Path $commitizen_path)) {
        Write-Verbose -Message "Commitizen does not exist; will install"
        npm install commitizen
    }

    if (Test-Path -Path $commitizen_path) {
        $commitizen_bin = Join-Path -Path $commitizen_path -ChildPath bin
        if (-not $Env:PATH.Contains($commitizen_bin)) {
            Write-Verbose -Message "Adding commitizen to path"
            $Env:PATH += ";$commitizen_bin"
        }
    }

    if (Get-Command -Name commitizen -ErrorAction SilentlyContinue) {
        Write-Verbose -Message "Commitizen seems to be installed"
        # Not sure whether we need to run the following. I think it is needed once per repo:
        # commitizen init cz-conventional-changelog --save-dev --save-exact
    }

} else {
    Write-Warning -Message "node.js is not installed; this is not a problem, but when present gives better options for structuring git commit messages"
}

#
# Get external packages
#
$paket_dir = Join-Path -Path $PSScriptRoot -ChildPath ".paket"
& "$paket_dir\paket.exe" restore

# Generate load scripts, but only if the packages have been updated.
$load_scripts = Join-Path -Path $paket_dir -ChildPath "load"
$load_script_info = Join-Path -Path $paket_dir -ChildPath ".load.checksum"

$generate_scripts = $false
$packages_lock_file = Join-Path -Path $PSScriptRoot -ChildPath "paket.lock"
if (-not ((Test-Path -Path $load_scripts) -and (Test-Path -Path $load_script_info))) {
    Write-Verbose -Message "Loading scripts need to be regenerated"
    $generate_scripts = $true
} else {
    $current_hash = Get-FileHash $packages_lock_file
    $file_hash = Get-Content $load_script_info
    if (-not $current_hash.Hash.ToString().Equals($file_hash)) {
        Write-Verbose -Message "Load script hashes do not agree; will regenerate scripts"
        $generate_scripts = $true
    }
}

if ($generate_scripts) {
    Write-Warning -Message "Regenerating loading scripts"

    & "$paket_dir\paket.exe" generate-load-scripts -t fsx
    $dependencies_hash = Get-FileHash $packages_lock_file
    $dependencies_hash.Hash | Out-File -Encoding ascii -FilePath $load_script_info
}

$fakePath = Join-Path -Path $PSScriptRoot -ChildPath "tools" | Join-Path -ChildPath "fake"
$fakeExecutable = Join-Path -Path $fakePath -ChildPath "fake.exe"
if (-not (Test-Path -Path $fakeExecutable)) {
    dotnet tool install fake-cli --tool-path $fakePath
}

$Env:FakePath = $fakePath
$Env:FakeExecutable = $fakeExecutable

#
# Helper cmdlets
#
function global:Build {
    Push-Location -Path $PSScriptRoot
    .\build.bat $args
    Pop-Location
}

function global:qb {
    Push-Location -Path $PSScriptRoot
    . "$Env:FakeExecutable" run build.fsx -t QuickBuild
    Pop-Location
}

Import-Module $PSScriptRoot\src\YangDevHelper

$userPath = Join-Path -Path $PSScriptRoot -ChildPath users | Join-Path -ChildPath $Env:USERNAME
$userInit = Join-Path -Path $userPath -ChildPath "init.ps1"
$userLocal = Join-Path -Path $userPath -ChildPath "init-local.ps1"
if (Test-Path -Path $userPath -PathType Leaf) {
    Write-Verbose -Message "Running user's profile from $userPath"
    . $userInit
}
if (Test-Path -Path $userLocal -PathType Leaf) {
    Write-Verbose -Message "Running user's local profile from $userPath"
    . $userLocal
}