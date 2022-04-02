using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;


namespace SignalRems.Core.Utils
{
    public static class JsonUtil
    { 
        public static string ToJson<T>(T entity)
        {
            return JsonConvert.SerializeObject(entity);
        }

        public static T? FromJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
