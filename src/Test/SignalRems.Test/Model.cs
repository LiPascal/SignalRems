using System;
using System.Collections.Generic;
using MessagePack;
using NUnit.Framework;

namespace SignalRems.Test
{
    [MessagePackObject(true)]
    public class Model
    {
        [Core.Attributes.Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime CreateTime { get; set; }
        public List<double> Marks { get; set; }

    }
}
