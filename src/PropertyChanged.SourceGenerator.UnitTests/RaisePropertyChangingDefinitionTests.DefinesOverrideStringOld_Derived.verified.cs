partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanging(string propertyName, object oldValue)
    {
        base.OnPropertyChanging(propertyName, oldValue);
        switch (propertyName)
        {
            case @"Foo":
                this.OnPropertyChanging(@"Bar", (object)null);
                break;
        }
    }
}