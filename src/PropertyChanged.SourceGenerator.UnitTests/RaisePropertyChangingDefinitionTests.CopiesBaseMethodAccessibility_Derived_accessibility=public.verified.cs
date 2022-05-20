partial class Derived
{
    public string Bar { get; set; }
    public override void OnPropertyChanging(string propertyName)
    {
        base.OnPropertyChanging(propertyName);
        switch (propertyName)
        {
            case @"Foo":
            {
                this.OnPropertyChanging(@"Bar");
            }
            break;
        }
    }
}