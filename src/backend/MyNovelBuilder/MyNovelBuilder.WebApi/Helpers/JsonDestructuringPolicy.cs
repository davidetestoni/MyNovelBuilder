using System.Text.Json;
using Serilog.Core;
using Serilog.Events;

namespace MyNovelBuilder.WebApi.Helpers;

internal class JsonDestructuringPolicy : IDestructuringPolicy
{
    /// <inheritdoc/>
    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue result)
    {
        switch (value)
        {
            case JsonDocument jsonDocument:
                result = Destructure(jsonDocument.RootElement);
                return true;

            case JsonElement jsonElement:
                result = Destructure(jsonElement);
                return true;
        }

        result = propertyValueFactory.CreatePropertyValue(null);
        return false;
    }

    private static LogEventPropertyValue Destructure(in JsonElement jel)
    {
        return jel.ValueKind switch
        {
            JsonValueKind.Array => new SequenceValue(jel.EnumerateArray().Select(ae => Destructure(in ae))),
            JsonValueKind.False => new ScalarValue(false),
            JsonValueKind.True => new ScalarValue(true),
            JsonValueKind.Null or JsonValueKind.Undefined => new ScalarValue(null),
            JsonValueKind.Number => new ScalarValue(jel.GetDecimal()),
            JsonValueKind.String => new ScalarValue(jel.GetString()),
            JsonValueKind.Object => new StructureValue(jel.EnumerateObject().Select(jp => new LogEventProperty(jp.Name, Destructure(jp.Value)))),
            _ => throw new ArgumentException("Unrecognized value kind " + jel.ValueKind + "."),
        };
    }
}
