#pragma warning disable CS0649, CS0169, CS0414
namespace ApiMark.DotNet.Fixtures.Modifiers;

/// <summary>A base type declaring one member of every hideable kind and accessibility.</summary>
public class NewBase
{
    /// <summary>A constant.</summary>
    public const int Konst = 1;

    /// <summary>A public field.</summary>
    public int Value;

    /// <summary>A public property.</summary>
    public int Prop { get; set; }

    /// <summary>A public method.</summary>
    public void Run()
    {
    }

    /// <summary>A public event.</summary>
    public event EventHandler? Evt;

    /// <summary>A protected field.</summary>
    protected int Prot;

    /// <summary>An internal field.</summary>
    internal int Inner;

    /// <summary>A private field that nothing can hide.</summary>
#pragma warning disable CS0169
    private int hidden;
#pragma warning restore CS0169

    /// <summary>A field that a derived method will hide.</summary>
    public int FieldHiddenByMethod;

    /// <summary>A method that a derived property will hide.</summary>
    public void MethodHiddenByProperty()
    {
    }

    /// <summary>Raises the event so the field is used.</summary>
    protected void RaiseEvt() => Evt?.Invoke(this, EventArgs.Empty);
}

/// <summary>A derived type hiding base members of every kind, plus unrelated members that must not carry <c>new</c>.</summary>
public class NewDerived : NewBase
{
    /// <summary>Hides a constant.</summary>
    public new const int Konst = 2;

    /// <summary>Hides a field with a field.</summary>
    public new int Value;

    /// <summary>Hides a property with a field.</summary>
    public new string Prop = string.Empty;

    /// <summary>Hides a method with a field.</summary>
    public new int Run;

    /// <summary>Hides an event with a field.</summary>
    public new int Evt;

    /// <summary>Hides a protected field with a static field.</summary>
    public new static int Prot;

    /// <summary>Hides an internal field (same assembly, so it is accessible).</summary>
    public new int Inner;

    /// <summary>Has the same name as a private base field, which is not hidden.</summary>
    public int hidden;

    /// <summary>Hides a field with a method.</summary>
    public new void FieldHiddenByMethod()
    {
    }

    /// <summary>Hides a method with a property.</summary>
    public new int MethodHiddenByProperty { get; set; }

    /// <summary>A member with no base counterpart.</summary>
    public int Unrelated;
}
