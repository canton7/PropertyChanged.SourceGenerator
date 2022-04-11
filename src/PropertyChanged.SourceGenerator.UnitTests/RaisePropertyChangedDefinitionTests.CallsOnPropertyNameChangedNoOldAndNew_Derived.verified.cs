partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs, object oldValue, object newValue)
    {
        base.OnPropertyChanged(eventArgs, oldValue, newValue);
        switch (eventArgs.PropertyName)
        {
            case @"Foo":
                this.OnBarChanged();
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar, (object)null, this.Bar);
                break;
        }
    }
}