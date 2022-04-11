partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                string old_Foo = this.Foo;
                this._foo = value;
                this.OnFooChanged(old_Foo, this.Foo);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
            }
        }
    }
}