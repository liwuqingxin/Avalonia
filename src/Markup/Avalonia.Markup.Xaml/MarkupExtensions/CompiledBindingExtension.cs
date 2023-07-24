using System;
using System.Collections.Generic;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Data.Core;
using Avalonia.Data.Core.ExpressionNodes;
using Avalonia.Markup.Parsers;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;

namespace Avalonia.Markup.Xaml.MarkupExtensions
{
    public class CompiledBindingExtension : BindingBase
    {
        public CompiledBindingExtension()
        {
            Path = new CompiledBindingPath();
        }

        public CompiledBindingExtension(CompiledBindingPath path)
        {
            Path = path;
        }

        public CompiledBindingExtension ProvideValue(IServiceProvider provider)
        {
            return new CompiledBindingExtension
            {
                Path = Path,
                Converter = Converter,
                ConverterParameter = ConverterParameter,
                TargetNullValue = TargetNullValue,
                FallbackValue = FallbackValue,
                Mode = Mode,
                Priority = Priority,
                StringFormat = StringFormat,
                Source = Source,
                DefaultAnchor = new WeakReference(provider.GetDefaultAnchor())
            };
        }

        public override InstancedBinding? Initiate(
            AvaloniaObject target,
            AvaloniaProperty? targetProperty,
            object? anchor = null,
            bool enableDataValidation = false)
        {
            var nodes = new List<ExpressionNode>();

            // Build the expression nodes from the binding path.
            Path.BuildExpression(nodes, out var isRooted);

            // If the binding isn't rooted (i.e. doesn't have a Source or start with $parent, $self,
            // #elementName etc.) then we need to add a data context source node.
            if (Source is null && !isRooted)
                nodes.Insert(0, ExpressionNodeFactory.CreateDataContext(targetProperty));

            // If the first node is an ISourceNode then allow it to select the source; otherwise
            // use the binding source if specified, falling back to the target.
            var source = nodes.Count > 0 && nodes[0] is ISourceNode sn
                ? sn.SelectSource(Source, target, anchor ?? DefaultAnchor?.Target)
                : Source ?? target;

            // Create the binding expression and wrap it in an InstancedBinding.
            var expression = new UntypedBindingExpression(
                source,
                nodes,
                FallbackValue,
                converter: Converter,
                converterParameter: ConverterParameter,
                enableDataValidation: enableDataValidation,
                stringFormat: StringFormat,
                targetNullValue: TargetNullValue,
                targetTypeConverter: TargetTypeConverter.Create(targetProperty));

            return new InstancedBinding(expression, Mode, Priority);
        }

        [ConstructorArgument("path")]
        public CompiledBindingPath Path { get; set; }

        public object? Source { get; set; }

        public Type? DataType { get; set; }
    }
}
