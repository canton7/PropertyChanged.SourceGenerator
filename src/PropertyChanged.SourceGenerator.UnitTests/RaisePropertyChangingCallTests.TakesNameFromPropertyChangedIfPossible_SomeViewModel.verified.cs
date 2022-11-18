partial class SomeViewModel
{
    /// <inheritdoc />
    public event global::System.ComponentModel.PropertyChangingEventHandler PropertyChanging;
    public int Foo { get; set; }
    protected virtual void RaisePropertyChanging(global::System.ComponentModel.PropertyChangingEventArgs eventArgs)
    {
        this.PropertyChanging?.Invoke(this, eventArgs);
    }
}