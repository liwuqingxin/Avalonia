using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Avalonia.Data.Core.ExpressionNodes;

internal class LogicalNotNode : ExpressionNode, ISettableNode
{
    public override void BuildString(StringBuilder builder)
    {
        builder.Append("!");
    }

    public Type ValueType => typeof(bool);

    public bool WriteValueToSource(object? value, IReadOnlyList<ExpressionNode> nodes)
    {
        if (Index > 0 && nodes[Index - 1] is ISettableNode previousNode && TryConvert(value, out var boolValue))
            return previousNode.WriteValueToSource(!boolValue, nodes);
        return false;
    }

    protected override void OnSourceChanged(object source)
    {
        if (TryConvert(source, out var value))
            SetValue(!value);
        else
            SetError(new InvalidCastException($"Unable to convert '{source}' to bool."));
    }

    private static bool TryConvert(object? value, out bool result)
    {
        if (value is bool b)
        {
            result = b;
            return true;
        }
        if (value is string s)
        {
            // Special case string for performance.
            if (bool.TryParse(s, out result))
                return true;
        }
        else
        {
            try
            {
                result = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch { }
        }

        result = false;
        return false;
    }
}
