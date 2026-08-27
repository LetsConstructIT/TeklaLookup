using System;
using System.Collections.Generic;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

/// <summary>
/// Surfaces "virtual" properties for a Tekla type — values that require a method call to compute
/// (e.g. <c>Part.GetSolid()</c>, <c>Assembly.GetSecondaries()</c>) and therefore don't show up in
/// the reflection pass.
/// </summary>
public interface ITeklaTypeExtension
{
    bool Applies(object target);
    IEnumerable<PropertyEntry> Resolve(object target);
}

/// <summary>
/// Convenience base that handles the type test and supplies an <see cref="Entry"/> helper that
/// captures exceptions per-property.
/// </summary>
public abstract class TeklaTypeExtension<T> : ITeklaTypeExtension where T : class
{
    public bool Applies(object target) => target is T;

    public IEnumerable<PropertyEntry> Resolve(object target) => ResolveCore((T)target);

    protected abstract IEnumerable<PropertyEntry> ResolveCore(T target);

    protected static PropertyEntry Entry(string name, Func<object?> producer, string? typeHint = null)
    {
        try
        {
            var raw = producer();
            // Preserve the original type name so the column still says "ModelObjectEnumerator"
            // even after we drain it into a List for safe re-iteration.
            var displayType = raw?.GetType().Name ?? typeHint;
            raw = ValueFormatting.MaterializeIfEnumerator(raw);

            return new PropertyEntry
            {
                Category = PropertyCategories.Extensions,
                Name = name,
                Value = ValueFormatting.Format(raw),
                ValueType = displayType,
                RawValue = raw,
            };
        }
        catch (Exception ex)
        {
            return new PropertyEntry
            {
                Category = PropertyCategories.Extensions,
                Name = name,
                Value = $"<error: {ex.InnerException?.Message ?? ex.Message}>",
                ValueType = typeHint,
            };
        }
    }
}
