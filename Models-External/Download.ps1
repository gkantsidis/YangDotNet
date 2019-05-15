[CmdletBinding(SupportsShouldProcess=$True)]
param(
    [switch]$Update
)

[PSCustomObject[]]$repositories = @(
    [PSCustomObject]@{ Name = "BroadbandForum"; Link = "https://github.com/BroadbandForum/yang.git" },
    [PSCustomObject]@{ Name = "Juniper";        Link = "https://github.com/Juniper/yang.git" },
    [PSCustomObject]@{ Name = "networkop";      Link = "https://github.com/networkop/yang.git" },
    [PSCustomObject]@{ Name = "OpenROADM";      Link = "https://github.com/OpenROADM/OpenROADM_MSA_Public.git" },
    [PSCustomObject]@{ Name = "openconfig";     Link = "https://github.com/openconfig/public.git" },
    [PSCustomObject]@{ Name = "sonic-object-model-schema"; Link = "https://github.com/Azure/sonic-object-model-schema.git"},
    [PSCustomObject]@{ Name = "sysrepo";        Link = "https://github.com/sysrepo/sysrepo.git" },
    [PSCustomObject]@{ Name = "tail-f-systems"; Link = "https://github.com/tail-f-systems/JNC.git" },
    [PSCustomObject]@{ Name = "YangModels";     Link = "https://github.com/YangModels/yang.git" }
)

Push-Location -Path $PSScriptRoot

foreach($repo in $repositories) {
    $name = $repo.Name

    Write-Verbose -Message "Checking $name"

    if (-not (Test-Path -Path $name -PathType Container)) {
        Write-Verbose -Message "Cloning $name"

        if($PSCmdlet.ShouldProcess("$name", "Cloning (git clone)")) {
            git clone --recurse-submodule $repo.Link $name
        }
    } else {
        Write-Verbose -Message "Repo $name is present"

        if ($Update) {
            if ($PSCmdlet.ShouldProcess("$name", "Updating (git pull)")) {
                Push-Location -Path $name
                git pull
                Pop-Location
            }
        }
    }
}

#
# Special treatment
#

# Polatis
$url = "http://www.polatis.com/yang/"
$directory = "Polatis"
$links = (Invoke-WebRequest -Uri $url).Links
$links = $links | Where-Object -Property href -Like -Value "*.yang"
$toDownload = $links | Select-Object -Property @{Label='href'; Expression={@{$true=$_.href;$false=$url+$_.href}[$_.href.StartsWith('http')]}}

if (-not (Test-Path -Path $directory -PathType Container)) {
    New-Item -Name $directory -ItemType Directory -Force
}
$toDownload | ForEach-Object -Process {
    $modelUrl = $_.href
    $name = $modelUrl.Substring($modelUrl.LastIndexOf('/') + 1)
    $fullPath = Join-Path -Path $directory -ChildPath $name
    # We force download here, even if the files exist. It could take some effort to check whether we have updated versions.
    Invoke-WebRequest -Uri $modelUrl -OutFile $fullPath
}

Pop-Location

# Some extra models
if (-not (Test-Path "onos" -PathType Container)) {
    New-Item -Path onos -ItemType Directory
    Push-Location -Path onos
    git init
    git config core.sparseCheckout true
    git remote add -f origin https://github.com/opennetworkinglab/onos.git
    "/models" | Out-File -FilePath ".git/info/sparse-checkout" -Encoding ascii
    git checkout master
    Pop-Location
}