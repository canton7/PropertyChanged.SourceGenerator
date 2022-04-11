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
                global::System.Collections.Generic.List<global::SomeViewModel> old_Bar = this.Bar;
                this._foo = value;
                this.OnPropertyChanged(@"Foo", old_Foo, this.Foo);
                this.OnPropertyChanged(@"Bar", old_Bar, this.Bar);
            }
        }
    }
}