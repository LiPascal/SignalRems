using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack;
using MessagePack.Formatters;


namespace SignalRems.Test.Data
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(StatusMessagePackFormatter))]
    public class Status
    {
        public static Status New = new Status('1', "New");
        public static Status Done = new Status('2', "Done");

        public char Id { get; }
        public string Name { get; }

        private Status(char id, string name)
        {
            Id = id;
            Name = name;
        }

        public static StatusJsonConvertor Convertor = new();
        public static StatusMessagePackFormatter Formatter = new();

        public class StatusJsonConvertor : JsonConverter<Status>
        {
            public override Status? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.GetInt16())
                {
                    case (short)'1': return New;
                    case (short)'2': return Done;
                    default: return null;
                }
            }

            public override void Write(Utf8JsonWriter writer, Status value, JsonSerializerOptions options)
            {
                writer.WriteNumberValue(value?.Id ?? 0);
            }
        }

        public class StatusMessagePackFormatter : IMessagePackFormatter<Status>
        {
            public void Serialize(ref MessagePackWriter writer, Status value, MessagePackSerializerOptions options)
            {
                var ch = value?.Id ?? '\0';
                options.Resolver.GetFormatterWithVerify<char>().Serialize(ref writer, ch, options);
                writer.Write(ch);
            }

            public Status Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                var ch = options.Resolver.GetFormatterWithVerify<char>().Deserialize(ref reader, options);
                switch (ch)
                {
                    case '1': return New;
                    case '2': return Done;
                    default: return null;
                }
            }
        }
    }
}
