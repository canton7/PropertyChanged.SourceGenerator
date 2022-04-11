partial class Derived : global::System.ComponentModel.INotifyPropertyChanged
{
    public string Bar
    {
        get => this._bar;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._bar))
            {
                this._bar = value;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
            }
        }
    }
}