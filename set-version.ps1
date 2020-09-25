[CmdletBinding()]
param($newVersion)

$xml=New-Object XML
$xml.Load("Directory.Build.props")

if ($xml.Project.PropertyGroup.Properties.name -match "Version")
{
    $xml.Project.PropertyGroup.Version = $newVersion
    $xml.Save("Directory.Build.props")
}
