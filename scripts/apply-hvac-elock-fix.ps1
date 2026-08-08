# Tuong thich: goi ban fix all systems.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)
& (Join-Path $PSScriptRoot "apply-elock-all-systems.ps1") -PluginSourceRoot $PluginSourceRoot
