using SignalRems.Core.Attributes;

namespace Samples.Model;

public class Person
{
    [Key]
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
}
