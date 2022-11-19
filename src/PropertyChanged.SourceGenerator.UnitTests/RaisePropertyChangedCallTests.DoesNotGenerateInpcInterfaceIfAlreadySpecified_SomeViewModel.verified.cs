partial class SomeViewModel
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public string Foo { get; set; }
    protected virtual void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        this.PropertyChanged?.Invoke(this, eventArgs);
    }
}