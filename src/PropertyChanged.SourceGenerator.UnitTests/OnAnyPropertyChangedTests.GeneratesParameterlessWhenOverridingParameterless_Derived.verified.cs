partial class Derived : global::System.ComponentModel.INotifyPropertyChanged
{
    public int Foo { get; set; }
    protected override void OnPropertyChanged(string propertyName)
    {
        this.OnAnyPropertyChanged(propertyName);
        base.OnPropertyChanged(propertyName);
    }
}