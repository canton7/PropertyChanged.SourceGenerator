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
                this._foo = value;
                int new_Bar = this.Bar;
                string new_Baz = this.Baz;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanged_Foo);
                this.OnBarChanged(old_Bar, new_Bar);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanged_Bar);
                this.OnBazChanged(old_Baz, new_Baz);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanged_Baz);
            }
        }
    }
}