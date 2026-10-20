using System;

namespace Unidad.Core.Diagnostics;

/// <summary>
/// One section of the Alt+D debug window (the window is the adapter's), added with <c>AddMany&lt;IDebugSection&gt;</c> by an
/// installer in this assembly. Sections are shown by <see cref="Order"/>.
/// </summary>
public interface IDebugSection
{
    string Title { get; }
    int Order { get; }

    /// <summary>Declares the section's rows on <paramref name="ui"/>; called once when the window builds it.</summary>
    void Build(IDebugUi ui);
}

/// <summary>The rows a debug section can declare; the debug window decides how they look.</summary>
public interface IDebugUi
{
    void Button(string text, Action action);

    /// <summary>A label and a value read again while the window is open.</summary>
    void Readout(string label, Func<string> value);

    void IntField(string label, int value, Action<int> set);

    /// <summary>An int field that shows <paramref name="value"/> again while the window is open (a verb can change it).</summary>
    void IntField(string label, Func<int> value, Action<int> set) => IntField(label, value(), set);
    void TextField(string label, string value, Action<string> set);
    void Choice(string label, string[] options, Action<int> pick);
    void Separator();
}
