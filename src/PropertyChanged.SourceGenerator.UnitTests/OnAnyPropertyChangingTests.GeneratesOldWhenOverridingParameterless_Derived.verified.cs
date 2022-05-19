partial class Derived
{
    public int Foo { get; set; }
    protected override void OnPropertyChanging(global::System.ComponentModel.PropertyChangingEventArgs eventArgs)
    {
        this.OnAnyPropertyChanging(eventArgs.PropertyName, (object)null);
        base.OnPropertyChanging(eventArgs);
    }
}