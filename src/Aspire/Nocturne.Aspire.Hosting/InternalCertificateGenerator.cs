using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Nocturne.Aspire.Hosting;

/// <summary>
/// Generates mTLS certificates for secure internal communication between
/// the Nocturne API and connectors.
/// </summary>
/// <remarks>
/// Certificate hierarchy:
/// - Root CA (self-signed, long-lived)
///   - Server certificate (for API internal endpoint)
///   - Client certificates (one per connector, CN = connector name)
///
/// Certificates are stored in %APPDATA%/Nocturne/certificates/ on Windows
/// or ~/.nocturne/certificates/ on Unix.
/// </remarks>
public static class InternalCertificateGenerator
{
    private static readonly string CertificateDirectory = GetCertificateDirectory();

    /// <summary>
    /// Gets the path to the CA certificate.
    /// </summary>
    public static string CaCertificatePath => Path.Combine(CertificateDirectory, "ca.crt");

    /// <summary>
    /// Gets the path to the CA private key.
    /// </summary>
    public static string CaKeyPath => Path.Combine(CertificateDirectory, "ca.key");

    /// <summary>
    /// Gets the path to the server certificate (PFX).
    /// </summary>
    public static string ServerCertificatePath => Path.Combine(CertificateDirectory, "server.pfx");

    /// <summary>
    /// Gets the path to a connector's client certificate (PFX).
    /// </summary>
    public static string GetConnectorCertificatePath(string connectorName) =>
        Path.Combine(CertificateDirectory, $"connector-{connectorName.ToLowerInvariant()}.pfx");

    /// <summary>
    /// Gets the certificate directory path based on the platform.
    /// </summary>
    private static string GetCertificateDirectory()
    {
        string basePath;
        if (OperatingSystem.IsWindows())
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            basePath = Path.Combine(basePath, ".nocturne");
        }
        return Path.Combine(basePath, "Nocturne", "certificates");
    }

    /// <summary>
    /// Ensures all internal certificates exist, generating them if necessary.
    /// </summary>
    /// <param name="connectorNames">Names of connectors that need client certificates.</param>
    /// <returns>True if all certificates are valid, false if generation failed.</returns>
    public static bool EnsureCertificatesExist(IEnumerable<string> connectorNames)
    {
        try
        {
            Directory.CreateDirectory(CertificateDirectory);

            // Check if CA exists and is valid
            if (!CaCertificateExists() || IsCaCertificateExpiringSoon())
            {
                Console.WriteLine("[mTLS] Generating new CA certificate...");
                GenerateCaCertificate();
            }
            else
            {
                Console.WriteLine($"[mTLS] Using existing CA certificate at {CaCertificatePath}");
            }

            // Check/generate server certificate
            if (!ServerCertificateExists() || IsServerCertificateExpiringSoon())
            {
                Console.WriteLine("[mTLS] Generating new server certificate...");
                GenerateServerCertificate();
            }
            else
            {
                Console.WriteLine($"[mTLS] Using existing server certificate at {ServerCertificatePath}");
            }

            // Check/generate connector certificates
            foreach (var connectorName in connectorNames)
            {
                var certPath = GetConnectorCertificatePath(connectorName);
                if (!File.Exists(certPath) || IsConnectorCertificateExpiringSoon(connectorName))
                {
                    Console.WriteLine($"[mTLS] Generating client certificate for connector: {connectorName}");
                    GenerateConnectorCertificate(connectorName);
                }
                else
                {
                    Console.WriteLine($"[mTLS] Using existing certificate for connector: {connectorName}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[mTLS] Error generating certificates: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generates the root CA certificate.
    /// </summary>
    private static void GenerateCaCertificate()
    {
        using var rsa = RSA.Create(4096);

        var request = new CertificateRequest(
            "CN=Nocturne Internal CA, O=Nocturne, OU=Internal",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // CA certificate extensions
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, true, 1, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature,
                true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        // CA valid for 10 years
        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddYears(10);

        using var caCert = request.CreateSelfSigned(notBefore, notAfter);

        // Export CA certificate (public)
        var caCertPem = caCert.ExportCertificatePem();
        File.WriteAllText(CaCertificatePath, caCertPem);

        // Export CA private key
        var caKeyPem = rsa.ExportRSAPrivateKeyPem();
        File.WriteAllText(CaKeyPath, caKeyPem);

        // Set restrictive permissions on key file
        SetRestrictivePermissions(CaKeyPath);

        Console.WriteLine($"[mTLS] CA certificate generated: {CaCertificatePath}");
    }

    /// <summary>
    /// Generates the server certificate for the API internal endpoint.
    /// </summary>
    private static void GenerateServerCertificate()
    {
        var caCert = LoadCaCertificateWithPrivateKey();

        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            "CN=nocturne-api, O=Nocturne, OU=Internal",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Server certificate extensions
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
                true));

        // Subject Alternative Names for localhost and service discovery names
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName("nocturne-api");
        sanBuilder.AddDnsName("127.0.0.1");
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
        sanBuilder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        // Server cert valid for 1 year
        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddYears(1);

        var serialNumber = GenerateSerialNumber();

        using var serverCert = request.Create(caCert, notBefore, notAfter, serialNumber);
        using var serverCertWithKey = serverCert.CopyWithPrivateKey(rsa);

        // Export as PFX (no password for local development)
        var pfxBytes = serverCertWithKey.Export(X509ContentType.Pfx, string.Empty);
        File.WriteAllBytes(ServerCertificatePath, pfxBytes);

        Console.WriteLine($"[mTLS] Server certificate generated: {ServerCertificatePath}");
    }

    /// <summary>
    /// Generates a client certificate for a connector.
    /// </summary>
    /// <param name="connectorName">The connector name (used as CN).</param>
    public static void GenerateConnectorCertificate(string connectorName)
    {
        var caCert = LoadCaCertificateWithPrivateKey();

        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            $"CN={connectorName}, O=Nocturne, OU=Connectors",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Client certificate extensions
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, // Client Authentication
                true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        // Client cert valid for 1 year
        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddYears(1);

        var serialNumber = GenerateSerialNumber();

        using var clientCert = request.Create(caCert, notBefore, notAfter, serialNumber);
        using var clientCertWithKey = clientCert.CopyWithPrivateKey(rsa);

        var certPath = GetConnectorCertificatePath(connectorName);

        // Export as PFX (no password for local development)
        var pfxBytes = clientCertWithKey.Export(X509ContentType.Pfx, string.Empty);
        File.WriteAllBytes(certPath, pfxBytes);

        Console.WriteLine($"[mTLS] Client certificate generated for {connectorName}: {certPath}");
    }

    /// <summary>
    /// Loads the CA certificate with its private key.
    /// </summary>
    private static X509Certificate2 LoadCaCertificateWithPrivateKey()
    {
        var caCertPem = File.ReadAllText(CaCertificatePath);
        var caKeyPem = File.ReadAllText(CaKeyPath);

        var caCert = X509Certificate2.CreateFromPem(caCertPem, caKeyPem);
        return caCert;
    }

    /// <summary>
    /// Generates a random serial number for a certificate.
    /// </summary>
    private static byte[] GenerateSerialNumber()
    {
        var serialNumber = new byte[16];
        RandomNumberGenerator.Fill(serialNumber);
        serialNumber[0] &= 0x7F; // Ensure positive
        return serialNumber;
    }

    /// <summary>
    /// Sets restrictive permissions on a file (Unix only).
    /// </summary>
    private static void SetRestrictivePermissions(string filePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Ignore permission errors
            }
        }
    }

    /// <summary>
    /// Checks if the CA certificate exists.
    /// </summary>
    private static bool CaCertificateExists() =>
        File.Exists(CaCertificatePath) && File.Exists(CaKeyPath);

    /// <summary>
    /// Checks if the server certificate exists.
    /// </summary>
    private static bool ServerCertificateExists() =>
        File.Exists(ServerCertificatePath);

    /// <summary>
    /// Checks if the CA certificate is expiring within 30 days.
    /// </summary>
    private static bool IsCaCertificateExpiringSoon()
    {
        try
        {
            var certPem = File.ReadAllText(CaCertificatePath);
            using var cert = X509Certificate2.CreateFromPem(certPem);
            return cert.NotAfter < DateTime.UtcNow.AddDays(30);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Checks if the server certificate is expiring within 7 days.
    /// </summary>
    private static bool IsServerCertificateExpiringSoon()
    {
        try
        {
            using var cert = X509CertificateLoader.LoadPkcs12FromFile(ServerCertificatePath, string.Empty);
            return cert.NotAfter < DateTime.UtcNow.AddDays(7);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Checks if a connector certificate is expiring within 7 days.
    /// </summary>
    private static bool IsConnectorCertificateExpiringSoon(string connectorName)
    {
        try
        {
            var certPath = GetConnectorCertificatePath(connectorName);
            using var cert = X509CertificateLoader.LoadPkcs12FromFile(certPath, string.Empty);
            return cert.NotAfter < DateTime.UtcNow.AddDays(7);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Gets information about all managed certificates.
    /// </summary>
    public static CertificateInfo GetCertificateInfo()
    {
        var info = new CertificateInfo
        {
            CertificateDirectory = CertificateDirectory,
            CaCertificatePath = CaCertificatePath,
            ServerCertificatePath = ServerCertificatePath
        };

        if (CaCertificateExists())
        {
            try
            {
                var certPem = File.ReadAllText(CaCertificatePath);
                using var cert = X509Certificate2.CreateFromPem(certPem);
                info.CaValidUntil = cert.NotAfter;
            }
            catch { }
        }

        if (ServerCertificateExists())
        {
            try
            {
                using var cert = X509CertificateLoader.LoadPkcs12FromFile(ServerCertificatePath, string.Empty);
                info.ServerValidUntil = cert.NotAfter;
            }
            catch { }
        }

        return info;
    }
}

/// <summary>
/// Information about the certificate infrastructure.
/// </summary>
public class CertificateInfo
{
    /// <summary>
    /// Directory where certificates are stored.
    /// </summary>
    public string CertificateDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Path to the CA certificate.
    /// </summary>
    public string CaCertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the server certificate.
    /// </summary>
    public string ServerCertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// When the CA certificate expires.
    /// </summary>
    public DateTime? CaValidUntil { get; set; }

    /// <summary>
    /// When the server certificate expires.
    /// </summary>
    public DateTime? ServerValidUntil { get; set; }
}
