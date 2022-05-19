partial class Derived
{
    public int Foo { get; set; }
    protected override void OnPropertyChanging(string propertyName)
    {
        this.OnAnyPropertyChanging(propertyName);
        base.OnPropertyChanging(propertyName);
    }
}