using System.Diagnostics;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using SignalRems.Core.Interfaces;


[assembly:InternalsVisibleTo("SignalRems.Server")]
[assembly:InternalsVisibleTo("SignalRems.Client")]
namespace SignalRems.Core.Utils;

internal static class SerializeUtil
{
    internal static bool UseMessagePack { get; set; } = false;

    private static string ToJson<T>(T entity)
    {
        return JsonConvert.SerializeObject(entity);
    }

    private static T? FromJson<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json);
    }

    private static async ValueTask<byte[]> ToBinaryAsync<T>(T entity)
    {
        await using var stream = new MemoryStream();
        await MessagePack.MessagePackSerializer.SerializeAsync(stream, entity);
        return stream.ToArray();
    }

    private static async ValueTask<T?> FromBinaryAsync<T>(byte[] data)
    {
        await using var stream = new MemoryStream(data);
        return await MessagePack.MessagePackSerializer.DeserializeAsync<T>(stream);
    }

    internal static async ValueTask<TWrapper> SerializeAsync<T, TWrapper>(T entity) where TWrapper: IRpcMessageWrapper, new ()
    {
        var wrapper = new TWrapper();
        if (UseMessagePack)
        {
            wrapper.BinaryPayload = await ToBinaryAsync<T>(entity);
        }
        else
        {
            wrapper.JsonPayload = ToJson(entity);
        }

        return wrapper;
    }

    internal static async ValueTask<T?> DeserializeAsync<T, TWrapper>(TWrapper wrapper) where TWrapper : IRpcMessageWrapper
    {
        return UseMessagePack ? 
            wrapper.BinaryPayload == null ? default : await FromBinaryAsync<T>(wrapper.BinaryPayload) :
            wrapper.JsonPayload == null ? default : FromJson<T>(wrapper.JsonPayload);
    }

}