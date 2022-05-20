partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);
        switch (propertyName)
        {
            case @"Foo":
            {
                this.OnPropertyChanged(@"Bar");
            }
            break;
        }
    }
}