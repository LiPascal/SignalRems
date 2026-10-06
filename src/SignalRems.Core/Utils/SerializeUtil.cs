using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
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

    /// <summary>
    /// Extra resolvers shared by the payload serializer and the hub protocol. Order matters:
    /// <see cref="AttributeFormatterResolver"/> must win so types annotated with <c>[MessagePackFormatter]</c>
    /// (e.g. <c>Status</c>) keep their custom formatter; <see cref="NativeDateTimeResolver"/> preserves the exact
    /// <see cref="DateTime"/> (kind/ticks) like System.Text.Json instead of converting it to UTC;
    /// <see cref="DynamicObjectResolverAllowPrivate"/> handles the internal types annotated with
    /// <c>[MessagePackObject]</c> (the RPC wrappers) that the public-only attribute resolver cannot build;
    /// <see cref="ContractlessStandardResolver"/> handles everything else.
    /// </summary>
    private static readonly IFormatterResolver[] CustomResolvers =
    {
        AttributeFormatterResolver.Instance,
        NativeDateTimeResolver.Instance,
        DynamicUnionResolver.Instance,
        DynamicObjectResolverAllowPrivate.Instance,
        ContractlessStandardResolver.Instance,
    };

    private static MessagePackSerializerOptions _messagePackOptions = BuildMessagePackOptions(null);

    /// <summary>
    /// The custom resolver supplied by the host application, or null to use the default contractless resolver.
    /// </summary>
    internal static IFormatterResolver? MessagePackResolver { get; private set; }

    /// <summary>
    /// Options used by the MessagePack payload serializer. Defaults to a contractless resolver so that plain
    /// POCOs (which System.Text.Json serializes by default) also round-trip with MessagePack.
    /// </summary>
    internal static MessagePackSerializerOptions MessagePackOptions => _messagePackOptions;

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

    /// <summary>
    /// Configures the MessagePack resolver used by both the payload serializer and the SignalR MessagePack hub
    /// protocol. When <paramref name="resolver"/> is null a contractless resolver is used, matching the
    /// default behavior of System.Text.Json with POCOs.
    /// </summary>
    internal static void ConfigureMessagePack(IFormatterResolver? resolver)
    {
        MessagePackResolver = resolver;
        _messagePackOptions = BuildMessagePackOptions(resolver);
    }

    /// <summary>
    /// Applies the resolvers needed by the SignalR MessagePack hub protocol, keeping the protocol's built-in
    /// resolvers (enum-as-string, contractless) and security as fallback.
    /// </summary>
    internal static MessagePackSerializerOptions ApplyToHubProtocol(MessagePackSerializerOptions hubProtocolOptions)
    {
        var resolvers = new List<IFormatterResolver>();
        if (MessagePackResolver != null)
        {
            resolvers.Add(MessagePackResolver);
        }
        resolvers.AddRange(CustomResolvers);
        resolvers.Add(hubProtocolOptions.Resolver);

        return hubProtocolOptions.WithResolver(CompositeResolver.Create(resolvers.ToArray()));
    }

    private static MessagePackSerializerOptions BuildMessagePackOptions(IFormatterResolver? customResolver)
    {
        var resolvers = new List<IFormatterResolver>();
        if (customResolver != null)
        {
            resolvers.Add(customResolver);
        }
        resolvers.AddRange(CustomResolvers);

        return MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(resolvers.ToArray()));
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
        await MessagePackSerializer.SerializeAsync(stream, entity, MessagePackOptions);
        return stream.ToArray();
    }

    private static async ValueTask<T?> FromBinaryAsync<T>(byte[] data)
    {
        await using var stream = new MemoryStream(data);
        return await MessagePackSerializer.DeserializeAsync<T>(stream, MessagePackOptions);
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