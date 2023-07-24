using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Avalonia.Data.Core.ExpressionNodes;

/// <summary>
/// A node in the binding path of an <see cref="UntypedBindingExpression"/>.
/// </summary>
internal abstract class ExpressionNode
{
    private WeakReference<object?>? _source;
    private WeakReference<object?>? _value;

    /// <summary>
    /// Gets the index of the node in the binding path.
    /// </summary>
    public int Index { get; private set; }

    /// <summary>
    /// Gets the owning <see cref="UntypedBindingExpression"/>.
    /// </summary>
    public UntypedBindingExpression? Owner { get; private set; }

    /// <summary>
    /// Gets the source object from which the node will read its value.
    /// </summary>
    public object? Source
    {
        get
        {
            if (_source?.TryGetTarget(out var source) == true)
                return source;
            return null;
        }
    }

    /// <summary>
    /// Gets the current value of the node.
    /// </summary>
    public object? Value
    {
        get
        {
            if (_value is null)
                return AvaloniaProperty.UnsetValue;
            _value.TryGetTarget(out var value);
            return value;
        }
    }

    /// <summary>
    /// Builds a string representation of the node.
    /// </summary>
    /// <param name="builder">The string builder.</param>
    public abstract void BuildString(StringBuilder builder);

    /// <summary>
    /// Resets the node to its uninitialized state when the <see cref="Owner"/> is unsubscribed.
    /// </summary>
    public void Reset()
    {
        SetSource(null);
        _source = _value = null;
    }

    /// <summary>
    /// Sets the owner binding.
    /// </summary>
    /// <param name="owner">The owner binding.</param>
    /// <param name="index">The index of the node in the binding path.</param>
    /// <exception cref="InvalidOperationException">
    /// The node already has an owner.
    /// </exception>
    public void SetOwner(UntypedBindingExpression owner, int index)
    {
        if (Owner is not null)
            throw new InvalidOperationException($"{this} already has an owner.");
        Owner = owner;
        Index = index;
    }

    /// <summary>
    /// Sets the <see cref="Source"/> from which the node will read its value and updates
    /// the current <see cref="Value"/>, notifying the <see cref="Owner"/> if the value
    /// changes.
    /// </summary>
    /// <param name="source">
    /// The new source from which the node will read its value. May be 
    /// <see cref="AvaloniaProperty.UnsetValue"/> in which case the source will be considered
    /// to be null.
    /// </param>
    public void SetSource(object? source)
    {
        var oldSource = Source;

        if (source == AvaloniaProperty.UnsetValue)
            source = null;

        if (oldSource is not null)
            Unsubscribe(oldSource);

        _source = new(source);

        if (source is null)
        {
            // If the source is null then the value is null. We explcitly do not want to call
            // OnSourceChanged as we don't want to raise errors for subsequent nodes in the
            // binding change.
            _value = new(null);
        }
        else if (source != oldSource)
        {
            try { OnSourceChanged(source); }
            catch (Exception e) { SetError(e); }
        }
    }

    /// <summary>
    /// Tries to write the specified value to the source.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <param name="nodes">The expression nodes in the binding.</param>
    /// <returns>True if the value was written sucessfully; otherwise false.</returns>
    public virtual bool WriteValueToSource(
        object? value,
        IReadOnlyList<ExpressionNode> nodes) => false;

    /// <summary>
    /// Sets the current value to <see cref="AvaloniaProperty.UnsetValue"/>.
    /// </summary>
    protected void ClearValue() => SetValue(AvaloniaProperty.UnsetValue);

    /// <summary>
    /// Sets the current value to <see cref="AvaloniaProperty.UnsetValue"/> and notifies the
    /// <see cref="Owner"/> of the error.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected void SetError(string message)
    {
        _value = new(AvaloniaProperty.UnsetValue);
        Owner?.OnNodeError(Index, message);
    }

    /// <summary>
    /// Sets the current value to <see cref="AvaloniaProperty.UnsetValue"/> and notifies the
    /// <see cref="Owner"/> of the error.
    /// </summary>
    /// <param name="e">The error.</param>
    protected void SetError(Exception e)
    {
        if (e is TargetInvocationException tie)
            e = tie.InnerException!;
        SetError(e.Message);
    }

    /// <summary>
    /// Sets the current <see cref="Value"/>, notifying the <see cref="Owner"/> if the value
    /// has changed.
    /// </summary>
    /// <param name="value">The new value.</param>
    protected void SetValue(object? value)
    {
        // We raise a change notification if:
        //
        // - This is the initial value (_value is null)
        // - The value is a binding notification
        // - The old value has been GC'd - in this case we don't know if the new value is different
        // - The new value is different to the old value
        if (_value is null ||
            value is BindingNotification ||
            _value.TryGetTarget(out var oldValue) == false ||
            !Equals(oldValue, value))
        {
            _value = new(value);
            Owner?.OnNodeValueChanged(Index, value);
        }
    }

    /// <summary>
    /// When implemented in a derived class, subscribes to the new source, and updates the current 
    /// <see cref="Value"/>.
    /// </summary>
    /// <param name="source">The new source.</param>
    protected abstract void OnSourceChanged(object source);

    /// <summary>
    /// When implemented in a derived class, unsubscribes from the previous source.
    /// </summary>
    /// <param name="oldSource">The old source.</param>
    protected virtual void Unsubscribe(object oldSource) { }
}
