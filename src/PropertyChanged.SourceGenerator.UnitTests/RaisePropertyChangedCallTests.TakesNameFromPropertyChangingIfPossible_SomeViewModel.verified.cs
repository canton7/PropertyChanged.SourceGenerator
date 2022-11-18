partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    /// <inheritdoc />
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public int Foo { get; set; }
    protected virtual void NotifyOfPropertyChange(global::System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        this.PropertyChanged?.Invoke(this, eventArgs);
    }
}