using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO;

#pragma warning disable SYSLIB0057 // .NET 8: load runtime-generated PFX/DER without requiring a newer X509CertificateLoader API.

namespace GrassiBoard.Services.Remote;

internal sealed class RemoteTlsMaterial : IDisposable
{
    public RemoteTlsMaterial(X509Certificate2 serverCertificate, byte[] rootCertificateDer, string rootThumbprint)
    {
        ServerCertificate = serverCertificate;
        RootCertificateDer = rootCertificateDer;
        RootThumbprint = rootThumbprint;
    }

    public X509Certificate2 ServerCertificate { get; }
    public byte[] RootCertificateDer { get; }
    public string RootThumbprint { get; }

    public void Dispose() => ServerCertificate.Dispose();
}

internal sealed class RemoteTlsService : IDisposable
{
    public const string StableHostName = "grassimote.local";
    private const string RootPfxFileName = "grassimote-root-ca.pfx";
    private const string ServerPfxFileName = "grassimote-server.pfx";
    private const string AddressFileName = "grassimote-server-address.txt";
    private readonly string _directory;
    private RemoteTlsMaterial? _current;

    public RemoteTlsService(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GrassiBoard",
            "remote-tls");
    }

    public RemoteTlsMaterial GetOrCreate(IPAddress address)
    {
        _current?.Dispose();
        _current = null;
        Directory.CreateDirectory(_directory);

        string rootPath = Path.Combine(_directory, RootPfxFileName);
        string serverPath = Path.Combine(_directory, ServerPfxFileName);
        string addressPath = Path.Combine(_directory, AddressFileName);

        using X509Certificate2 root = LoadOrCreateRoot(rootPath);
        X509Certificate2 server = LoadOrCreateServer(root, serverPath, addressPath, address);
        byte[] rootDer = root.Export(X509ContentType.Cert);
        _current = new RemoteTlsMaterial(server, rootDer, root.Thumbprint ?? string.Empty);
        return _current;
    }

    private static X509Certificate2 LoadOrCreateRoot(string rootPath)
    {
        if (File.Exists(rootPath))
        {
            try
            {
                var existing = new X509Certificate2(
                    rootPath,
                    (string?)null,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
                if (existing.HasPrivateKey && existing.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddYears(1))
                    return existing;
                existing.Dispose();
            }
            catch (CryptographicException)
            {
                // Recreate corrupt or incompatible local TLS material below.
            }
        }

        using RSA key = RSA.Create(3072);
        var request = new CertificateRequest(
            "CN=GrassiMote Local CA, O=GrassiBoard",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset notAfter = notBefore.AddYears(10);
        using X509Certificate2 created = request.CreateSelfSigned(notBefore, notAfter);
        byte[] pfx = created.Export(X509ContentType.Pfx);
        File.WriteAllBytes(rootPath, pfx);
        return new X509Certificate2(
            pfx,
            (string?)null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }

    private static X509Certificate2 LoadOrCreateServer(
        X509Certificate2 root,
        string serverPath,
        string addressPath,
        IPAddress address)
    {
        string expectedAddress = address.ToString();
        if (File.Exists(serverPath) && File.Exists(addressPath) &&
            string.Equals(File.ReadAllText(addressPath).Trim(), expectedAddress, StringComparison.Ordinal))
        {
            try
            {
                var existing = new X509Certificate2(
                    serverPath,
                    (string?)null,
                    ServerKeyStorageFlags);
                if (existing.HasPrivateKey && existing.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddDays(30))
                    return existing;
                existing.Dispose();
            }
            catch (CryptographicException)
            {
                // Recreate below while keeping the already-trusted local root.
            }
        }

        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={StableHostName}, O=GrassiBoard",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") },
            true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(StableHostName);
        san.AddIpAddress(address);
        request.CertificateExtensions.Add(san.Build());

        byte[] serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;
        serial[^1] |= 0x01; // Keep the serial positive and non-zero.
        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddHours(-1);
        DateTimeOffset notAfter = notBefore.AddDays(365);
        using X509Certificate2 signed = request.Create(root, notBefore, notAfter, serial);
        using X509Certificate2 withKey = signed.CopyWithPrivateKey(key);
        byte[] pfx = withKey.Export(X509ContentType.Pfx);
        File.WriteAllBytes(serverPath, pfx);
        File.WriteAllText(addressPath, expectedAddress);
        return new X509Certificate2(
            pfx,
            (string?)null,
            ServerKeyStorageFlags);
    }

    // Windows SChannel/SslStream cannot reliably use an ephemeral private key for a
    // TLS server certificate. Persist the generated leaf key in the current user's
    // key store so Kestrel can complete HTTPS handshakes without requiring admin.
    private const X509KeyStorageFlags ServerKeyStorageFlags =
        X509KeyStorageFlags.Exportable |
        X509KeyStorageFlags.UserKeySet |
        X509KeyStorageFlags.PersistKeySet;

    public void Dispose()
    {
        _current?.Dispose();
        _current = null;
    }
}

#pragma warning restore SYSLIB0057
