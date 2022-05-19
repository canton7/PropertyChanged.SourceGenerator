partial class Derived
{
    public int Foo { get; set; }
    protected override void OnPropertyChanging(global::System.ComponentModel.PropertyChangingEventArgs eventArgs, object oldValue)
    {
        this.OnAnyPropertyChanging(eventArgs.PropertyName, oldValue);
        base.OnPropertyChanging(eventArgs, oldValue);
    }
}