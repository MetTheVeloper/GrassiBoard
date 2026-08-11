using System.Buffers;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GrassiBoard.Services.Remote;

internal sealed class RemoteServerService : IAsyncDisposable
{
    private readonly object _clientsGate = new();
    private readonly RemoteSettingsStore _settingsStore;
    private readonly RemoteSettingsDocument _settings;
    private readonly RemotePairingService _pairing;
    private readonly RemoteCommandDispatcher _commands;
    private readonly RemoteStatePublisher _statePublisher;
    private readonly RemoteTlsService _tls = new();
    private readonly RemoteMdnsService _mdns = new();
    private readonly Channel<long> _invalidations = Channel.CreateBounded<long>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly HashSet<RemoteClientConnection> _clients = [];
    private WebApplication? _app;
    private CancellationTokenSource? _lifetime;
    private Task? _broadcastTask;
    private RemoteTlsMaterial? _tlsMaterial;
    private IPAddress? _lanAddress;

    public RemoteServerService(
        RemoteSettingsStore settingsStore,
        RemoteSettingsDocument settings,
        RemotePairingService pairing,
        RemoteCommandDispatcher commands,
        RemoteStatePublisher statePublisher)
    {
        _settingsStore = settingsStore;
        _settings = settings;
        _pairing = pairing;
        _commands = commands;
        _statePublisher = statePublisher;
        _statePublisher.Invalidated += OnStateInvalidated;
    }

    public bool IsRunning => _app is not null;
    public string Address { get; private set; } = string.Empty;
    public string OnboardingAddress { get; private set; } = string.Empty;
    public string SecureAddress { get; private set; } = string.Empty;
    public string DiscoveryStatus { get; private set; } = "mDNS discovery is off";
    public string Status { get; private set; } = "Remote Control is off";
    public string NetworkHint { get; private set; } = "Enable Remote Control while the PC and phone are on the same private Wi-Fi/LAN.";
    public RemotePairingInfo? CurrentPairing { get; private set; }

    public event Action? Changed;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null) return;

        IPAddress? address = RemoteNetworkInfo.GetPreferredPrivateIpv4();
        if (address is null)
        {
            Status = "No private LAN address was found";
            NetworkHint = "Connect this PC to the same private Wi-Fi/LAN as your phone, then restart Remote Control.";
            Changed?.Invoke();
            return;
        }

        RemoteTlsMaterial tlsMaterial;
        try
        {
            tlsMaterial = _tls.GetOrCreate(address);
        }
        catch (IOException)
        {
            SetTlsFailureStatus();
            return;
        }
        catch (UnauthorizedAccessException)
        {
            SetTlsFailureStatus();
            return;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            SetTlsFailureStatus();
            return;
        }

        CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(RemoteServerService).Assembly.GetName().Name ?? "GrassiBoard",
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(address, _settings.Port);
            options.Listen(address, _settings.SecurePort, listen => listen.UseHttps(tlsMaterial.ServerCertificate));
        });
        WebApplication app = builder.Build();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
        app.Use(ApplyDevelopmentCorsAsync);

        app.MapGet("/api/remote/info", () => Results.Json(new
        {
            protocolVersion = RemoteProtocol.Version,
            name = "GrassiMote",
            pairingOpen = _pairing.IsPairingActive,
            secureOrigin = SecureAddress,
            onboardingOrigin = OnboardingAddress,
            stableHost = RemoteTlsService.StableHostName,
            mdnsAvailable = _mdns.IsRunning
        }, RemoteProtocol.JsonOptions));

        app.MapGet("/api/remote/ca.cer", () =>
        {
            byte[] certificate = _tlsMaterial?.RootCertificateDer ?? tlsMaterial.RootCertificateDer;
            return Results.File(certificate, "application/x-x509-ca-cert", "GrassiMote-Local-CA.cer");
        });
        app.MapGet("/onboard", HandleOnboardingAsync);
        app.MapPost("/api/remote/pair", HandlePairAsync);
        app.Map("/ws", HandleWebSocketAsync);

        string webRoot = Path.Combine(AppContext.BaseDirectory, "RemoteWeb");
        if (Directory.Exists(webRoot) && File.Exists(Path.Combine(webRoot, "index.html")))
        {
            var provider = new PhysicalFileProvider(webRoot);
            var defaults = new DefaultFilesOptions { FileProvider = provider };
            defaults.DefaultFileNames.Clear();
            defaults.DefaultFileNames.Add("index.html");
            app.UseDefaultFiles(defaults);
            var contentTypes = new FileExtensionContentTypeProvider();
            contentTypes.Mappings[".webmanifest"] = "application/manifest+json";
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = provider,
                ContentTypeProvider = contentTypes,
                OnPrepareResponse = responseContext =>
                {
                    string name = responseContext.File.Name;
                    if (string.Equals(name, "sw.js", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "manifest.webmanifest", StringComparison.OrdinalIgnoreCase))
                    {
                        responseContext.Context.Response.Headers.CacheControl = "no-cache";
                    }
                }
            });
            app.MapFallback(async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                byte[] html = await File.ReadAllBytesAsync(Path.Combine(webRoot, "index.html"), context.RequestAborted);
                await context.Response.Body.WriteAsync(html, context.RequestAborted);
            });
        }
        else
        {
            app.MapFallback(async context =>
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync("GrassiMote web assets are missing from this build.", context.RequestAborted);
            });
        }

        try
        {
            await app.StartAsync(cancellationToken);
        }
        catch
        {
            lifetime.Dispose();
            await app.DisposeAsync();
            tlsMaterial.Dispose();
            Status = "Remote server could not start";
            NetworkHint = $"Ports {_settings.Port}/{_settings.SecurePort} may be unavailable. Restart Remote Control or close another app using those ports.";
            Changed?.Invoke();
            return;
        }

        _app = app;
        _lifetime = lifetime;
        _tlsMaterial = tlsMaterial;
        _lanAddress = address;
        bool mdnsAvailable = await _mdns.StartAsync(address, lifetime.Token);
        // The direct IP origin is the compatibility path and also works when Android is
        // acting as a mobile hotspot, where .local/mDNS resolution may be unavailable.
        // grassimote.local remains a convenience alias for ordinary LAN/Wi-Fi networks.
        SecureAddress = $"https://{address}:{_settings.SecurePort}/";
        OnboardingAddress = $"http://{address}:{_settings.Port}/onboard";
        Address = SecureAddress;
        DiscoveryStatus = mdnsAvailable
            ? $"Stable LAN alias advertised: {RemoteTlsService.StableHostName} (secure IP is always available)"
            : $"mDNS responder unavailable; secure IP mode is active ({address}).";
        Status = "GrassiMote secure Remote is running";
        NetworkHint = mdnsAvailable
            ? $"Use the secure IP on phone-hotspot/mobile-data networks. On ordinary Wi-Fi/LAN you can also try {RemoteTlsService.StableHostName}."
            : "Secure GrassiMote is available by LAN IP. If DHCP changes this PC address, scan the new pairing QR again.";
        CurrentPairing = CreateOnboardingPairing();
        _broadcastTask = BroadcastLoopAsync(lifetime.Token);
        _invalidations.Writer.TryWrite(_statePublisher.Revision);
        Changed?.Invoke();
    }

    private void SetTlsFailureStatus()
    {
        Status = "GrassiMote HTTPS certificate setup failed";
        NetworkHint = "GrassiBoard could not create its local GrassiMote certificate under your AppData folder.";
        Changed?.Invoke();
    }

    public async Task StopAsync()
    {
        WebApplication? app = _app;
        CancellationTokenSource? lifetime = _lifetime;
        if (app is null) return;

        _app = null;
        _lifetime = null;
        lifetime?.Cancel();

        RemoteClientConnection[] clients;
        lock (_clientsGate) clients = _clients.ToArray();
        foreach (RemoteClientConnection client in clients)
        {
            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Remote server stopped");
        }

        using (var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
        {
            try { await app.StopAsync(stopTimeout.Token); }
            catch (OperationCanceledException) { }
        }
        await app.DisposeAsync();
        if (_broadcastTask is not null)
        {
            try { await _broadcastTask; }
            catch (OperationCanceledException) { }
        }
        _broadcastTask = null;
        await _mdns.StopAsync();
        lifetime?.Dispose();
        _tlsMaterial = null;
        _lanAddress = null;
        Address = string.Empty;
        SecureAddress = string.Empty;
        OnboardingAddress = string.Empty;
        DiscoveryStatus = "mDNS discovery is off";
        CurrentPairing = null;
        Status = "Remote Control is off";
        NetworkHint = "Enable Remote Control while the PC and phone are on the same private Wi-Fi/LAN.";
        Changed?.Invoke();
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    public void RegeneratePairing()
    {
        if (_app is null || string.IsNullOrWhiteSpace(SecureAddress)) return;
        CurrentPairing = CreateOnboardingPairing();
        Changed?.Invoke();
    }

    private RemotePairingInfo CreateOnboardingPairing()
    {
        RemotePairingInfo securePairing = _pairing.CreatePairing(SecureAddress);
        string query = new Uri(securePairing.Url).Query;
        string onboardingUrl = $"{OnboardingAddress}{query}";
        return new RemotePairingInfo(onboardingUrl, securePairing.Code, securePairing.ExpiresAt);
    }

    private async Task HandleOnboardingAsync(HttpContext context)
    {
        string querySuffix = context.Request.QueryString.HasValue ? context.Request.QueryString.Value ?? string.Empty : string.Empty;
        string secureIpTarget = _lanAddress is null
            ? SecureAddress
            : $"https://{_lanAddress}:{_settings.SecurePort}/{querySuffix}";
        string stableTarget = $"https://{RemoteTlsService.StableHostName}:{_settings.SecurePort}/{querySuffix}";
        string safeSecureIp = WebUtility.HtmlEncode(secureIpTarget);
        string safeStable = WebUtility.HtmlEncode(stableTarget);
        string safeHost = WebUtility.HtmlEncode(RemoteTlsService.StableHostName);
        string html = $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
  <meta name="theme-color" content="#07111f">
  <title>GrassiMote Secure Setup</title>
  <style>
    :root{color-scheme:dark;font-family:Inter,system-ui,-apple-system,"Segoe UI",sans-serif;background:#07111f;color:#edf6ff}
    *{box-sizing:border-box}body{margin:0;min-height:100vh;background:radial-gradient(circle at 50% -10%,#123454,#07111f 48%);padding:24px 16px}
    main{max-width:560px;margin:5vh auto;background:linear-gradient(145deg,#182d46,#0a1828);border:1px solid rgba(151,191,230,.17);border-radius:22px;padding:24px;box-shadow:0 18px 60px rgba(0,0,0,.28)}
    .brand{font-size:.75rem;letter-spacing:.16em;color:#82a4c3;font-weight:800}.status{display:inline-block;border-radius:999px;padding:6px 10px;background:rgba(93,226,166,.1);color:#5de2a6;font-size:.78rem}
    h1{font-size:2rem;margin:10px 0 8px}p{color:#a6b8ca;line-height:1.6}.step{margin-top:18px;padding:16px;border:1px solid rgba(151,191,230,.14);border-radius:16px;background:rgba(7,17,31,.45)}
    .step strong{display:block;margin-bottom:6px}.button{display:block;text-align:center;text-decoration:none;margin-top:12px;padding:14px;border-radius:13px;font-weight:800;background:linear-gradient(135deg,#0e8cdb,#2abcf0);color:#001522}
    .button.secondary{background:#10253c;color:#edf6ff;border:1px solid rgba(151,191,230,.15)}code{word-break:break-all;color:#60d5ff}.tiny{font-size:.78rem;color:#8198ad}
  </style>
</head>
<body><main>
  <div class="brand">GRASSIMOTE</div><span class="status">Local secure setup</span>
  <h1>Trust this GrassiBoard once</h1>
  <p>GrassiMote needs a trusted HTTPS origin for PWA installation and camera access. The private CA key stays on this Windows PC; only its public certificate is downloaded to your phone.</p>
  <div class="step"><strong>1 · Install the local CA certificate</strong><p>Download the certificate, then install it as a <b>CA certificate</b> in Android. Menu wording varies by phone.</p><a class="button" href="/api/remote/ca.cer" download>Download GrassiMote CA</a><p class="tiny">Android may show a warning that a user CA can inspect secure traffic. Only keep this CA installed while you trust this PC.</p></div>
  <div class="step"><strong>2 · Open secure GrassiMote</strong><p>After the certificate is installed, use the direct secure LAN IP. This is the compatible path for ordinary Wi-Fi and for Android phone-hotspot/mobile-data setups.</p><a class="button" href="{{safeSecureIp}}">Open secure GrassiMote</a><p class="tiny">Optional stable alias on networks where mDNS is available: <code>{{safeHost}}</code></p><a class="button secondary" href="{{safeStable}}">Try grassimote.local</a></div>
  <p class="tiny">Both devices must be on the same local link. VPNs must allow local-network traffic. The .local alias is optional; direct secure IP access remains supported.</p>
</main></body></html>
""";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html, context.RequestAborted);
    }

    public async Task<bool> RevokeClientAsync(Guid clientId)
    {
        bool revoked = _pairing.Revoke(clientId);
        RemoteClientConnection[] matches;
        lock (_clientsGate) matches = _clients.Where(client => client.ClientId == clientId).ToArray();
        foreach (RemoteClientConnection client in matches)
        {
            await client.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Device revoked");
        }
        Changed?.Invoke();
        return revoked;
    }

    public IReadOnlyList<RemoteClientDisplay> GetClientDisplays()
    {
        HashSet<Guid> connected;
        lock (_clientsGate) connected = _clients.Where(client => client.Authenticated).Select(client => client.ClientId).ToHashSet();
        return _pairing.GetClients()
            .Select(client => new RemoteClientDisplay(client.Id, client.Name, connected.Contains(client.Id), client.CreatedAt, client.LastSeenAt))
            .OrderByDescending(client => client.Connected)
            .ThenBy(client => client.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void SetEnabledPreference(bool enabled)
    {
        _settings.Enabled = enabled;
        _settingsStore.Save(_settings);
    }

    private async Task HandlePairAsync(HttpContext context)
    {
        if (!context.Request.IsHttps)
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            await context.Response.WriteAsJsonAsync(
                new { error = "Pairing is available only on the secure GrassiMote HTTPS origin." },
                RemoteProtocol.JsonOptions,
                context.RequestAborted);
            return;
        }

        RemotePairRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<RemotePairRequest>(
                context.Request.Body, RemoteProtocol.JsonOptions, context.RequestAborted);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null || !_pairing.TryPair(request, out RemotePairResponse? response) || response is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Pairing code/secret is invalid, expired, or locked." }, RemoteProtocol.JsonOptions, context.RequestAborted);
            return;
        }

        if (!string.IsNullOrWhiteSpace(OnboardingAddress)) CurrentPairing = CreateOnboardingPairing();
        Changed?.Invoke();
        await context.Response.WriteAsJsonAsync(response, RemoteProtocol.JsonOptions, context.RequestAborted);
    }

    private async Task HandleWebSocketAsync(HttpContext context)
    {
        if (!context.Request.IsHttps)
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
        var client = new RemoteClientConnection(socket, context.RequestAborted);
        await client.SendAsync(new
        {
            protocolVersion = RemoteProtocol.Version,
            type = "connection.hello",
            revision = _statePublisher.Revision,
            payload = new { requiresAuth = true }
        });

        try
        {
            using var authTimeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            authTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            string? authText = await ReceiveTextAsync(socket, authTimeout.Token);
            RemoteIncomingEnvelope? auth = DeserializeEnvelope(authText);
            if (auth is null || auth.ProtocolVersion != RemoteProtocol.Version || auth.Type != "connection.auth" ||
                !TryGetString(auth.Payload, "token", out string token))
            {
                await client.SendErrorAsync(auth?.MessageId, "unauthorized", "Authenticate this paired device first.");
                await client.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Authentication required");
                return;
            }

            RemotePairedClientRecord? record = _pairing.ValidateClientToken(token);
            if (record is null)
            {
                await client.SendErrorAsync(auth.MessageId, "unauthorized", "This device is not paired or has been revoked.");
                await client.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid credential");
                return;
            }

            client.Authenticate(record.Id, record.Name);
            lock (_clientsGate) _clients.Add(client);
            Changed?.Invoke();
            await client.SendAckAsync(auth.MessageId, "connection.auth");
            RemoteStateSnapshot initial = await _commands.CreateSnapshotAsync(_statePublisher.Revision);
            await client.SendSnapshotAsync(initial);

            while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
            {
                string? text = await ReceiveTextAsync(socket, context.RequestAborted);
                if (text is null) break;
                RemoteIncomingEnvelope? envelope = DeserializeEnvelope(text);
                if (envelope is null)
                {
                    await client.SendErrorAsync(null, "invalid_message", "The message is not valid Remote protocol JSON.");
                    continue;
                }
                if (envelope.ProtocolVersion != RemoteProtocol.Version)
                {
                    await client.SendErrorAsync(envelope.MessageId, "protocol_mismatch", $"Remote protocol {RemoteProtocol.Version} is required.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(envelope.MessageId) || envelope.MessageId.Length > 128)
                {
                    await client.SendErrorAsync(null, "message_id_required", "Every command requires a unique messageId of at most 128 characters.");
                    continue;
                }
                if (!client.TryAcceptMessageId(envelope.MessageId))
                {
                    await client.SendErrorAsync(envelope.MessageId, "duplicate_message", "That messageId was already processed on this connection.");
                    continue;
                }
                if (envelope.Type == "connection.auth")
                {
                    await client.SendErrorAsync(envelope.MessageId, "already_authenticated", "This connection is already authenticated.");
                    continue;
                }

                RemoteCommandResult result = await _commands.ExecuteAsync(envelope);
                if (result.Success)
                {
                    await client.SendAckAsync(envelope.MessageId, envelope.Type);
                    _statePublisher.Invalidate();
                }
                else
                {
                    await client.SendErrorAsync(envelope.MessageId, result.ErrorCode ?? "command_failed", result.ErrorMessage ?? "Command failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Browser backgrounding/network loss is expected.
        }
        catch (WebSocketException)
        {
            // Connection loss is isolated from GrassiBoard's audio engine.
        }
        finally
        {
            lock (_clientsGate) _clients.Remove(client);
            Changed?.Invoke();
        }
    }

    private async Task BroadcastLoopAsync(CancellationToken cancellationToken)
    {
        while (await _invalidations.Reader.WaitToReadAsync(cancellationToken))
        {
            long revision = _statePublisher.Revision;
            while (_invalidations.Reader.TryRead(out long queued)) revision = Math.Max(revision, queued);
            await Task.Delay(35, cancellationToken);
            while (_invalidations.Reader.TryRead(out long queued)) revision = Math.Max(revision, queued);

            RemoteClientConnection[] clients;
            lock (_clientsGate) clients = _clients.Where(client => client.Authenticated).ToArray();
            if (clients.Length == 0) continue;

            RemoteStateSnapshot snapshot = await _commands.CreateSnapshotAsync(revision);
            foreach (RemoteClientConnection client in clients)
            {
                try { await client.SendSnapshotAsync(snapshot); }
                catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException) { }
            }
        }
    }

    private void OnStateInvalidated(long revision) => _invalidations.Writer.TryWrite(revision);

    private static async Task ApplyDevelopmentCorsAsync(HttpContext context, Func<Task> next)
    {
        string origin = context.Request.Headers.Origin.ToString();
        if (Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) && uri.IsLoopback)
        {
            context.Response.Headers.AccessControlAllowOrigin = origin;
            context.Response.Headers.AccessControlAllowHeaders = "Content-Type";
            context.Response.Headers.AccessControlAllowMethods = "GET,POST,OPTIONS";
            context.Response.Headers.Vary = "Origin";
        }
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }
        await next();
    }

    private static RemoteIncomingEnvelope? DeserializeEnvelope(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return JsonSerializer.Deserialize<RemoteIncomingEnvelope>(text, RemoteProtocol.JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static bool TryGetString(JsonElement payload, string name, out string value)
    {
        value = string.Empty;
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.String)
            return false;
        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static async Task<string?> ReceiveTextAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            using var stream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) return null;
                if (result.MessageType != WebSocketMessageType.Text) return null;
                if (stream.Length + result.Count > RemoteProtocol.MaxMessageBytes) return null;
                stream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);
            return Encoding.UTF8.GetString(stream.GetBuffer(), 0, checked((int)stream.Length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _statePublisher.Invalidated -= OnStateInvalidated;
        await StopAsync();
        await _mdns.DisposeAsync();
        _tls.Dispose();
    }

    private sealed class RemoteClientConnection
    {
        private const int RecentMessageLimit = 256;
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private readonly CancellationToken _requestAborted;
        private readonly Queue<string> _recentMessageIds = new();
        private readonly HashSet<string> _recentMessageSet = new(StringComparer.Ordinal);

        public RemoteClientConnection(WebSocket socket, CancellationToken requestAborted)
        {
            Socket = socket;
            _requestAborted = requestAborted;
        }

        public WebSocket Socket { get; }
        public Guid ClientId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public bool Authenticated => ClientId != Guid.Empty;

        public void Authenticate(Guid clientId, string name)
        {
            ClientId = clientId;
            Name = name;
        }

        public bool TryAcceptMessageId(string messageId)
        {
            if (!_recentMessageSet.Add(messageId)) return false;
            _recentMessageIds.Enqueue(messageId);
            if (_recentMessageIds.Count > RecentMessageLimit)
            {
                string oldest = _recentMessageIds.Dequeue();
                _recentMessageSet.Remove(oldest);
            }
            return true;
        }

        public Task SendAckAsync(string? messageId, string command) => SendAsync(new
        {
            protocolVersion = RemoteProtocol.Version,
            type = "ack",
            messageId,
            revision = 0,
            payload = new { command }
        });

        public Task SendErrorAsync(string? messageId, string code, string message) => SendAsync(new
        {
            protocolVersion = RemoteProtocol.Version,
            type = "error",
            messageId,
            revision = 0,
            payload = new { code, message }
        });

        public Task SendSnapshotAsync(RemoteStateSnapshot snapshot) => SendAsync(new
        {
            protocolVersion = RemoteProtocol.Version,
            type = "state.snapshot",
            revision = snapshot.Revision,
            payload = snapshot
        });

        public async Task SendAsync(object message)
        {
            if (Socket.State != WebSocketState.Open) return;
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(message, RemoteProtocol.JsonOptions);
            await _sendGate.WaitAsync(_requestAborted);
            try
            {
                if (Socket.State == WebSocketState.Open)
                    await Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _requestAborted);
            }
            finally
            {
                _sendGate.Release();
            }
        }

        public async Task CloseAsync(WebSocketCloseStatus status, string description)
        {
            using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try
            {
                if (Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    await Socket.CloseAsync(status, description, closeTimeout.Token);
            }
            catch (OperationCanceledException)
            {
                Socket.Abort();
            }
            catch (WebSocketException)
            {
                Socket.Abort();
            }
        }
    }
}
