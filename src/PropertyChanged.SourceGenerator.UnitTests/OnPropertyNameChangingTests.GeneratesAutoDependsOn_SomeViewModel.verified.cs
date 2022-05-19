partial class SomeViewModel
{
    public int Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(value, this._foo))
            {
                int old_Bar = this.Bar;
                string old_Baz = this.Baz;
                this.OnPropertyChanging(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanging_Foo);
                this.OnBarChanging(old_Bar);
                this.OnPropertyChanging(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanging_Bar);
                this.OnBazChanging(old_Baz);
                this.OnPropertyChanging(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanging_Baz);
                this._foo = value;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanged_Foo);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanged_Bar);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanged_Baz);
            }
        }
    }
}