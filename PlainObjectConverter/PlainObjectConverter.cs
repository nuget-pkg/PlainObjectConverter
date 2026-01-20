using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Global;

internal static class ObjectParserUtil
{
    public static string GetMemberName(MemberInfo member)
    {
        if (member.IsDefined(typeof(DataMemberAttribute), true))
        {
            var dataMemberAttribute = (DataMemberAttribute)Attribute.GetCustomAttribute(member, typeof(DataMemberAttribute), true)!;
            if (!string.IsNullOrEmpty(dataMemberAttribute.Name!))
                return dataMemberAttribute.Name;
        }

        return member.Name;
    }

    public static object? UnWrapOrExportToPlainObject(this object? x)
    {
        if (x is IPlainObjectWrapper)
        {
            x = ((IPlainObjectWrapper)x).UnWrap();
        }
        else if (x is IExportToPlainObject)
        {
            x = ((IExportToPlainObject)x).ExportToPlainObject();
        }
        return x;
    }
}
public class PlainObjectConverter: IConvertParsedResult
{
    public object? ConvertParsedResult(object? x, string origTypeName) // IConvertParsedResult
    {
        return x;
    }

    private readonly bool _forceAscii;
    IConvertParsedResult _oc;
    public PlainObjectConverter(bool forceAscii = false, IConvertParsedResult? oc = null)
    {
        this._forceAscii = forceAscii;
        if (oc == null) oc = this;
        this._oc = oc;
    }
    public static string ToPrintable(bool showDetail, object? x, string? title = null)
    {
        // ReSharper disable once RedundantArgumentDefaultValue
        PlainObjectConverter op = new PlainObjectConverter(false);
        x = x.UnWrapOrExportToPlainObject();
        string s = "";
        if (title != null) s = title + ": ";
        if (x is null) return s + "null";
        if (x is string)
        {
            if (!showDetail) return s + (string)x;
            return s + "`" + (string)x + "`";
        }
        string output /*= null*/;
        try
        {
            output = op.Stringify(x, true);
        }
        catch (Exception)
        {
            output = x.ToString()!;
        }
        if (!showDetail) return s + output;
        return s + $"<{FullName(x)}> {output}";
    }
    // ReSharper disable once MemberCanBePrivate.Global
    public static string FullName(dynamic? x)
    {
        if (x is null) return "null";
        string fullName = ((object)x).GetType().FullName!;
        return fullName.Split('`')[0];
    }
    public object? Parse(object? x, bool numberAsDecimal = false)
    {
        string origTypeName = FullName(x);
        if (x == null)
        {
            return _oc.ConvertParsedResult(null, origTypeName);
        }
        x = x.UnWrapOrExportToPlainObject();
        Type type = x!.GetType();
        if (type == typeof(string) || type == typeof(char))
        {
            return _oc.ConvertParsedResult(x.ToString(), origTypeName);
        }
        else if (type == typeof(byte) || type == typeof(sbyte)
            || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint)
            || type == typeof(long) || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal))
        {
            if (numberAsDecimal)
                return _oc.ConvertParsedResult(Convert.ToDecimal(x), origTypeName);
            return _oc.ConvertParsedResult(Convert.ToDouble(x), origTypeName);
        }
        else if (type == typeof(bool))
        {
            return _oc.ConvertParsedResult(x, origTypeName);
        }
        else if (type == typeof(DateTime))
        {
            DateTime dt = (DateTime)x;
            switch (dt.Kind)
            {
                case DateTimeKind.Local:
                    return _oc.ConvertParsedResult(dt.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffffffzzz"), origTypeName);
                case DateTimeKind.Utc:
                    return _oc.ConvertParsedResult(dt.ToString("o"), origTypeName);
                default:
                    return _oc.ConvertParsedResult(dt.ToString("o").Replace("Z", ""), origTypeName);
            }
        }
        else if (type == typeof(TimeSpan))
        {
            return _oc.ConvertParsedResult(x.ToString(), origTypeName);
        }
        else if (type == typeof(Guid))
        {
            return _oc.ConvertParsedResult(x.ToString(), origTypeName);
        }
        else if (type.IsEnum)
        {
            return _oc.ConvertParsedResult(x.ToString(), origTypeName);
        }
        else if (x is ExpandoObject)
        {
            var dic = x as IDictionary<string, object?>;
            var result = new Dictionary<string, object?>();
            foreach (var key in dic!.Keys)
            {
                result[key] = Parse(dic[key], numberAsDecimal);
            }
            return _oc.ConvertParsedResult(result, origTypeName);
        }
        else if (x is IList list)
        {
            var result = new List<object?>();
            for (int i = 0; i < list.Count; i++)
            {
                result.Add(Parse(list[i], numberAsDecimal));
            }
            return _oc.ConvertParsedResult(result, origTypeName);
        }
        else if (x is Hashtable ht)
        {
            var result = new Dictionary<string, object?>();
            foreach (object key in ht.Keys)
            {
                if (!(key is string s)) continue;
                result.Add(s, Parse(ht[s], numberAsDecimal));
            }
            return _oc.ConvertParsedResult(result, origTypeName);
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            Type keyType = type.GetGenericArguments()[0];
            var result = new Dictionary<string, object?>();
            //Refuse to output dictionary keys that aren't of type string
            if (keyType != typeof(string))
            {
                return _oc.ConvertParsedResult(result, origTypeName);
            }
            IDictionary dict = (x as IDictionary)!;
            foreach (object key in dict.Keys)
            {
                result[(string)key] = Parse(dict[key], numberAsDecimal);
            }
            return _oc.ConvertParsedResult(result, origTypeName);
        }
        else if (x is IEnumerable enumerable)
        {
            var result = new List<object?>();
            IEnumerator e = enumerable.GetEnumerator();
            while (e.MoveNext())
            {
                object? o = e.Current;
                result.Add(Parse(o, numberAsDecimal));
            }
            ((IDisposable)e).Dispose();
            return _oc.ConvertParsedResult(result, origTypeName);
        }
        else
        {
            var result = new Dictionary<string, object?>();
            FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            for (int i = 0; i < fieldInfos.Length; i++)
            {
                if (fieldInfos[i].IsDefined(typeof(IgnoreDataMemberAttribute), true))
                    continue;
                object? value = fieldInfos[i].GetValue(x);
                result[ObjectParserUtil.GetMemberName(fieldInfos[i])] = Parse(value);
            }
            PropertyInfo[] propertyInfo = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            for (int i = 0; i < propertyInfo.Length; i++)
            {
                if (!propertyInfo[i].CanRead || propertyInfo[i].IsDefined(typeof(IgnoreDataMemberAttribute), true))
                    continue;
                object? value = propertyInfo[i].GetValue(x, null);
                result[ObjectParserUtil.GetMemberName(propertyInfo[i])] = Parse(value);
            }
            return _oc.ConvertParsedResult(result, origTypeName);
        }
    }
    // ReSharper disable once MemberCanBePrivate.Global
    public string Stringify(object x, bool indent, bool sortKeys = false)
    {
        StringBuilder sb = new StringBuilder();
        new JsonStringBuilder(this._forceAscii, indent, sortKeys).WriteToSb(sb, x, 0);
        string json = sb.ToString();
        return json;
    }
}

internal class JsonStringBuilder
{
    private readonly bool _forceAscii /*= false*/;
    private readonly bool _indentJson /*= false*/;
    private readonly bool _sortKeys /*= false*/;
    public JsonStringBuilder(bool forceAscii, bool indentJson, bool sortKeys)
    {
        this._forceAscii = forceAscii;
        this._indentJson = indentJson;
        this._sortKeys = sortKeys;
    }

    private void Indent(StringBuilder sb, int level)
    {
        if (this._indentJson)
        {
            for (int i = 0; i < level; i++)
            {
                sb.Append("  ");
            }
        }
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public static Type? GetGenericIDictionaryType(Type? type)
    {
        if (type == null) return null;
        var ifs = type.GetInterfaces();
        foreach (var i in ifs)
        {
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                return i;
            }
        }
        return null;
    }

    private void WriteProcessGenericIDictionaryToSb<T>(StringBuilder sb, IDictionary<string, T> dict/*, bool indent*/, int level)
    {
        sb.Append("{");
        int count = 0;
        var keys = from a in dict.Keys select a;
        if (this._sortKeys)
            keys = from a in dict.Keys orderby a select a;
        foreach (string key in keys)
        {
            if (count == 0 && this._indentJson) sb.Append('\n');
            if (count > 0)
            {
                sb.Append(",");
                if (this._indentJson) sb.Append('\n');
            }
            WriteToSb(sb, (string)key, level + 1);
            sb.Append(this._indentJson ? ": " : ":");
            WriteToSb(sb, dict[key]!, level + 1, true);
            count++;
        }
        if (count > 0 && this._indentJson)
        {
            sb.Append('\n');
            Indent(sb, level);
        }
        sb.Append("}");
    }
    public void WriteToSb(StringBuilder sb, object? x, int level, bool cancelIndent = false)
    {
        if (!cancelIndent) Indent(sb, level);

        if (x == null)
        {
            sb.Append("null");
            return;
        }

        if (x is IExportToPlainObject exportableObject)
        {
            x = exportableObject.ExportToPlainObject();
        }

        Type type = x!.GetType();
        if (type == typeof(string) || type == typeof(char))
        {
            string str = x.ToString()!;
            sb.Append('"');
            sb.Append(Escape(str));
            sb.Append('"');
            return;
        }
        if (type == typeof(byte) || type == typeof(sbyte)
            || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint)
            || type == typeof(long) || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal))
        {
            sb.Append(x/*.ToString()*/);
            // ReSharper disable once RedundantJumpStatement
            return;
        }
        else if (type == typeof(bool))
        {
            sb.Append(x.ToString()!.ToLower());
            // ReSharper disable once RedundantJumpStatement
            return;
        }
        else if (type == typeof(DateTime))
        {
            DateTime dt = (DateTime)x;
            switch(dt.Kind)
            {
                case DateTimeKind.Local:
                    WriteToSb(sb, dt.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffffffzzz"), level, cancelIndent);
                    break;
                case DateTimeKind.Utc:
                    WriteToSb(sb, dt.ToString("o"), level, cancelIndent);
                    break;
                default: //case DateTimeKind.Unspecified:
                    WriteToSb(sb, dt.ToString("o").Replace("Z", ""), level, cancelIndent);
                    break;
            }
            // ReSharper disable once RedundantJumpStatement
            return;
        }
        else if (type == typeof(TimeSpan))
        {
            WriteToSb(sb, x.ToString(), level, cancelIndent);
            // ReSharper disable once RedundantJumpStatement
            return;
        }
        else if (type == typeof(Guid))
        {
            WriteToSb(sb, x.ToString(), level, cancelIndent);
            // ReSharper disable once RedundantJumpStatement
            return;
        }
        else if (type.IsEnum)
        {
            WriteToSb(sb, x.ToString(), level, cancelIndent);
            // ReSharper disable once RedundantJumpStatement
            return;
        }
        else if (x is ExpandoObject)
        {
            var dic = x as IDictionary<string, object>;
            var result = new Dictionary<string, object>();
            foreach (var key in dic!.Keys)
            {
                result[key] = dic[key];
            }
            WriteToSb(sb, result, level, cancelIndent);
            // ReSharper disable once RedundantJumpStatement
            return;
        }
        else if (x is IList)
        {
            IList list = (x as IList)!;
            if (list.Count == 0)
            {
                sb.Append("[]");
                return;
            }
            sb.Append('[');
            if (this._indentJson) sb.Append('\n');
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(",");
                    if (this._indentJson) sb.Append('\n');
                }
                WriteToSb(sb, list[i], level + 1);
            }
            if (this._indentJson) sb.Append('\n');
            Indent(sb, level);
            sb.Append(']');
            // ReSharper disable once RedundantJumpStatement
            return;
        }
        else if (x is Hashtable)
        {
            Hashtable ht = (x as Hashtable)!;
            sb.Append("{");
            int count = 0;
            var keys = new List<object>();
            foreach (object key in ht.Keys)
            {
                keys.Add(key);
            }
            keys = keys.Where(k => k is string).ToList();
            if (this._sortKeys)
                keys = keys.OrderBy(k => k as string).ToList();
            foreach (object key in keys/*ht.Keys*/)
            {
                if (count == 0 && this._indentJson) sb.Append('\n');
                if (count > 0)
                {
                    sb.Append(",");
                    if (this._indentJson) sb.Append('\n');
                }
                WriteToSb(sb, (string)key, level + 1);
                sb.Append(this._indentJson ? ": " : ":");
                WriteToSb(sb, ht[key], level + 1, true);
                count++;
            }
            if (count > 0 && this._indentJson)
            {
                sb.Append('\n');
                Indent(sb, level);
            }
            sb.Append("}");
        }
        else if (GetGenericIDictionaryType(type) != null)
        {
            type = GetGenericIDictionaryType(type)!;
            Type keyType = type.GetGenericArguments()[0];
            //Refuse to output dictionary keys that aren't of type string
            if (keyType != typeof(string))
            {
                sb.Append("{}");
                return;
            }
            WriteProcessGenericIDictionaryToSb(sb, (dynamic)x, level);
            // ReSharper disable once RedundantJumpStatement
            return;
        }
        else if (x is IEnumerable)
        {
            IEnumerable enumerable = (IEnumerable)x;
            var result = new List<object>();
            IEnumerator e = enumerable.GetEnumerator();
            while (e.MoveNext())
            {
                object? o = e.Current;
                result.Add(o);
            }
            ((IDisposable)e).Dispose();
            WriteToSb(sb, result, level, cancelIndent);
        }
        else
        {
            int count = 0;
            sb.Append('{');
            FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            for (int i = 0; i < fieldInfos.Length; i++)
            {
                if (fieldInfos[i].IsDefined(typeof(IgnoreDataMemberAttribute), true))
                    continue;
                object? value = fieldInfos[i].GetValue(x);
                // if (x is RedundantObject)
                // {
                //     if (value == null) continue;
                // }
                if (count == 0 && this._indentJson) sb.Append('\n');
                if (count > 0)
                {
                    sb.Append(",");
                    if (this._indentJson) sb.Append('\n');
                }
                WriteToSb(sb, ObjectParserUtil.GetMemberName(fieldInfos[i]), level + 1);
                sb.Append(this._indentJson ? ": " : ":");
                WriteToSb(sb, value, level + 1, true);
                count++;
            }
            PropertyInfo[] propertyInfo = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            for (int i = 0; i < propertyInfo.Length; i++)
            {
                if (!propertyInfo[i].CanRead || propertyInfo[i].IsDefined(typeof(IgnoreDataMemberAttribute), true))
                    continue;
                object? value = propertyInfo[i].GetValue(x, null);
                // if (x is RedundantObject)
                // {
                //     if (value == null) continue;
                // }
                if (count == 0 && this._indentJson) sb.Append('\n');
                if (count > 0)
                {
                    sb.Append(",");
                    if (this._indentJson) sb.Append('\n');
                }
                WriteToSb(sb, ObjectParserUtil.GetMemberName(propertyInfo[i]), level + 1);
                sb.Append(this._indentJson ? ": " : ":");
                WriteToSb(sb, value, level + 1, true);
                count++;
            }
            if (count > 0 && this._indentJson)
            {
                sb.Append('\n');
                Indent(sb, level);
            }
            sb.Append('}');
            // ReSharper disable once RedundantJumpStatement
            return;
        }
    }

    private string Escape(string aText /*, bool ForceASCII*/)
    {
        var sb = new StringBuilder();
        sb.Length = 0;
        if (sb.Capacity < aText.Length + aText.Length / 10)
            sb.Capacity = aText.Length + aText.Length / 10;
        foreach (char c in aText)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                default:
                    if (c < ' ' || (_forceAscii && c > 127))
                    {
                        ushort val = c;
                        sb.Append("\\u").Append(val.ToString("X4"));
                    }
                    else
                        sb.Append(c);
                    break;
            }
        }
        string result = sb.ToString();
        sb.Length = 0;
        return result;
    }
}