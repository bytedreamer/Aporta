using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Aporta.Utilities;

/// <summary>
/// 
/// </summary>
public static class SslCertificateUtilities
{
    private const string CertificateFileName = "Aporta.pfx";

    /// <summary>
    /// Checks that there is a valid certificate
    /// </summary>
    /// <param name="password"></param>
    /// <returns></returns>
    public static bool IsThereAValidCertificate(string password)
    {
        if (!File.Exists(BuildCertificateFileName()))
        {
            return false;
        }

        try
        {
            var certificate =
                new X509Certificate2(BuildCertificateFileName(), password);

            return certificate.HasPrivateKey;
        }
        catch
        {
            return false;
        }
    }

    public static void CreateAndSaveSelfSignedServerCertificate(string password)
    {
        SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName(Environment.MachineName);

        var distinguishedName = new X500DistinguishedName("CN=Aporta");

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment |
                X509KeyUsageFlags.DigitalSignature, false));


        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection {new Oid("1.3.6.1.5.5.7.3.1")}, false));

        request.CertificateExtensions.Add(sanBuilder.Build());

        var certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)),
            new DateTimeOffset(DateTime.UtcNow.AddDays(3650)));

        File.WriteAllBytes(BuildCertificateFileName(), certificate.Export(X509ContentType.Pfx, password));
    }

    public static X509Certificate2 LoadCertificate(string password)
    {
        return new X509Certificate2(BuildCertificateFileName(), password, X509KeyStorageFlags.MachineKeySet);
    }

    private static string BuildCertificateFileName()
    {
        return Path.Combine(
            Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? Environment.CurrentDirectory,
            CertificateFileName);
    }
}