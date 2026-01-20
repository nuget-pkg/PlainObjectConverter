// ReSharper disable once CheckNamespace
namespace Global;

public interface IConvertParsedResult
{
    public object? ConvertParsedResult(object? x, string origTypeName);
}
