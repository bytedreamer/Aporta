[CmdletBinding()]
param($newVersion)

$xml=New-Object XML
$xml.Load("Directory.Build.props")

$versionNode = $xml.Project.PropertyGroup.Version
if ($null -eq $versionNode)
{
    $versionNode = $xml.CreateElement("Version")
    $xml.Project.PropertyGroup.AppendChild($versionNode)
    Write-Host "Version XML tag added to the csproj"
}

$xml.Project.PropertyGroup.Version = $newVersion
$xml.Save("Directory.Build.props")

$content = [System.IO.File]::ReadAllText("setup/Linux/control-amd64").Replace("{version}",$newVersion)
[System.IO.File]::WriteAllText("setup/Linux/control-amd64", $content)
$content = [System.IO.File]::ReadAllText("setup/Linux/control-armhf").Replace("{version}",$newVersion)
[System.IO.File]::WriteAllText("setup/Linux/control-armhf", $content)
$content = [System.IO.File]::ReadAllText("setup/Linux/control-arm64").Replace("{version}",$newVersion)
[System.IO.File]::WriteAllText("setup/Linux/control-arm64", $content)
