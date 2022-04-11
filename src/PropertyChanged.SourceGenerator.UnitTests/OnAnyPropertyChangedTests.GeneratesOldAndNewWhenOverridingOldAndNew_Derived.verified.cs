partial class Derived : global::System.ComponentModel.INotifyPropertyChanged
{
    public int Foo { get; set; }
    protected override void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs, object oldValue, object newValue)
    {
        this.OnAnyPropertyChanged(eventArgs.PropertyName, oldValue, newValue);
        base.OnPropertyChanged(eventArgs, oldValue, newValue);
    }
}