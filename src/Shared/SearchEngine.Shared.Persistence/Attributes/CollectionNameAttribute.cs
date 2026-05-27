namespace SearchEngine.Shared.Persistence.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CollectionNameAttribute : Attribute
{
    public string Name { get; }

    public CollectionNameAttribute(string name) => Name = name;
}
