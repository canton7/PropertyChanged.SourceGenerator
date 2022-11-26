partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    /// <inheritdoc />
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    /// <inheritdoc />
    public event global::System.ComponentModel.PropertyChangingEventHandler PropertyChanging;
    public int Foo { get; set; }
    /// <summary>
    /// Raises the PropertyChanged event
    /// </summary>
    /// <param name="eventArgs">The EventArgs to use to raise the event</param>
    protected virtual void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        this.PropertyChanged?.Invoke(this, eventArgs);
    }
    /// <summary>
    /// Raises the PropertyChanging event
    /// </summary>
    /// <param name="eventArgs">The EventArgs to use to raise the event</param>
    protected virtual void OnPropertyChanging(global::System.ComponentModel.PropertyChangingEventArgs eventArgs)
    {
        this.PropertyChanging?.Invoke(this, eventArgs);
    }
}