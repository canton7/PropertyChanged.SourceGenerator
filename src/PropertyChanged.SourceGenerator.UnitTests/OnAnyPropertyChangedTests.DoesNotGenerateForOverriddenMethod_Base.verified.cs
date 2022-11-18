﻿partial class Base : global::System.ComponentModel.INotifyPropertyChanged
{
    /// <inheritdoc />
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public string Foo { get; set; }
    protected virtual void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        this.OnAnyPropertyChanged(eventArgs.PropertyName);
        this.PropertyChanged?.Invoke(this, eventArgs);
    }
}