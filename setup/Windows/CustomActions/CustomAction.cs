using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;

namespace CustomActions
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult CheckDotNetCore31Installed(Session session)
        {
            session.Log("Begin CheckDotNetCore31Installed");

            var localMachine64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            var registryKey =
                localMachine64.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost\", false);

            var version = (string) registryKey?.GetValue("Version") ?? string.Empty;

            session.Log($"Found version {version} of .NET Core");

            session["DOTNETCORE31"] = version.StartsWith("3.1") ? "1" : "0";

            return ActionResult.Success;
        }
    }
}
