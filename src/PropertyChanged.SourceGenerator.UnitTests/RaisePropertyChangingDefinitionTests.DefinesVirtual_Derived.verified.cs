partial class Derived
{
    public event global::System.ComponentModel.PropertyChangingEventHandler PropertyChanging;
    public string Bar { get; set; }
    protected virtual void OnPropertyChanging(global::System.ComponentModel.PropertyChangingEventArgs eventArgs)
    {
        this.PropertyChanging?.Invoke(this, eventArgs);
        switch (eventArgs.PropertyName)
        {
            case @"Foo":
                this.OnPropertyChanging(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanging_Bar);
                break;
        }
    }
}