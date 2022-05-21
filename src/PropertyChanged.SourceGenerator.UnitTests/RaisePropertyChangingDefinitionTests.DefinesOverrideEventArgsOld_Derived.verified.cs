partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanging(global::System.ComponentModel.PropertyChangingEventArgs eventArgs, object oldValue)
    {
        base.OnPropertyChanging(eventArgs, oldValue);
        switch (eventArgs.PropertyName)
        {
            case @"Foo":
            {
                this.OnPropertyChanging(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanging_Bar, (object)null);
            }
            break;
        }
    }
}