partial class B
{
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                this._foo = value;
                this.NotifyOfPropertyChange(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
            }
        }
    }
}