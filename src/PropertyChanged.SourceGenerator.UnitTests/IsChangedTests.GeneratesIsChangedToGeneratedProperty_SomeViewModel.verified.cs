partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public bool IsChanged
    {
        get => this._isChanged;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<bool>.Default.Equals(value, this._isChanged))
            {
                this._isChanged = value;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.IsChanged);
            }
        }
    }
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                this._foo = value;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.IsChanged = true;
            }
        }
    }
}