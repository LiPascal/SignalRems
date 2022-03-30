using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;

namespace SignalRems.Core.Utils
{
    public static class MessagePackUtil
    {
        public static async Task<byte[]> ToBinaryAsync<T>(T entity)
        {
            await using var stream = new MemoryStream();
            await MessagePackSerializer.SerializeAsync(stream, entity);
            return stream.ToArray();
        }

        public static async Task<T> FromBinaryAsync<T>(byte[] data)
        {
            await using var stream = new MemoryStream(data);
            return await MessagePackSerializer.DeserializeAsync<T>(stream);
        }
    }
}
