partial class SomeViewModel
{
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                string old_Foo = this._foo;
                global::System.Collections.Generic.List<global::SomeViewModel> old_Bar = this.Bar;
                this._foo = value;
                string new_Foo = this._foo;
                global::System.Collections.Generic.List<global::SomeViewModel> new_Bar = this.Bar;
                this.OnPropertyChanged(@"Foo", old_Foo, new_Foo);
                this.OnPropertyChanged(@"Bar", old_Bar, new_Bar);
            }
        }
    }
}