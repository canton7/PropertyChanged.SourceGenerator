partial class SomeViewModel
{
    /// <inheritdoc />
    public event global::System.ComponentModel.PropertyChangingEventHandler PropertyChanging;
    public string Foo { get; set; }
    protected virtual void OnPropertyChanging(global::System.ComponentModel.PropertyChangingEventArgs eventArgs)
    {
        this.OnAnyPropertyChanging(eventArgs.PropertyName);
        this.PropertyChanging?.Invoke(this, eventArgs);
    }
}