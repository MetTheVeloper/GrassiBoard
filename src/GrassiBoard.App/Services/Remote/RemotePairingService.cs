using System.Security.Cryptography;
using System.Text;

namespace GrassiBoard.Services.Remote;

internal sealed class RemotePairingService
{
    private const int PairingLifetimeSeconds = 120;
    private const int MaxManualAttempts = 8;
    private readonly object _gate = new();
    private readonly RemoteSettingsStore _store;
    private readonly RemoteSettingsDocument _settings;
    private readonly Func<DateTimeOffset> _utcNow;
    private byte[]? _secretHash;
    private byte[]? _codeBytes;
    private DateTimeOffset _expiresAt;
    private int _attempts;
    private string _pairingSecret = string.Empty;

    public RemotePairingService(RemoteSettingsStore store, RemoteSettingsDocument settings, Func<DateTimeOffset>? utcNow = null)
    {
        _store = store;
        _settings = settings;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public RemotePairingInfo CreatePairing(string baseUrl)
    {
        lock (_gate)
        {
            byte[] secret = RandomNumberGenerator.GetBytes(32);
            _pairingSecret = Base64UrlEncode(secret);
            _secretHash = SHA256.HashData(secret);
            string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            _codeBytes = Encoding.ASCII.GetBytes(code);
            _expiresAt = _utcNow().AddSeconds(PairingLifetimeSeconds);
            _attempts = 0;
            string url = $"{baseUrl.TrimEnd('/')}?pair={Uri.EscapeDataString(_pairingSecret)}";
            return new RemotePairingInfo(url, code, _expiresAt);
        }
    }

    public bool IsPairingActive
    {
        get
        {
            lock (_gate) return _secretHash is not null && _utcNow() < _expiresAt;
        }
    }

    public bool TryPair(RemotePairRequest request, out RemotePairResponse? response)
    {
        response = null;
        lock (_gate)
        {
            if (_secretHash is null || _codeBytes is null || _utcNow() >= _expiresAt) return false;
            if (_attempts >= MaxManualAttempts) return false;
            _attempts++;

            bool valid = false;
            if (!string.IsNullOrWhiteSpace(request.Secret) && TryBase64UrlDecode(request.Secret, out byte[]? secret))
            {
                byte[] candidate = SHA256.HashData(secret);
                valid = candidate.Length == _secretHash.Length && CryptographicOperations.FixedTimeEquals(candidate, _secretHash);
            }
            if (!valid && !string.IsNullOrWhiteSpace(request.Code))
            {
                byte[] candidateCode = Encoding.ASCII.GetBytes(request.Code.Trim());
                valid = candidateCode.Length == _codeBytes.Length && CryptographicOperations.FixedTimeEquals(candidateCode, _codeBytes);
            }
            if (!valid) return false;

            byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
            string token = Base64UrlEncode(tokenBytes);
            var client = new RemotePairedClientRecord
            {
                Id = Guid.NewGuid(),
                Name = NormalizeDeviceName(request.DeviceName),
                TokenHash = Convert.ToHexString(SHA256.HashData(tokenBytes)),
                CreatedAt = _utcNow(),
                LastSeenAt = _utcNow()
            };
            _settings.Clients.Add(client);
            _store.Save(_settings);
            InvalidatePairingNoLock();
            response = new RemotePairResponse(client.Id.ToString("D"), token, client.Name);
            return true;
        }
    }

    public RemotePairedClientRecord? ValidateClientToken(string token)
    {
        if (!TryBase64UrlDecode(token, out byte[]? tokenBytes)) return null;
        byte[] candidateHash = SHA256.HashData(tokenBytes);
        lock (_gate)
        {
            foreach (RemotePairedClientRecord client in _settings.Clients)
            {
                try
                {
                    byte[] stored = Convert.FromHexString(client.TokenHash);
                    if (stored.Length == candidateHash.Length && CryptographicOperations.FixedTimeEquals(stored, candidateHash))
                    {
                        client.LastSeenAt = _utcNow();
                        _store.Save(_settings);
                        return client;
                    }
                }
                catch (FormatException)
                {
                    // Corrupt credentials are ignored and can be revoked from Settings.
                }
            }
        }
        return null;
    }

    public IReadOnlyList<RemotePairedClientRecord> GetClients()
    {
        lock (_gate)
        {
            return _settings.Clients.Select(client => new RemotePairedClientRecord
            {
                Id = client.Id,
                Name = client.Name,
                TokenHash = client.TokenHash,
                CreatedAt = client.CreatedAt,
                LastSeenAt = client.LastSeenAt
            }).ToArray();
        }
    }

    public bool Revoke(Guid clientId)
    {
        lock (_gate)
        {
            int removed = _settings.Clients.RemoveAll(client => client.Id == clientId);
            if (removed > 0) _store.Save(_settings);
            return removed > 0;
        }
    }

    private void InvalidatePairingNoLock()
    {
        _secretHash = null;
        _codeBytes = null;
        _pairingSecret = string.Empty;
        _expiresAt = DateTimeOffset.MinValue;
        _attempts = 0;
    }

    private static string NormalizeDeviceName(string? value)
    {
        string name = string.IsNullOrWhiteSpace(value) ? "Android Remote" : value.Trim();
        return name.Length <= 48 ? name : name[..48];
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryBase64UrlDecode(string value, out byte[]? bytes)
    {
        bytes = null;
        try
        {
            string padded = value.Replace('-', '+').Replace('_', '/');
            padded += padded.Length % 4 switch { 2 => "==", 3 => "=", _ => string.Empty };
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
