partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public event global::System.ComponentModel.PropertyChangingEventHandler PropertyChanging;
    public int Foo { get; set; }
    /// <summary>
    /// Raises the PropertyChanged event
    /// </summary>
    /// <param name="eventArgs">The EventArgs to use to raise the event</param>
    /// <param name="oldValue">Current value of the property</param>
    /// <param name="newValue">New value of the property</param>
    protected virtual void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs, object oldValue, object newValue)
    {
        this.OnAnyPropertyChanged(eventArgs.PropertyName, oldValue, newValue);
        this.PropertyChanged?.Invoke(this, eventArgs);
    }
    /// <summary>
    /// Raises the PropertyChanging event
    /// </summary>
    /// <param name="eventArgs">The EventArgs to use to raise the event</param>
    /// <param name="oldValue">Current value of the property</param>
    protected virtual void OnPropertyChanging(global::System.ComponentModel.PropertyChangingEventArgs eventArgs, object oldValue)
    {
        this.OnAnyPropertyChanging(eventArgs.PropertyName, oldValue);
        this.PropertyChanging?.Invoke(this, eventArgs);
    }
}