partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    /// <inheritdoc />
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public string Foo { get; set; }
    protected virtual void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs, object oldValue, object newValue)
    {
        this.OnAnyPropertyChanged(eventArgs.PropertyName, oldValue, newValue);
        this.PropertyChanged?.Invoke(this, eventArgs);
    }
}