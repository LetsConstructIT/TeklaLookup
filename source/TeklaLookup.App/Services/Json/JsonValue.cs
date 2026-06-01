using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TeklaLookup.App.Services.Json;

/// <summary>
/// Minimal, dependency-free JSON document model + indented writer. net48 has no
/// <c>System.Text.Json</c>, and the curated dump has an arbitrary, nested shape that
/// <see cref="System.Runtime.Serialization.Json.DataContractJsonSerializer"/> can't express
/// cleanly, so we build a tiny node tree and render it ourselves.
/// </summary>
public abstract class JsonValue
{
    public static JsonValue Null { get; } = new JsonScalar("null", isRaw: true);
    public static JsonValue Bool(bool value) => new JsonScalar(value ? "true" : "false", isRaw: true);
    public static JsonValue String(string value) => new JsonScalar(value, isRaw: false);

    public static JsonValue Number(double value)
    {
        // JSON has no NaN/Infinity literals — fall back to a string so the document stays valid.
        if (double.IsNaN(value) || double.IsInfinity(value))
            return String(value.ToString(CultureInfo.InvariantCulture));
        var rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
        return new JsonScalar(rounded.ToString("0.######", CultureInfo.InvariantCulture), isRaw: true);
    }

    public static JsonValue Number(long value) =>
        new JsonScalar(value.ToString(CultureInfo.InvariantCulture), isRaw: true);

    public string ToIndentedString()
    {
        var builder = new StringBuilder();
        Write(builder, 0);
        return builder.ToString();
    }

    internal abstract void Write(StringBuilder builder, int indent);

    private protected static void AppendIndent(StringBuilder builder, int indent)
        => builder.Append(' ', indent * 2);

    private protected static void AppendEscaped(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (c < ' ')
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        builder.Append(c);
                    break;
            }
        }
        builder.Append('"');
    }
}

internal sealed class JsonScalar : JsonValue
{
    private readonly string _text;
    private readonly bool _isRaw;

    public JsonScalar(string text, bool isRaw)
    {
        _text = text;
        _isRaw = isRaw;
    }

    internal override void Write(StringBuilder builder, int indent)
    {
        if (_isRaw)
            builder.Append(_text);
        else
            AppendEscaped(builder, _text);
    }
}

/// <summary>Insertion-ordered JSON object. Order is preserved so the dump stays stable and readable.</summary>
public sealed class JsonObject : JsonValue
{
    private readonly List<KeyValuePair<string, JsonValue>> _members = new();

    public int Count => _members.Count;

    public void Add(string key, JsonValue value) => _members.Add(new(key, value));

    internal override void Write(StringBuilder builder, int indent)
    {
        if (_members.Count == 0)
        {
            builder.Append("{}");
            return;
        }

        builder.Append("{\n");
        for (var i = 0; i < _members.Count; i++)
        {
            AppendIndent(builder, indent + 1);
            AppendEscaped(builder, _members[i].Key);
            builder.Append(": ");
            _members[i].Value.Write(builder, indent + 1);
            if (i < _members.Count - 1) builder.Append(',');
            builder.Append('\n');
        }
        AppendIndent(builder, indent);
        builder.Append('}');
    }
}

public sealed class JsonArray : JsonValue
{
    private readonly List<JsonValue> _items = new();

    public int Count => _items.Count;

    public void Add(JsonValue value) => _items.Add(value);

    internal override void Write(StringBuilder builder, int indent)
    {
        if (_items.Count == 0)
        {
            builder.Append("[]");
            return;
        }

        builder.Append("[\n");
        for (var i = 0; i < _items.Count; i++)
        {
            AppendIndent(builder, indent + 1);
            _items[i].Write(builder, indent + 1);
            if (i < _items.Count - 1) builder.Append(',');
            builder.Append('\n');
        }
        AppendIndent(builder, indent);
        builder.Append(']');
    }
}
