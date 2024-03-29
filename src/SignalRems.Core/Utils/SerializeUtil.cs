﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack.Formatters;
using Microsoft.Extensions.Logging;
using SignalRems.Core.Interfaces;


[assembly: InternalsVisibleTo("SignalRems.Server")]
[assembly: InternalsVisibleTo("SignalRems.Client")]

namespace SignalRems.Core.Utils;

internal static class SerializeUtil
{
    private static JsonConverter[] _converters = Array.Empty<JsonConverter>();
    internal static bool UseMessagePack { get; set; } = false;
    private static JsonSerializerOptions JsonSerializerOptions { get; set; } = new();

    internal static JsonConverter[] Converters
    {
        get => _converters;
        set
        {
            if (_converters == value)
            {
                return;
            }

            _converters = value;
            var options = new JsonSerializerOptions();
            foreach (var converter in value)
            {
                options.Converters.Add(converter);
            }

            JsonSerializerOptions = options;
        }
    }

    private static byte[] ToJson<T>(T entity)
    {
        return JsonSerializer.SerializeToUtf8Bytes(entity, JsonSerializerOptions);
    }

    private static T? FromJson<T>(byte[] json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
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

    internal static async ValueTask<TWrapper> SerializeAsync<T, TWrapper>(T entity, bool compress = false)
        where TWrapper : IRpcMessageWrapper, new()
    {
        var wrapper = new TWrapper();
        var payload = UseMessagePack ? await ToBinaryAsync<T>(entity) : ToJson(entity);
        wrapper.SetPayload(payload, compress);
        return wrapper;
    }

    internal static async ValueTask<T?> DeserializeAsync<T, TWrapper>(TWrapper wrapper)
        where TWrapper : IRpcMessageWrapper
    {
        var payload = wrapper.GetPayload();
        if (payload == null)
        {
            return default;
        }

        return UseMessagePack ? await FromBinaryAsync<T>(payload) : FromJson<T>(payload);
    }

    internal static void Log<TWrapper>(this ILogger logger, string prefix, TWrapper wrapper, LogLevel level)
        where TWrapper : IRpcMessageWrapper
    {
        if (level == LogLevel.None)
        {
            return;
        }

        if (!UseMessagePack)
        {
            logger.Log(level, "{0}: {1}", prefix, wrapper.GetPayloadText());
        }
    }
}