partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanged(string propertyName, object oldValue, object newValue)
    {
        base.OnPropertyChanged(propertyName, oldValue, newValue);
        switch (propertyName)
        {
            case @"Foo":
                this.OnPropertyChanged(@"Bar", (object)null, this.Bar);
                break;
        }
    }
}