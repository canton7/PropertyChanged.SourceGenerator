partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanging(global::System.ComponentModel.PropertyChangingEventArgs eventArgs)
    {
        base.OnPropertyChanging(eventArgs);
        switch (eventArgs.PropertyName)
        {
            case @"Foo":
                this.OnPropertyChanging(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanging_Bar);
                break;
        }
    }
}