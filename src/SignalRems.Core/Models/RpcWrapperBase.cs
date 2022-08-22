using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using MessagePack;
using SignalRems.Core.Interfaces;

namespace SignalRems.Core.Models;

internal class RpcWrapperBase : IRpcMessageWrapper
{
    private byte[]? _unCompressedPayload;
    private string? _payloadText;

    [Key(0)]
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    [Key(1)]
    public bool UsingCompress { get; set; }

    public byte[]? GetPayload()
    {
        return _unCompressedPayload ??= UsingCompress ? UnCompress(Payload) : Payload;
    }

    public string? GetPayloadText()
    {
        if (_payloadText != null)
        {
            return _payloadText;
        }

        var data = GetPayload();
        return data != null ? _payloadText = Encoding.UTF8.GetString(data) : null;
    }

    public void SetPayload(byte[] payload, bool usingCompress = false)
    {
        UsingCompress = usingCompress;
        _unCompressedPayload = payload;
        Payload = UsingCompress ? Compress(payload)! : payload;
        _payloadText = null;
    }

    private static byte[]? UnCompress(byte[]? data)
    {
        if (data == null)
        {
            return null;
        }

        using var inStream = new MemoryStream(data);
        using var outStream = new MemoryStream(512);
        GZip.Decompress(inStream, outStream, true);
        return outStream.ToArray();
    }


    private static byte[]? Compress(byte[]? data)
    {
        if (data == null)
        {
            return null;
        }

        using var inStream = new MemoryStream(data);
        using var outStream = new MemoryStream(512);
        GZip.Compress(inStream, outStream, true);
        return outStream.ToArray();
    }

    public override string? ToString()
    {
        return GetPayloadText();
    }
}