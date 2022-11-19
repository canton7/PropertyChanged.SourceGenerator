partial class SomeViewModel
{
    public event global::System.ComponentModel.PropertyChangingEventHandler PropertyChanging;
    public string Foo { get; set; }
    protected virtual void OnPropertyChanging(global::System.ComponentModel.PropertyChangingEventArgs eventArgs, object oldValue)
    {
        this.OnAnyPropertyChanging(eventArgs.PropertyName, oldValue);
        this.PropertyChanging?.Invoke(this, eventArgs);
    }
}