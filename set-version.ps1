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

$content = [System.IO.File]::ReadAllText("./setup/Linux/content-amd64").Replace("{version}",$newVersion)
[System.IO.File]::WriteAllText("./setup/Linux/content-amd64", $content)
$content = [System.IO.File]::ReadAllText("./setup/Linux/content-armhf").Replace("{version}",$newVersion)
[System.IO.File]::WriteAllText("./setup/Linux/content-armhf", $content)
$content = [System.IO.File]::ReadAllText("./setup/Linux/content-arm64").Replace("{version}",$newVersion)
[System.IO.File]::WriteAllText("./setup/Linux/content-arm64", $content)

