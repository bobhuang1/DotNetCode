namespace CleanValidation
{
    /// <summary>One leaf property discovered by <see cref="CleanValidation.FlattenObjectProperties"/>.</summary>
    public sealed class PropertyValue
    {
        public string Name { get; set; } = string.Empty;
        public object? Value { get; set; }
    }
}
