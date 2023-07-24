using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Avalonia.Data.Converters;
using Avalonia.Data.Core.ExpressionNodes;
using Avalonia.Data.Core.Parsers;

namespace Avalonia.Data.Core;

/// <summary>
/// A binding expression which accepts and produces (possibly boxed) object values.
/// </summary>
/// <remarks>
/// A <see cref="UntypedBindingExpression"/> represents a untyped binding which has been
/// instantiated on an object.
/// </remarks>
internal class UntypedBindingExpression : IObservable<object?>,
    IObserver<object?>,
    IDescription,
    IDisposable
{
    private const string NullValueMessage = "Value is null.";
    private readonly WeakReference<object?>? _source;
    private readonly IReadOnlyList<ExpressionNode> _nodes;
    private readonly TargetTypeConverter? _targetTypeConverter;
    private readonly bool _enableDataValidation;
    private IObserver<object?>? _observer;
    private UncommonFields? _uncommon;

    /// <summary>
    /// Initializes a new instance of the <see cref="UntypedBindingExpression"/> class.
    /// </summary>
    /// <param name="source">The source from which the value will be read.</param>
    /// <param name="nodes">The nodes representing the binding path.</param>
    /// <param name="fallbackValue">
    /// The fallback value. Pass <see cref="AvaloniaProperty.UnsetValue"/> for no fallback.
    /// </param>
    /// <param name="converter">The converter to use.</param>
    /// <param name="converterParameter">The converter parameter.</param>
    /// <param name="enableDataValidation">
    /// Whether data validation should be enabled for the binding.
    /// </param>
    /// <param name="stringFormat">The format string to use.</param>
    /// <param name="targetNullValue">The null target value.</param>
    /// <param name="targetTypeConverter">
    /// A final type converter to be run on the produced value.
    /// </param>
    public UntypedBindingExpression(
        object? source,
        IReadOnlyList<ExpressionNode> nodes,
        object? fallbackValue,
        IValueConverter? converter = null,
        object? converterParameter = null,
        bool enableDataValidation = false,
        string? stringFormat = null,
        object? targetNullValue = null,
        TargetTypeConverter? targetTypeConverter = null)
    {
        if (source == AvaloniaProperty.UnsetValue)
            source = null;

        _source = new(source);
        _nodes = nodes;
        _targetTypeConverter = targetTypeConverter;
        _enableDataValidation = enableDataValidation;

        if (converter is not null ||
            converterParameter is not null ||
            fallbackValue != AvaloniaProperty.UnsetValue ||
            (targetNullValue is not null && targetNullValue != AvaloniaProperty.UnsetValue) ||
            !string.IsNullOrWhiteSpace(stringFormat))
        {
            _uncommon = new()
            {
                _converter = converter,
                _converterParameter = converterParameter,
                _fallbackValue = fallbackValue,
                _targetNullValue = targetNullValue ?? AvaloniaProperty.UnsetValue,
                _stringFormat = stringFormat switch
                {
                    string s when string.IsNullOrWhiteSpace(s) => null,
                    string s when !s.Contains('{') => $"{{0:{stringFormat}}}",
                    _ => stringFormat,
                },
            };
        }

        for (var i = 0; i < nodes.Count; ++i)
        {
            var node = nodes[i];
            node.SetOwner(this, i);

            if (enableDataValidation && i == nodes.Count - 1 && node is IPropertyAccessorNode leaf)
                leaf.EnableDataValidation();
        }
    }

    public string Description
    {
        get
        {
            var b = new StringBuilder();
            foreach (var node in _nodes)
                node.BuildString(b);
            return b.ToString();
        }
    }

    private Type TargetType => _targetTypeConverter?.TargetType ?? typeof(object);
    private IValueConverter? Converter => _uncommon?._converter;
    private object? ConverterParameter => _uncommon?._converterParameter;
    private object? FallbackValue => _uncommon is not null ? _uncommon._fallbackValue : AvaloniaProperty.UnsetValue;
    private object? TargetNullValue => _uncommon?._targetNullValue ?? AvaloniaProperty.UnsetValue;
    private ExpressionNode LeafNode => _nodes[_nodes.Count - 1];
    private string? StringFormat => _uncommon?._stringFormat;

    /// <summary>
    /// Writes the specified value to the binding source if possible.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <returns>
    /// True if the value could be written to the binding source; otherwise false.
    /// </returns>
    public bool SetValue(object? value)
    {
        if (_nodes.Count == 0)
            return false;

        if (Converter is not null)
            value = Converter.ConvertBack(value, TargetType, ConverterParameter, CultureInfo.CurrentCulture);

        if (value == BindingOperations.DoNothing)
            return true;

        try
        {
            return LeafNode.WriteValueToSource(BindingNotification.ExtractValue(value), _nodes);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates an <see cref="UntypedBindingExpression"/> from an expression tree.
    /// </summary>
    /// <typeparam name="TIn">The input type of the binding expression.</typeparam>
    /// <typeparam name="TOut">The output type of the binding expression.</typeparam>
    /// <param name="source">The source from which the binding value will be read.</param>
    /// <param name="expression">The expression representing the binding path.</param>
    /// <param name="converter">The converter to use.</param>
    /// <param name="converterParameter">The converter parameter.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <param name="targetType">The target type to convert to.</param>
    /// <param name="enableDataValidation">Whether data validation should be enabled for the binding.</param>
    [RequiresUnreferencedCode(TrimmingMessages.ExpressionNodeRequiresUnreferencedCodeMessage)]
    public static UntypedBindingExpression Create<TIn, TOut>(
        TIn source,
        Expression<Func<TIn, TOut>> expression,
        IValueConverter? converter = null,
        object? converterParameter = null,
        Optional<object?> fallbackValue = default,
        Type? targetType = null,
        bool enableDataValidation = false)
            where TIn : class?
    {
        var nodes = UntypedBindingExpressionVisitor<TIn>.BuildNodes(expression, enableDataValidation);
        var fallback = fallbackValue.HasValue ? fallbackValue.Value : AvaloniaProperty.UnsetValue;
        var targetTypeConverter = targetType is not null ? new TargetTypeConverter(targetType) : null;

        return new UntypedBindingExpression(
            source,
            nodes,
            fallback,
            converter: converter,
            converterParameter: converterParameter,
            enableDataValidation: enableDataValidation,
            targetTypeConverter: targetTypeConverter);
    }

    /// <summary>
    /// Implements the disposable returned by <see cref="IObservable{T}.Subscribe(IObserver{T})"/>.
    /// </summary>
    void IDisposable.Dispose()
    {
        if (_observer is null)
            return;
        _observer = null;
        Stop();
    }

    IDisposable IObservable<object?>.Subscribe(IObserver<object?> observer)
    {
        if (_observer is not null)
            throw new InvalidOperationException(
                $"An {nameof(UntypedBindingExpression)} may only have a single subscriber.");

        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        Start();
        return this;
    }

    void IObserver<object?>.OnCompleted() { }
    void IObserver<object?>.OnError(Exception error) { }
    void IObserver<object?>.OnNext(object? value) => SetValue(value);

    /// <summary>
    /// Called by an <see cref="ExpressionNode"/> belonging to this binding when its
    /// <see cref="ExpressionNode.Value"/> changes.
    /// </summary>
    /// <param name="nodeIndex">The <see cref="ExpressionNode.Index"/>.</param>
    /// <param name="value">The <see cref="ExpressionNode.Value"/>.</param>
    internal void OnNodeValueChanged(int nodeIndex, object? value)
    {
        if (nodeIndex == _nodes.Count - 1)
            PublishValue();
        else if (value is null)
            OnNodeError(nodeIndex, NullValueMessage);
        else
            _nodes[nodeIndex + 1].SetSource(value);
    }

    /// <summary>
    /// Called by an <see cref="ExpressionNode"/> belonging to this binding when an error occurs
    /// reading its value.
    /// </summary>
    /// <param name="nodeIndex">
    /// The <see cref="ExpressionNode.Index"/> or -1 if the source is null.
    /// </param>
    /// <param name="error">The error message.</param>
    internal void OnNodeError(int nodeIndex, string error)
    {
        // Set the source of all nodes after the one that errored to null. This needs to be done
        // for each node individually because setting the source to null will not result in
        // OnNodeValueChanged or OnNodeError being called.
        for (var i = nodeIndex + 1; i < _nodes.Count; ++i)
            _nodes[i].SetSource(null);

        if (_observer is null)
            return;

        // Build a string describing the binding chain up to the node that errored.
        var errorPoint = new StringBuilder();
        for (var i = 0; i <= nodeIndex; ++i)
            _nodes[i].BuildString(errorPoint);

        var e = new BindingChainException(error, Description, errorPoint.ToString());
        _observer?.OnNext(new BindingNotification(e, BindingErrorType.Error, FallbackValue));
    }

    private void Start()
    {
        if (_observer is null)
            return;

        if (_source?.TryGetTarget(out var source) == true)
        {
            if (_nodes.Count > 0)
                _nodes[0].SetSource(source);
            else
                _observer.OnNext(source);
        }
        else
        {
            OnNodeError(-1, NullValueMessage);
        }
    }

    private void Stop()
    {
        foreach (var node in _nodes)
            node.Reset();
    }

    private void PublishValue()
    {
        if (_observer is null)
            return;

        // The value can be a simple value or a BindingNotification. As we move through this method
        // we'll keep `notification` updated with the value and current error state by calling
        // `UpdateAndUnwrap`.
        var valueOrNotification = _nodes.Count > 0 ? _nodes[_nodes.Count - 1].Value : null;
        var value = BindingNotification.ExtractValue(valueOrNotification);
        var notification = valueOrNotification as BindingNotification;
        var isFallback = false;

        // All values other than DoNothing should be passed to the converter.
        if (value != BindingOperations.DoNothing && Converter is { } converter)
        {
            value = UpdateAndUnwrap(
                converter.Convert(
                    value,
                    _targetTypeConverter?.TargetType ?? typeof(object),
                    ConverterParameter,
                    CultureInfo.InvariantCulture),
                ref notification);
        }

        // Check this here as the converter may return DoNothing.
        if (value == BindingOperations.DoNothing)
            return;

        // TargetNullValue only applies when the value is null: UnsetValue indicates that there
        // was a binding error so we don't want to use TargetNullValue in that case.
        if (value is null && TargetNullValue != AvaloniaProperty.UnsetValue)
        {
            value = TargetNullValue;
            isFallback = true;
        }

        // If we have a value, try to convert it to the target type.
        if (value != AvaloniaProperty.UnsetValue)
        {
            if (StringFormat is { } stringFormat &&
                (TargetType == typeof(object) || TargetType == typeof(string)) &&
                !isFallback)
            {
                // The string format applies we're targeting a type that can accept a string
                // and the value isn't the TargetNullValue.
                value = string.Format(CultureInfo.CurrentCulture, stringFormat, value);
            }
            else if (_targetTypeConverter is not null && value is not null)
            {
                // Otherwise, if we have a target type converter, convert the value to the target type.
                value = UpdateAndUnwrap(_targetTypeConverter.ConvertFrom(value), ref notification);
            }
        }

        // FallbackValue applies if the result from the binding, converter or target type converter
        // is UnsetValue.
        if (value == AvaloniaProperty.UnsetValue && FallbackValue != AvaloniaProperty.UnsetValue)
        {
            value = FallbackValue;

            // If we have a target type converter, convert the fallback value to the target type.
            if (_targetTypeConverter is not null && value is not null)
                value = UpdateAndUnwrap(_targetTypeConverter.ConvertFrom(value), ref notification);
        }

        // Publish the notification/value to the observer.
        _observer.OnNext(notification ?? value);
    }

    private void PublishDataValidationError(Exception error)
    {
        _observer?.OnNext(new BindingNotification(
            error,
            BindingErrorType.DataValidationError,
            FallbackValue));
    }

    private void OnSourceChanged(object? source)
    {
        if (_nodes.Count > 0)
            _nodes[0].SetSource(source);
    }

    private static object? UpdateAndUnwrap(object? value, ref BindingNotification? notification)
    {
        if (value is BindingNotification n)
        {
            value = n.Value;

            if (n.Error is not null)
            {
                if (notification is null)
                    notification = n;
                else
                    notification.AddError(n.Error, n.ErrorType);
            }
        }
        else
        {
            notification?.SetValue(value);
        }

        return value;
    }

    private class UncommonFields
    {
        public IValueConverter? _converter;
        public object? _converterParameter;
        public object? _fallbackValue;
        public string? _stringFormat;
        public object? _targetNullValue;
    }
}
