partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanging(global::System.ComponentModel.PropertyChangingEventArgs eventArgs)
    {
        this.OnAnyPropertyChanging(eventArgs.PropertyName);
        base.OnPropertyChanging(eventArgs);
    }
}