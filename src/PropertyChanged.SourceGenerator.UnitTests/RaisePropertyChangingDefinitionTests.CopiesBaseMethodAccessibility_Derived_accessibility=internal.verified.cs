partial class Derived
{
    public string Bar { get; set; }
    internal override void OnPropertyChanging(string propertyName)
    {
        base.OnPropertyChanging(propertyName);
        switch (propertyName)
        {
            case @"Foo":
                this.OnPropertyChanging(@"Bar");
                break;
        }
    }
}