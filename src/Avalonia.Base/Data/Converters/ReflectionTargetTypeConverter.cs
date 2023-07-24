using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Avalonia.Data.Converters;

[RequiresUnreferencedCode(TrimmingMessages.TypeConversionRequiresUnreferencedCodeMessage)]
internal class ReflectionTargetTypeConverter : TargetTypeConverter
{
    public ReflectionTargetTypeConverter(Type targetType) : base(targetType) { }

    public new static ReflectionTargetTypeConverter? Create(AvaloniaProperty? targetProperty)
    {
        if (targetProperty is null)
            return null;
        return new ReflectionTargetTypeConverter(targetProperty.PropertyType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return DefaultValueConverter.Instance.Convert(value, TargetType, null, CultureInfo.InvariantCulture);
    }
}
