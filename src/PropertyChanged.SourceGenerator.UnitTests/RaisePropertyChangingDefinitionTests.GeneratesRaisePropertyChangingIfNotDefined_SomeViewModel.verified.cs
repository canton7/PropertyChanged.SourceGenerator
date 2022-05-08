partial class SomeViewModel
{
    public string Foo { get; set; }
    protected virtual void OnPropertyChanging(global::System.ComponentModel.PropertyChangingEventArgs eventArgs)
    {
        this.PropertyChanging?.Invoke(this, eventArgs);
    }
}