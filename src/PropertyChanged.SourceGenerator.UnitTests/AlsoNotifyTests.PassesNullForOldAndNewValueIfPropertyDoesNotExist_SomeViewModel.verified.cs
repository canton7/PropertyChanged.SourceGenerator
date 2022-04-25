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
                this.OnPropertyChanged(@"Foo", old_Foo, this.Foo);
                this.OnPropertyChanged(@"", (object)null, (object)null);
                this.OnPropertyChanged(@"Item[]", (object)null, (object)null);
                this.OnPropertyChanged(@"NonExistent", (object)null, (object)null);
            }
        }
    }
}