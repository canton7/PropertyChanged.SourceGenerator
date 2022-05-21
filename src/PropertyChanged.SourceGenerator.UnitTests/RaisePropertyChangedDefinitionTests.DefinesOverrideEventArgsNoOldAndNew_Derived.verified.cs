partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        base.OnPropertyChanged(eventArgs);
        switch (eventArgs.PropertyName)
        {
            case @"Foo":
            {
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanged_Bar);
            }
            break;
        }
    }
}