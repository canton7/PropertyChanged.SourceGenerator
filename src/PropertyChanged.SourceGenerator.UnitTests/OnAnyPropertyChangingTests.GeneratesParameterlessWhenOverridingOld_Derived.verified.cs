partial class Derived
{
    public int Foo { get; set; }
    protected override void OnPropertyChanging(string propertyName, object oldValue)
    {
        this.OnAnyPropertyChanging(propertyName);
        base.OnPropertyChanging(propertyName, oldValue);
    }
}