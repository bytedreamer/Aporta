using System.Linq;
using System.Text;
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

            var versions = registryKey?.GetValueNames().OrderByDescending(version => version).ToArray() ?? new string[]{};

            StringBuilder versionList = new StringBuilder();
            foreach (var version in versions)
            {
                versionList.Append(version + " ");
            }
            session.Log($"Found version {versionList} of .NET Core");

            session["DOTNETCORE6"] = versions.Any(version => version.StartsWith("6")) ? "1" : "0";
            
            session["DOTNETCORE7"] = versions.Any(version => version.StartsWith("7")) ? "1" : "0";

            return ActionResult.Success;
        }
    }
}
