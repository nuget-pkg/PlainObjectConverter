# if true
namespace Global;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;

#if GLOBAL_POC
public
#else
internal
#endif
class PlainObjectConverter: IConvertParsedResult
{
    public object? ConvertParsedResult(object? x, string origTypeName) // IConvertParsedResult
    {
        return x;
    }
    internal readonly IParseJson? JsonParser;
    private readonly bool _forceAscii;
    private readonly IConvertParsedResult _iConvertParsedResult;
    public PlainObjectConverter(IParseJson? jsonParser = null, bool forceAscii = false, /*bool removeSurrogatePair = false,*/ IConvertParsedResult? iConvertParsedResult = null)
    {
        this.JsonParser = jsonParser;
        this._forceAscii = forceAscii;
        if (iConvertParsedResult == null) iConvertParsedResult = this;
        this._iConvertParsedResult = iConvertParsedResult;
    }
    internal object? UnWrapOrExportToPlainObject(object? x)
    {
        if (x == null) return null;
        if (x is IExposeInternalObject)
        {
            x = ((IExposeInternalObject)x).ExposeInternalObject();
        }
        else if (x is IExportToPlainObject)
        {
            x = ((IExportToPlainObject)x).ExportToPlainObject();
        }
        else
        {
            try
            {
                Type type = x.GetType();
                MethodInfo? method = type.GetMethod("ExportToPlainObject");
                if (method != null)
                {
                    x = method.Invoke(x, []);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }
        if (this.JsonParser != null)
        {
            if (x is IExportToCommonJson)
            {
                x = JsonParser.ParseJson( ((IExportToCommonJson)x).ExportToCommonJson() );
            }
            else
            {
                try
                {
                    if (x != null)
                    {
                        Type type = x!.GetType();
                        MethodInfo? method = type.GetMethod("ExportToCommonJson");
                        if (method != null)
                        {
                            x = JsonParser.ParseJson((string)method.Invoke(x, [])!);
                        }
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
        return x;
    }
    public static bool IsValidSymbolName(string s)
    {
        Regex? r = null;
        Match? m = null;
        string pat = @"^[_a-zA-Z][_a-zA-Z0-9]*$";
        r = new Regex(pat);
        m = r.Match(s);
        return m.Success;
    }
    internal string GetMemberName(MemberInfo member)
    {
        if (member.IsDefined(typeof(DataMemberAttribute), true))
        {
            var dataMemberAttribute = (DataMemberAttribute)Attribute.GetCustomAttribute(member, typeof(DataMemberAttribute), true)!;
            if (!string.IsNullOrEmpty(dataMemberAttribute.Name!))
                return dataMemberAttribute.Name;
        }

        return member.Name;
    }
    public string ToPrintable(bool showDetail, object? x, string? title = null, bool noIndent = false, bool removeSurrogatePair = false)
    {
        //var po = Parse(x, numberAsDecimal: true);
        return ToPrintableHelper(showDetail, x, title, noIndent: noIndent, removeSurrogatePair: removeSurrogatePair, FullName(x));
    }
    public static string EscapeNonAsciiChars(string text)
    {
        var sb = new StringBuilder();
        sb.Length = 0;
        if (sb.Capacity < text.Length + text.Length / 10)
            sb.Capacity = text.Length + text.Length / 10;
        foreach (char c in text)
        {
            if (c > 127)
            {
                ushort val = c;
                sb.Append("\\u").Append(val.ToString("X4"));
            }
            else
            {
                sb.Append(c);
            }
        }
        string result = sb.ToString();
        sb.Length = 0;
        return result;
    }
    private string ToPrintableHelper(bool showDetail, object? x, string? title, bool noIndent, bool removeSurrogatePair, string? fullName = null)
    {
        if (fullName == null) fullName = FullName(x);
        PlainObjectConverter op = this;
        string s = "";
        if (title != null) s = title + ": ";
        if (x is null) return s + "null";
        if (x is string str)
        {
            if (_forceAscii) str = EscapeNonAsciiChars(str);
            if (!showDetail) return s + str;
            return s + "`" + str + "`";
        }
        string output /*= null*/;
        try
        {
            output = op.Stringify(x, indent: !noIndent, keyAsSymbol: true, removeSurrogatePair: removeSurrogatePair);
        }
        catch (Exception)
        {
            output = x.ToString()!;
        }
        if (!showDetail) return s + output;
        return s + $"<{fullName}> {output}";
    }
    public static string FullName(dynamic? x)
    {
        if (x is null) return "null";
        string fullName = ((object)x).GetType().FullName!;
        if (fullName.StartsWith("<>f__AnonymousType"))
        {
            return "AnonymousType";
        }
        fullName = fullName.Split('`')[0];
        if (x is IExposeInternalObject)
        {
            object? internalObject = ((IExposeInternalObject)x).ExposeInternalObject();
            fullName = $"{fullName}({FullName(internalObject)})";
        }
        return fullName;
    }
    public object? Parse(object? x, bool numberAsDecimal = false)
    {
        string origTypeName = FullName(x);
        x = this.UnWrapOrExportToPlainObject(x);
        if (x == null)
        {
            return _iConvertParsedResult.ConvertParsedResult(null, origTypeName);
        }
        Type type = x.GetType();
        if (type == typeof(string) || type == typeof(char))
        {
            return _iConvertParsedResult.ConvertParsedResult(x.ToString(), origTypeName);
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
                return _iConvertParsedResult.ConvertParsedResult(Convert.ToDecimal(x), origTypeName);
            return _iConvertParsedResult.ConvertParsedResult(Convert.ToDouble(x), origTypeName);
        }
        else if (type == typeof(bool))
        {
            return _iConvertParsedResult.ConvertParsedResult(x, origTypeName);
        }
        else if (type == typeof(DateTime))
        {
            DateTime dt = (DateTime)x;
            switch (dt.Kind)
            {
                case DateTimeKind.Local:
                    return _iConvertParsedResult.ConvertParsedResult(dt.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffffffzzz"), origTypeName);
                case DateTimeKind.Utc:
                    return _iConvertParsedResult.ConvertParsedResult(dt.ToString("o"), origTypeName);
                default:
                    return _iConvertParsedResult.ConvertParsedResult(dt.ToString("o").Replace("Z", ""), origTypeName);
            }
        }
        else if (type == typeof(TimeSpan))
        {
            return _iConvertParsedResult.ConvertParsedResult(x.ToString(), origTypeName);
        }
        else if (type == typeof(Guid))
        {
            return _iConvertParsedResult.ConvertParsedResult(x.ToString(), origTypeName);
        }
        else if (type.IsEnum)
        {
            return _iConvertParsedResult.ConvertParsedResult(x.ToString(), origTypeName);
        }
        else if (x is ExpandoObject)
        {
            var dic = x as IDictionary<string, object?>;
            var result = new Dictionary<string, object?>();
            foreach (var key in dic!.Keys)
            {
                result[key] = Parse(dic[key], numberAsDecimal);
            }
            return _iConvertParsedResult.ConvertParsedResult(result, origTypeName);
        }
        else if (x is IList list)
        {
            var result = new List<object?>();
            for (int i = 0; i < list.Count; i++)
            {
                result.Add(Parse(list[i], numberAsDecimal));
            }
            return _iConvertParsedResult.ConvertParsedResult(result, origTypeName);
        }
        else if (x is Hashtable ht)
        {
            var result = new Dictionary<string, object?>();
            foreach (object key in ht.Keys)
            {
                if (!(key is string s)) continue;
                result.Add(s, Parse(ht[s], numberAsDecimal));
            }
            return _iConvertParsedResult.ConvertParsedResult(result, origTypeName);
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            Type keyType = type.GetGenericArguments()[0];
            var result = new Dictionary<string, object?>();
            //Refuse to output dictionary keys that aren't of type string
            if (keyType != typeof(string))
            {
                return _iConvertParsedResult.ConvertParsedResult(result, origTypeName);
            }
            IDictionary dict = (x as IDictionary)!;
            foreach (object key in dict.Keys)
            {
                result[(string)key] = Parse(dict[key], numberAsDecimal);
            }
            return _iConvertParsedResult.ConvertParsedResult(result, origTypeName);
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
            return _iConvertParsedResult.ConvertParsedResult(result, origTypeName);
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
                result[this.GetMemberName(fieldInfos[i])] = Parse(value);
            }
            PropertyInfo[] propertyInfo = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            for (int i = 0; i < propertyInfo.Length; i++)
            {
                if (!propertyInfo[i].CanRead || propertyInfo[i].IsDefined(typeof(IgnoreDataMemberAttribute), true))
                    continue;
                object? value = propertyInfo[i].GetValue(x, null);
                result[this.GetMemberName(propertyInfo[i])] = Parse(value);
            }
            return _iConvertParsedResult.ConvertParsedResult(result, origTypeName);
        }
    }
    public string Stringify(object? x, bool indent, bool sortKeys = false, bool keyAsSymbol = false, bool removeSurrogatePair = false)
    {
        var po = Parse(x, numberAsDecimal: true);
        StringBuilder sb = new StringBuilder();
        new JsonStringBuilder(this, forceAscii: this._forceAscii, indentJson: indent, sortKeys: sortKeys, keyAsSymbol: keyAsSymbol, removeSurrogatePair: removeSurrogatePair).WriteToSb(sb, po, 0);
        string json = sb.ToString();
        return json;
    }
}

internal class JsonStringBuilder
{
    private readonly PlainObjectConverter _poc;
    private readonly bool _forceAscii /*= false*/;
    private readonly bool _indentJson /*= false*/;
    private readonly bool _sortKeys /*= false*/;
    private readonly bool _keyAsSymbol;
    private readonly bool _removeSurrogatePair;
    public JsonStringBuilder(PlainObjectConverter poc, bool forceAscii, bool indentJson, bool sortKeys, bool keyAsSymbol, bool removeSurrogatePair = false)
    {
        this._poc = poc;
        this._forceAscii = forceAscii;
        this._indentJson = indentJson;
        this._sortKeys = sortKeys;
        this._keyAsSymbol = keyAsSymbol;
        this._removeSurrogatePair = removeSurrogatePair;
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
            WriteToSb(sb, (string)key, level + 1, noQuoteKey: this._keyAsSymbol);
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
    public void WriteToSb(StringBuilder sb, object? x, int level, bool cancelIndent = false, bool noQuoteKey = false)
    {
        if (!cancelIndent) Indent(sb, level);

        if (x == null)
        {
            sb.Append("null");
            return;
        }

        Type type = x!.GetType();

        if (x is IExportToPlainObject exportableObject)
        {
            x = exportableObject.ExportToPlainObject();
        }
        else
        {
            try
            {
                //Type type = x.GetType();
                MethodInfo? method = type.GetMethod("ExportToPlainObject");
                if (method != null)
                {
                    x = method.Invoke(x, []);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        if (x == null)
        {
            sb.Append("null");
            return;
        }

        type = x!.GetType();

        if (type == typeof(string) || type == typeof(char))
        {
            string str = x.ToString()!;
            if (_removeSurrogatePair)
            {
                str = Regex.Replace(str, @"[\uD800-\uDFFF]", "{ddbea68e-d93f-4e85-92b5-83b1ace6d50f}");
                str = str.Replace("{ddbea68e-d93f-4e85-92b5-83b1ace6d50f}{ddbea68e-d93f-4e85-92b5-83b1ace6d50f}", "★");
                str = str.Replace("{ddbea68e-d93f-4e85-92b5-83b1ace6d50f}", "★");
            }
            if (noQuoteKey)
            {
                if (PlainObjectConverter.IsValidSymbolName(str))
                {
                    sb.Append(str);
                    return;
                }
            }
            sb.Append('"');
            sb.Append(Escape(str));
            sb.Append('"');
            return;
        }
        if (type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double) || type == typeof(Double)
            || type == typeof(decimal)
            )
        {
            sb.Append(x/*.ToString()*/);
            return;
        }
        else if (type == typeof(bool))
        {
            sb.Append(x.ToString()!.ToLower());
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
            return;
        }
        else if (type == typeof(TimeSpan))
        {
            WriteToSb(sb, x.ToString(), level, cancelIndent);
            return;
        }
        else if (type == typeof(Guid))
        {
            WriteToSb(sb, x.ToString(), level, cancelIndent);
            return;
        }
        else if (type.IsEnum)
        {
            WriteToSb(sb, x.ToString(), level, cancelIndent);
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
                WriteToSb(sb, (string)key, level + 1, noQuoteKey: this._keyAsSymbol);
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
                if (count == 0 && this._indentJson) sb.Append('\n');
                if (count > 0)
                {
                    sb.Append(",");
                    if (this._indentJson) sb.Append('\n');
                }
                WriteToSb(sb, this._poc.GetMemberName(fieldInfos[i]), level + 1);
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
                if (count == 0 && this._indentJson) sb.Append('\n');
                if (count > 0)
                {
                    sb.Append(",");
                    if (this._indentJson) sb.Append('\n');
                }
                WriteToSb(sb, this._poc.GetMemberName(propertyInfo[i]), level + 1);
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
            return;
        }
    }

    private string Escape(string text)
    {
        var sb = new StringBuilder();
        sb.Length = 0;
        if (sb.Capacity < text.Length + text.Length / 10)
            sb.Capacity = text.Length + text.Length / 10;
        foreach (char c in text)
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
#endif
