using System;
using System.Collections.Generic;
using MessagePack;

namespace SignalRems.Test.Data
{
    [MessagePackObject(true)]
    public class Model
    {
        [Core.Attributes.Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime CreateTime { get; set; }
        public List<double> Marks { get; set; }
        public Status Status { get; set; }
    }
}
