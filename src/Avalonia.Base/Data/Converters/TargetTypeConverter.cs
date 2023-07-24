using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Avalonia.Data.Converters;

internal class TargetTypeConverter : TypeConverter
{
    public TargetTypeConverter(Type targetType) => TargetType = targetType;

    public Type TargetType { get; }

    public static TargetTypeConverter? Create(AvaloniaProperty? targetProperty)
    {
        if (targetProperty is null)
            return null;
        return new TargetTypeConverter(targetProperty.PropertyType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        // We may want to generate code to do this in order to avoid reflection.
        if (TargetType.IsAssignableFrom(value.GetType()))
            return value;
        if (value is IConvertible convertible)
            return convertible.ToType(TargetType, culture);
        if (TargetType == typeof(string))
            return value.ToString();
        return new BindingNotification(
            new InvalidCastException($"Cannot convert '{value}' to '{TargetType.Name}'."),
            BindingErrorType.Error);
    }
}
