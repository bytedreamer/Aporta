using System.Linq;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;

namespace CustomActions
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult CheckDotNetInstalled(Session session)
        {
            session.Log("Begin CheckDotNetInstalled");

            var localMachine64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            var registryKey =
                localMachine64.OpenSubKey(@"SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App\", false);

            var latestVersion = registryKey?.GetValueNames().OrderByDescending(version => version).First() ?? string.Empty;

            session.Log($"Found version {latestVersion} of .NET Core");

            session["DOTNETCORE6"] = latestVersion.StartsWith("6") ? "1" : "0";

            return ActionResult.Success;
        }
    }
}
