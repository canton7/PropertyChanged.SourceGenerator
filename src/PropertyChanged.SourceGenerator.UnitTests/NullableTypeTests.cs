using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests
{
    [TestFixture]
    public class NullableTypeTests : TestsBase
    {
        [Test]
        public void GeneratesNullableEventIfInCompilationNullableContext()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo = """";
}";
            string expected = @"
#nullable enable
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string Foo { get; set; }
}";

            this.AssertSource(expected, input, RemovePropertiesRewriter.Instance, NullableContextOptions.Enable);
        }

        [Test]
        public void DoesNotGenerateNullableEventIfInFileNullableContext()
        {
            string input = @"
#nullable enable
public partial class SomeViewModel
{
    [Notify]
    private string _foo = """";
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    #nullable enable annotations
    public string Foo { get; set; }
    #nullable disable
}";

            this.AssertSource(expected, input, RemovePropertiesRewriter.Instance, NullableContextOptions.Disable);
        }

        [Test]
        public void GeneratesNullablePropertiesIfInCompilationNullableContext()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string? _nullable;
    [Notify]
    private string _notNullable = """";
#nullable disable
    [Notify]
    private string _oblivious;
#nullable restore
    [Notify]
    private int? _nullableValue;
}";
            string expected = @"
#nullable enable
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string? Nullable
    {
        get => this._nullable;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string?>.Default.Equals(value, this._nullable))
            {
                this._nullable = value;
                this.PropertyChanged?.Invoke(this, global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Nullable);
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
                this.PropertyChanged?.Invoke(this, global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.NotNullable);
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
                this.PropertyChanged?.Invoke(this, global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Oblivious);
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
                this.PropertyChanged?.Invoke(this, global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.NullableValue);
            }
        }
    }
}";

            this.AssertSource(expected, input, nullableContextOptions: NullableContextOptions.Enable);
        }

        [Test]
        public void GeneratesNullablePropertiesIfInFilenNullableContext()
        {
            string input = @"
#nullable enable
public partial class SomeViewModel
{
    [Notify]
    private string? _nullable;
    [Notify]
    private string _notNullable = """";
#nullable disable
    [Notify]
    private string _oblivious;
#nullable restore
    [Notify]
    private int? _nullableValue;
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    #nullable enable annotations
    public string? Nullable
    {
        get => this._nullable;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string?>.Default.Equals(value, this._nullable))
            {
                this._nullable = value;
                this.PropertyChanged?.Invoke(this, global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Nullable);
            }
        }
    }
    #nullable disable
    #nullable enable annotations
    public string NotNullable
    {
        get => this._notNullable;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._notNullable))
            {
                this._notNullable = value;
                this.PropertyChanged?.Invoke(this, global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.NotNullable);
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
                this.PropertyChanged?.Invoke(this, global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Oblivious);
            }
        }
    }
    public int? NullableValue
    {
        get => this._nullableValue;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<int?>.Default.Equals(value, this._nullableValue))
            {
                this._nullableValue = value;
                this.PropertyChanged?.Invoke(this, global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.NullableValue);
            }
        }
    }
}";

            this.AssertSource(expected, input);
        }
    }
}
