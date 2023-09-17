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

$content = [System.IO.File]::ReadAllText("./setup/Linux/CONTENT-amd64").Replace("{version}",$newVersion)
[System.IO.File]::WriteAllText("./setup/Linux/CONTENT-amd64", $content)
$content = [System.IO.File]::ReadAllText("./setup/Linux/CONTENT-armhf").Replace("{version}",$newVersion)
[System.IO.File]::WriteAllText("./setup/Linux/CONTENT-armhf", $content)
$content = [System.IO.File]::ReadAllText("./setup/Linux/CONTENT-arm64").Replace("{version}",$newVersion)
[System.IO.File]::WriteAllText("./setup/Linux/CONTENT-arm64", $content)

