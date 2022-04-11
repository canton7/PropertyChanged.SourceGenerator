partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        this.OnAnyPropertyChanged(eventArgs.PropertyName);
        base.OnPropertyChanged(eventArgs);
    }
}