using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GrassiBoard.Services.Remote;

internal sealed class RemoteMdnsService : IAsyncDisposable
{
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("224.0.0.251");
    private const int MdnsPort = 5353;
    private readonly string _hostName;
    private UdpClient? _udp;
    private CancellationTokenSource? _lifetime;
    private Task? _loop;
    private IPAddress? _address;

    public RemoteMdnsService(string hostName = RemoteTlsService.StableHostName)
    {
        _hostName = hostName.TrimEnd('.').ToLowerInvariant();
    }

    public bool IsRunning => _udp is not null;

    public async Task<bool> StartAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        if (_udp is not null) return true;
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;

        var udp = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            udp.Client.ExclusiveAddressUse = false;
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));
            udp.JoinMulticastGroup(MulticastAddress, address);
            udp.MulticastLoopback = false;
            udp.Ttl = 255;
        }
        catch (SocketException)
        {
            udp.Dispose();
            return false;
        }

        _udp = udp;
        _address = address;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = ReceiveLoopAsync(_lifetime.Token);
        try { await SendMulticastAnswerAsync(_lifetime.Token); }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException) { }
        return true;
    }

    public async Task StopAsync()
    {
        UdpClient? udp = _udp;
        CancellationTokenSource? lifetime = _lifetime;
        Task? loop = _loop;
        _udp = null;
        _lifetime = null;
        _loop = null;
        _address = null;
        lifetime?.Cancel();
        udp?.Dispose();
        if (loop is not null)
        {
            try { await loop; }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }
        lifetime?.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        UdpClient? udp = _udp;
        if (udp is null) return;

        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult packet;
            try { packet = await udp.ReceiveAsync(cancellationToken); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (SocketException) { continue; }

            if (!TryParseQuery(packet.Buffer, out QueryInfo query) || !query.RequestsHost)
                continue;

            try
            {
                // Android's platform .local resolver uses RFC 6762 section 5.1 one-shot
                // queries from an ephemeral source port. RFC 6762 section 6.7 requires
                // responders to answer those queries directly via legacy unicast,
                // preserving the query ID and question section.
                if (packet.RemoteEndPoint.Port != MdnsPort)
                {
                    byte[] response = BuildLegacyUnicastAnswer(packet.Buffer, query);
                    await udp.SendAsync(response, packet.RemoteEndPoint, cancellationToken);
                }
                else
                {
                    await SendMulticastAnswerAsync(cancellationToken);
                }
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException)
            {
                // mDNS is a convenience discovery layer. Direct secure-IP access remains available.
            }
        }
    }

    private bool TryParseQuery(ReadOnlySpan<byte> packet, out QueryInfo query)
    {
        query = default;
        if (packet.Length < 12) return false;

        ushort id = BinaryPrimitives.ReadUInt16BigEndian(packet[0..2]);
        ushort flags = BinaryPrimitives.ReadUInt16BigEndian(packet[2..4]);
        ushort questionCount = BinaryPrimitives.ReadUInt16BigEndian(packet[4..6]);
        int offset = 12;
        bool requestsHost = false;

        for (int index = 0; index < questionCount; index++)
        {
            if (!TryReadName(packet, ref offset, out string name) || offset + 4 > packet.Length)
                return false;

            ushort type = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(offset, 2));
            offset += 4; // QTYPE + QCLASS
            if ((type == 1 || type == 255) &&
                string.Equals(name.TrimEnd('.'), _hostName, StringComparison.OrdinalIgnoreCase))
            {
                requestsHost = true;
            }
        }

        query = new QueryInfo(id, flags, questionCount, offset, requestsHost);
        return true;
    }

    private async Task SendMulticastAnswerAsync(CancellationToken cancellationToken)
    {
        UdpClient? udp = _udp;
        IPAddress? address = _address;
        if (udp is null || address is null) return;
        byte[] response = BuildMulticastAnswer(address);
        await udp.SendAsync(response, new IPEndPoint(MulticastAddress, MdnsPort), cancellationToken);
    }

    private byte[] BuildMulticastAnswer(IPAddress address)
    {
        using var stream = new MemoryStream();
        WriteUInt16(stream, 0);       // mDNS transaction id
        WriteUInt16(stream, 0x8400);  // response + authoritative answer
        WriteUInt16(stream, 0);       // questions
        WriteUInt16(stream, 1);       // answers
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteAddressAnswer(stream, address, cacheFlush: true, ttlSeconds: 120);
        return stream.ToArray();
    }

    private byte[] BuildLegacyUnicastAnswer(ReadOnlySpan<byte> request, QueryInfo query)
    {
        using var stream = new MemoryStream();
        WriteUInt16(stream, query.Id);
        // Preserve the Recursion Desired flag like a conventional DNS response while
        // still marking the local answer authoritative.
        WriteUInt16(stream, (ushort)(0x8400 | (query.Flags & 0x0100)));
        WriteUInt16(stream, query.QuestionCount);
        WriteUInt16(stream, 1);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        stream.Write(request.Slice(12, query.QuestionEnd - 12));
        if (_address is not null)
            WriteAddressAnswer(stream, _address, cacheFlush: false, ttlSeconds: 10);
        return stream.ToArray();
    }

    private void WriteAddressAnswer(Stream stream, IPAddress address, bool cacheFlush, uint ttlSeconds)
    {
        WriteName(stream, _hostName);
        WriteUInt16(stream, 1); // A
        WriteUInt16(stream, cacheFlush ? (ushort)0x8001 : (ushort)0x0001); // IN (+ cache flush for multicast only)
        WriteUInt32(stream, ttlSeconds);
        WriteUInt16(stream, 4);
        byte[] bytes = address.GetAddressBytes();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static bool TryReadName(ReadOnlySpan<byte> packet, ref int offset, out string name)
    {
        name = string.Empty;
        var labels = new List<string>();
        int cursor = offset;
        int resume = -1;
        int hops = 0;
        while (cursor < packet.Length && hops++ < 32)
        {
            byte length = packet[cursor++];
            if (length == 0)
            {
                offset = resume >= 0 ? resume : cursor;
                name = string.Join('.', labels);
                return true;
            }
            if ((length & 0xC0) == 0xC0)
            {
                if (cursor >= packet.Length) return false;
                int pointer = ((length & 0x3F) << 8) | packet[cursor++];
                if (pointer >= packet.Length) return false;
                if (resume < 0) resume = cursor;
                cursor = pointer;
                continue;
            }
            if (length > 63 || cursor + length > packet.Length) return false;
            labels.Add(Encoding.UTF8.GetString(packet.Slice(cursor, length)));
            cursor += length;
        }
        return false;
    }

    private static void WriteName(Stream stream, string hostName)
    {
        foreach (string label in hostName.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(label);
            if (bytes.Length is 0 or > 63) throw new InvalidOperationException("Invalid mDNS host label.");
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }
        stream.WriteByte(0);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private readonly record struct QueryInfo(
        ushort Id,
        ushort Flags,
        ushort QuestionCount,
        int QuestionEnd,
        bool RequestsHost);

    public async ValueTask DisposeAsync() => await StopAsync();
}
