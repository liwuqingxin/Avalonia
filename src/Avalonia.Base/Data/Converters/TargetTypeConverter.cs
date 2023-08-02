using System;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

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
    
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => true;
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => true;

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return Convert(value, culture, TargetType);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        return Convert(value, culture, destinationType);
    }

    private static object? Convert(object? value, CultureInfo? culture, Type type)
    {
        if (value is null)
            return null;
        if (type.IsAssignableFrom(value.GetType()))
            return value;
        if (value is IConvertible convertible)
            return convertible.ToType(type, culture);
        if (type == typeof(string))
            return value.ToString();
        return new BindingNotification(
            new InvalidCastException($"Cannot convert '{value}' to '{type}'."),
            BindingErrorType.Error);
    }
}
