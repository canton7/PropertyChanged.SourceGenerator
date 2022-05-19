#nullable enable
partial class SomeViewModel
{
    public string? Nullable
    {
        get => this._nullable;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string?>.Default.Equals(value, this._nullable))
            {
                this._nullable = value;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanged_Nullable);
            }
        }
    }
    public string NotNullable
    {
        get => this._notNullable;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._notNullable))
            {
                this._notNullable = value;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanged_NotNullable);
            }
        }
    }
    #nullable disable
    public string Oblivious
    {
        get => this._oblivious;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._oblivious))
            {
                this._oblivious = value;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanged_Oblivious);
            }
        }
    }
    #nullable enable
    public int? NullableValue
    {
        get => this._nullableValue;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<int?>.Default.Equals(value, this._nullableValue))
            {
                this._nullableValue = value;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.EventArgsCache.PropertyChanged_NullableValue);
            }
        }
    }
}