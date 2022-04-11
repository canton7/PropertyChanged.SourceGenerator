partial class SomeViewModel
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
                this.NotifyPropertyChanged(@"Foo", old_Foo, this.Foo);
            }
        }
    }
}