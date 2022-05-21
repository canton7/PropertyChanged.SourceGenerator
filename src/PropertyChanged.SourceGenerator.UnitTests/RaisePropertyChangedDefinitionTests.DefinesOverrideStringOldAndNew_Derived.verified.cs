partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanged(string propertyName, object oldValue, object newValue)
    {
        base.OnPropertyChanged(propertyName, oldValue, newValue);
        switch (propertyName)
        {
            case @"Foo":
            {
                string new_Bar = this.Bar;
                this.OnPropertyChanged(@"Bar", (object)null, new_Bar);
            }
            break;
        }
    }
}