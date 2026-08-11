using System.Buffers;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

        CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(RemoteServerService).Assembly.GetName().Name ?? "GrassiBoard",
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(address, _settings.Port));
        WebApplication app = builder.Build();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
        app.Use(ApplyDevelopmentCorsAsync);

        app.MapGet("/api/remote/info", () => Results.Json(new
        {
            protocolVersion = RemoteProtocol.Version,
            name = "GrassiBoard Remote",
            pairingOpen = _pairing.IsPairingActive
        }, RemoteProtocol.JsonOptions));

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
            app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
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
                await context.Response.WriteAsync("GrassiBoard Remote web assets are missing from this build.", context.RequestAborted);
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
            Status = "Remote server could not start";
            NetworkHint = $"Port {_settings.Port} may be unavailable. Restart Remote Control or close another app using that port.";
            Changed?.Invoke();
            return;
        }

        _app = app;
        _lifetime = lifetime;
        Address = $"http://{address}:{_settings.Port}/";
        Status = "Remote Control is running";
        NetworkHint = "If your phone cannot open the address, keep both devices on the same Wi-Fi and allow GrassiBoard on Private networks in Windows Firewall.";
        CurrentPairing = _pairing.CreatePairing(Address);
        _broadcastTask = BroadcastLoopAsync(lifetime.Token);
        _invalidations.Writer.TryWrite(_statePublisher.Revision);
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
        lifetime?.Dispose();
        Address = string.Empty;
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
        if (_app is null || string.IsNullOrWhiteSpace(Address)) return;
        CurrentPairing = _pairing.CreatePairing(Address);
        Changed?.Invoke();
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

        if (!string.IsNullOrWhiteSpace(Address)) CurrentPairing = _pairing.CreatePairing(Address);
        Changed?.Invoke();
        await context.Response.WriteAsJsonAsync(response, RemoteProtocol.JsonOptions, context.RequestAborted);
    }

    private async Task HandleWebSocketAsync(HttpContext context)
    {
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
            try
            {
                if (Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    await Socket.CloseAsync(status, description, CancellationToken.None);
            }
            catch (WebSocketException) { }
        }
    }
}
