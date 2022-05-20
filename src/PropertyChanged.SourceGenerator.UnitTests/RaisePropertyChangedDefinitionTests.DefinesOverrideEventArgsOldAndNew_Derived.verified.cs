partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs, object oldValue, object newValue)
    {
        base.OnPropertyChanged(eventArgs, oldValue, newValue);
        switch (eventArgs.PropertyName)
        {
            case @"Foo":
            {
                string new_Bar = this.Bar;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanged_Bar, (object)null, new_Bar);
            }
            break;
        }
    }
}