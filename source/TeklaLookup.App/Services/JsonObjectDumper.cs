using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Tekla.Structures.Model;
using TeklaLookup.App.Services.Json;
using TSIdentifier = Tekla.Structures.Identifier;
using TSPoint = Tekla.Structures.Geometry3d.Point;
using TSVector = Tekla.Structures.Geometry3d.Vector;
using TSDistance = Tekla.Structures.Datatype.Distance;
using TSAngle = Tekla.Structures.Datatype.Angle;

namespace TeklaLookup.App.Services;

/// <summary>
/// Produces a curated, creation-focused JSON view of a Tekla object, intended to be pasted into an
/// LLM that will recreate a similar object via the Tekla Open API. The emphasis is on the data you
/// would actually set when constructing the object — geometry, catalog strings (profile / material),
/// class / name, position, and UDAs — while dropping noise that doesn't help recreation: internal
/// identifiers, <c>Father</c> back-references, nulls, and empty values. Property names mirror the
/// Tekla API so the mapping from JSON back to setters stays obvious.
/// </summary>
public sealed class JsonObjectDumper
{
    private const int MaxDepth = 8;
    private const int MaxItems = 2000;

    // Properties that carry no recreation value or would drag the whole model graph in.
    private static readonly HashSet<string> BlockedMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Identifier", "Father", "ModificationStamp", "Handle",
    };

    /// <summary>Dumps a single object to a JSON value (object for model objects, scalar/array otherwise).</summary>
    public JsonValue Dump(object target)
    {
        var visited = new HashSet<object>(ReferenceComparer.Instance);
        return BuildValue(target, 0, visited) ?? new JsonObject();
    }

    /// <summary>Dumps several objects as a JSON array — one curated entry per object.</summary>
    public JsonValue DumpMany(IEnumerable<object> targets)
    {
        var array = new JsonArray();
        foreach (var target in targets)
            array.Add(Dump(target));
        return array;
    }

    /// <summary>
    /// Dumps a decomposition-pane frame target. A collection frame (e.g. the result of drilling into
    /// a list of parts) is dumped as an array of full entries — one per element — rather than a single
    /// object, so each element gets a complete curated dump instead of a bare reference.
    /// </summary>
    public JsonValue DumpFrame(object target)
    {
        if (target is string || target is IDictionary)
            return Dump(target);
        if (target is IEnumerable enumerable)
            return DumpMany(enumerable.Cast<object?>().Where(o => o is not null).Cast<object>());
        return Dump(target);
    }

    /// <returns>The JSON node, or <c>null</c> to signal "omit" (null / empty / cyclic / unreadable).</returns>
    private JsonValue? BuildValue(object? value, int depth, HashSet<object> visited)
    {
        if (value is null) return null;

        switch (value)
        {
            case string s:
                return string.IsNullOrEmpty(s) ? null : JsonValue.String(s);
            case bool b:
                return JsonValue.Bool(b);
            case char c:
                return JsonValue.String(c.ToString());
            case Enum e:
                return JsonValue.String(e.ToString());
            case byte or sbyte or short or ushort or int or uint or long:
                return JsonValue.Number(Convert.ToInt64(value, CultureInfo.InvariantCulture));
            case ulong ul:
                return JsonValue.Number((double)ul);
            case float or double or decimal:
                return JsonValue.Number(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            case DateTime dt:
                return JsonValue.String(dt.ToString("o", CultureInfo.InvariantCulture));
            case Guid g:
                return JsonValue.String(g.ToString());
            case TimeSpan ts:
                return JsonValue.String(ts.ToString());
        }

        // Tekla value types collapsed to their meaningful, API-shaped form.
        switch (value)
        {
            case TSIdentifier id:
                return JsonValue.String(id.GUID.ToString());
            case TSVector vec:
                return Point(vec.X, vec.Y, vec.Z);
            case TSPoint pt:
                return Point(pt.X, pt.Y, pt.Z);
            case Profile prof:
                return string.IsNullOrEmpty(prof.ProfileString) ? null : JsonValue.String(prof.ProfileString);
            case Material mat:
                return string.IsNullOrEmpty(mat.MaterialString) ? null : JsonValue.String(mat.MaterialString);
            case TSDistance dist:
                return JsonValue.Number(dist.Millimeters);
            case TSAngle ang:
                return JsonValue.Number(ang.Radians);
        }

        // The focus object gets a full dump; a model object reached through a property is emitted as a
        // compact reference so we don't recurse through Father / bolted-part graphs.
        if (value is ModelObject modelObject)
            return depth == 0 ? BuildModelObject(modelObject, depth, visited) : BuildReference(modelObject);

        if (value is IDictionary dict)
            return BuildDictionary(dict, depth, visited);

        if (value is IEnumerable enumerable)
            return BuildArray(enumerable, depth, visited);

        if (depth >= MaxDepth)
            return JsonValue.String(value.ToString() ?? value.GetType().Name);

        var obj = new JsonObject();
        PopulateReflected(obj, value, depth, visited);
        return obj.Count == 0 ? null : obj;
    }

    private JsonValue BuildModelObject(ModelObject modelObject, int depth, HashSet<object> visited)
    {
        var obj = new JsonObject();
        obj.Add("objectType", JsonValue.String(modelObject.GetType().Name));
        PopulateReflected(obj, modelObject, depth, visited);

        var userProperties = BuildUserProperties(modelObject);
        if (userProperties is not null)
            obj.Add("userProperties", userProperties);

        return obj;
    }

    private static JsonValue BuildReference(ModelObject modelObject)
    {
        var obj = new JsonObject();
        obj.Add("objectType", JsonValue.String(modelObject.GetType().Name));
        try { obj.Add("guid", JsonValue.String(modelObject.Identifier.GUID.ToString())); }
        catch { /* GUID unavailable for uninserted objects — type alone is still informative */ }
        return obj;
    }

    private void PopulateReflected(JsonObject obj, object value, int depth, HashSet<object> visited)
    {
        var type = value.GetType();
        var track = !type.IsValueType;
        if (track && !visited.Add(value)) return; // cycle — leave what we have
        try
        {
            foreach (var member in GetMembers(type))
            {
                if (BlockedMembers.Contains(member.Name)) continue;

                object? raw;
                try { raw = member.Get(value); }
                catch { continue; }

                raw = ValueFormatting.MaterializeIfEnumerator(raw);
                var node = BuildValue(raw, depth + 1, visited);
                if (node is not null)
                    obj.Add(member.Name, node);
            }
        }
        finally
        {
            if (track) visited.Remove(value);
        }
    }

    private JsonValue? BuildUserProperties(ModelObject modelObject)
    {
        var hashtable = new Hashtable();
        try { modelObject.GetAllUserProperties(ref hashtable); }
        catch { return null; }
        if (hashtable.Count == 0) return null;

        var obj = new JsonObject();
        foreach (var key in hashtable.Keys.Cast<object>().OrderBy(k => k?.ToString(), StringComparer.Ordinal))
        {
            var node = BuildValue(hashtable[key], MaxDepth, new HashSet<object>(ReferenceComparer.Instance));
            if (node is not null)
                obj.Add(key?.ToString() ?? string.Empty, node);
        }
        return obj.Count == 0 ? null : obj;
    }

    private JsonValue? BuildDictionary(IDictionary dictionary, int depth, HashSet<object> visited)
    {
        var obj = new JsonObject();
        foreach (DictionaryEntry entry in dictionary)
        {
            var node = BuildValue(entry.Value, depth + 1, visited);
            if (node is not null)
                obj.Add(entry.Key?.ToString() ?? "<null>", node);
        }
        return obj.Count == 0 ? null : obj;
    }

    private JsonValue? BuildArray(IEnumerable enumerable, int depth, HashSet<object> visited)
    {
        var array = new JsonArray();
        foreach (var item in enumerable)
        {
            if (array.Count >= MaxItems) break;
            var node = BuildValue(item, depth + 1, visited);
            if (node is not null)
                array.Add(node);
        }
        return array.Count == 0 ? null : array;
    }

    private static JsonObject Point(double x, double y, double z)
    {
        var obj = new JsonObject();
        obj.Add("x", JsonValue.Number(x));
        obj.Add("y", JsonValue.Number(y));
        obj.Add("z", JsonValue.Number(z));
        return obj;
    }

    private static IEnumerable<ReflectedMember> GetMembers(Type type)
    {
        var properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Select(p => new ReflectedMember(p.Name, target => p.GetValue(target)));

        // Tekla geometry types expose X/Y/Z (and similar) as public fields, not properties — include
        // public instance fields too, minus compiler backing fields and any name a property covers.
        var propertyNames = new HashSet<string>(
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name));
        var fields = type
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => !f.Name.StartsWith("<", StringComparison.Ordinal))
            .Where(f => !propertyNames.Contains(f.Name))
            .Select(f => new ReflectedMember(f.Name, target => f.GetValue(target)));

        return properties.Concat(fields).OrderBy(m => m.Name, StringComparer.Ordinal);
    }

    private readonly struct ReflectedMember
    {
        public ReflectedMember(string name, Func<object, object?> get)
        {
            Name = name;
            Get = get;
        }

        public string Name { get; }
        public Func<object, object?> Get { get; }
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
