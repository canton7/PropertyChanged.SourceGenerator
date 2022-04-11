partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
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
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.OnBarChanged(old_Bar, this.Bar);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
                this.OnBazChanged(old_Baz, this.Baz);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Baz);
            }
        }
    }
}