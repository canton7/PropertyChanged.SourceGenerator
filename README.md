![Project Icon](icon-small.png) PropertyChanged.SourceGenerator
===============================================================

[![NuGet](https://img.shields.io/nuget/v/PropertyChanged.SourceGenerator.svg)](https://www.nuget.org/packages/PropertyChanged.SourceGenerator/)
[![Build status](https://ci.appveyor.com/api/projects/status/r989lw0mclb6jmja?svg=true)](https://ci.appveyor.com/project/canton7/propertychanged-sourcegenerator)

Implementing `INotifyPropertyChanged` is annoying.
PropertyChanged.SourceGenerator hooks into your compilation process to automatically generate the boilerplate for you, automatically.

PropertyChanged.SourceGenerator works well if you're using an MVVM framework or going without, and supports various time-saving features such as:

 - Automatically notifying dependent properties
 - Calling hooks when particular properties change
 - Keeping track of whether any properties have changed

### Table of Contents

1. [Installation](#installation)
1. [Quick Start](#quick-start)
1. [Versioning](#versioning)
1. [Defining your ViewModel](#defining-your-viewmodel)
1. [Defining Properties](#defining-properties)
   1. [Property Names](#property-names)
   1. [Property Accessibility](#property-accessibility)
1. [Property Dependencies](#property-dependencies)
   1. [Automatic Dependencies](#automatic-dependencies)
   1. [Manual Dependencies with `[DependsOn]`](#manual-dependencies-with-dependson)
   1. [Manual Dependencies with `[AlsoNotify]`](#manual-dependencies-with-alsonotify)
1. [Property Changed Hooks](#property-changed-hooks)
   1. [Type Hooks with `OnPropertyChanged`](#type-hooks-with-onpropertychanged)
   1. [Property Hooks with `On{PropertyName}Changed`](#property-hooks-with-onpropertynamechanged)
1. [Change Tracking with `[IsChanged]`](#change-tracking-with-ischanged)
1. [Configuration](#configuration)
   1. [Generated Property Names](#generated-property-names)
   1. [`OnPropertyChanged` Method Name](#onpropertychanged-method-name)
1. [Contributing](#contributing)
1. [Comparison to PropertyChanged.Fody](#comparison-to-propertychangedfody)


Installation
------------

[PropertyChanged.SourceGenerator is available on NuGet](https://www.nuget.org/packages/PropertyChanged.SourceGenerator).
You'll need to be running Visual Studio 2019 16.9 or higher, or be building using the .NET SDK 5.0.200 or higher (your project doesn't have to target .NET 5, you just need to be building using a newish version of the .NET SDK).

These dependencies may change in future versions, see [Versioning](#versioning).


Quick Start
-----------

```cs
using PropertyChanged.SourceGenerator;
public partial class MyViewModel
{
	[Notify] private string _lastName;
	public string FullName => $"Dr. {LastName}";
}
```

Make sure your ViewModel is `partial`, and define the backing fields for your properties, decorated with `[Notify]`.
When you build your project, PropertyChanged.SourceGenerator will create another partial class which looks something like:

```cs
using PropertyChanged.SourceGenerator;
partial class MyViewModel : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;

	public string LastName
	{
		get => _lastName;
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_lastName, value))
			{
				_lastName = value;
				OnPropertyChanged(EventArgsCache.LastName);
				OnPropertyChanged(EventArgsCache.FullName);
			}
		}
	}

	protected virtual void OnPropertyChanged(PropertyChangedEventArgs args)
	{
		PropertyChanged?.Invoke(args);
	}
}
```

What happened there?

1. PropertyChanged.SourceGenerator spotted that you defined a partial class and at least one property was decorated with `[Notify]`, so it got involved and generated another part to the partial class.
2. It noticed that you hadn't implemented `INotifyPropertyChanged`, so it implemented it for you (it's also fine if you implement it yourself).
3. For each field decorated with `[Notify]`, it generated property with the same name (but with the leading `_` removed and the first letter capitalised) which used that field as its backing field. That property implemented `INotifyPropertyChanged`.
4. It noticed that `FullName` depended on `LastName`, so raised the `PropertyChanged` event for `FullName` whenever `LastName` changed.

**Note**: It's really important that you don't refer to the backing fields after you've defined them: let PropertyChanged.SourceGenerator generate the corresponding property, and then always use the property.


Versioning
----------

Source Generators are a relatively new technology, and they're being improved all the time.
Unfortunately, in order for source generators to take advantage of improvements, they must target a newer version of Visual Studio / the .NET SDK.

If/when PropertyChanged.SourceGenerator is updated to depend on a new version Visual Studio / the .NET SDK, this will be signified by a **minor version bump**: the minor version number will be incremented.
Changes which mean you have to change existing *code* to keep PropertyChanged.SourceGenerator working will be signified by a **major version bump**.


| Version Number | Min Visual Studio Version | Min .NET SDK Version |
|----------------|---------------------------|----------------------|
| 1.0.x | 2019 16.9 | 5.0.200 |


Defining your ViewModel
-----------------------

When you define a ViewModel which makes use of PropertyChanged.SourceGenerator, that ViewModel must be `partial`.
If it isn't, you'll get a warning.

Your ViewModel can implement `INotifyPropertyChanged`, or not, or it can implement parts of it (such as implementing the interface but not defining the `PropertyChanged` event), or it can extend from a base class which implements `INotifyPropertyChanged`: PropertyChanged.SourceGenerator will figure it out and fill in the gaps.

If you've got a `ViewModel` base class which implements `INotifyPropertyChanged` (perhaps as part of an MVVM framework), PropertyChanged.SourceGenerator will try and find a suitable method to call to raise the `PropertyChanged` event.
It will look for a method called `OnPropertyChanged`, `RaisePropertyChanged`, `NotifyOfPropertyChange`, or `NotifyPropertyChanged`, which covers all of the major MVVM frameworks (although this is configurable, see [Configuration](#configuration)), with one of the following signatures:

 - `void OnPropertyChanged(PropertyChangedEventArgs args)`
 - `void OnPropertyChanged(string propertyName)`
 - `void OnPropertyChanged(PropertyChangedEventArgs args, object oldValue, object newValue)`
 - `void OnPropertyChanged(string propertyName, object oldValue, object newValue)`

If it can't find a suitable method, you'll get a warning and it won't run on that particular ViewModel.


Defining Properties
-------------------

To get PropertyChanged.SourceGenerator to generate a property which implements `INotifyPropertyChanged`, you must define the backing field for that property, and decorate it with `[Notify]`.

(This is an annoying effect of how Source Generators work. If you'd like a better way, please [vote for this issue on partial properties](https://github.com/dotnet/csharplang/discussions/3412)).

If you write:

```cs
using PropertyChanged.SourceGenerator;
public partial class MyViewModel : INotifyPropertyChanged
{
	[Notify] private int _foo;
}
```

PropertyChanged.SourceGenerator will generate something like:

```cs
partial class MyViewModel
{
	public int Foo
	{
		get => _foo,
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_foo, value))
			{
				_foo = value;
				OnPropertyChanged("Foo");
			}
		}
	}

	// PropertyChanged event, OnPropertyChanged method, etc.
}
```

### Property Names

The name of the generated property is derived from the name of the backing field, by:

1. Removing a `_` prefix, if one exists
2. Changing the first letter to upper-case

This can be customised, see [Configuration](#configuration).

If you want to manually specify the name of a particular property, you can pass a string to `[Notify]`:

```cs
using PropertyChanged.SourceGenerator;
public partial class MyViewModel : INotifyPropertyChanged
{
	[Notify("FOO")] private int _foo;
}
```

and PropertyChanged.SourceGenerator will generate a property called `FOO`.


### Property Accessibility

By default, all generated properties have public getters and public setters.

This isn't always what you want, so it's possible to override this by passing `Getter.XXX` and `Setter.XXX` to `[Notify]`.

```cs
using PropertyChanged.SourceGenerator;
public partial class MyViewModel : INotifyPropertyChanged
{
	[Notify(Setter.Private)]
	private int _foo;

	[Notify(Getter.PrivateProtected, Setter.Protected)]
	private string _bar;
}
```

This generates:

```cs
partial class MyViewModel
{
	public int Foo
	{
		get => _foo
		private set => { /* ... */ }
	}

	protected string Bar
	{
		private protected get => _bar,
		set { /* ... */ }
	}
}
```


Property Dependencies
---------------------

Sometimes, you have properties which depend on other properties, for example:

```cs
using PropertyChanged.SourceGenerator;
public partial class MyViewModel
{
	[Notify] private string _firstName;
	[Notify] private string _lastName;
	public string FullName => $"{FirstName} {LastName}";
}
```

Whenever `FirstName` or `LastName` is updated, you want to raise a PropertyChanged event for `FullName`, so that the UI also updates the value of `FullName` which is displayed.


### Automatic Dependencies

If a property has a getter which accesses a generated property on the same type, then PropertyChanged.SourceGenerator will automatically raise a PropertyChanged event every time the property it depends on changes.

For example, if you write:

```cs
using PropertyChanged.SourceGenerator;
public partial class MyViewModel
{
	[Notify] private string _lastName;
	public string FullName => $"Dr. {LastName}";
}
```

PropertyChanged.SourceGenerator will notice that the getter for `FullName` accesses the generated `LastName` property, and so it will add code to the `LastName` setter to raise a PropertyChanged event for `FullName` whenever `LastName` is set:

```cs
partial class MyViewModel : INotifyPropertyChanged
{
	public string LastName
	{
		get => _lastName;
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_lastName, value))
			{
				_lastName = value;
				OnPropertyChanged(EventArgsCache.LastName);
				OnPropertyChanged(EventArgsCache.FullName); // <-- Here
			}
		}
	}
}
```

Note that this can only work when a property getter accesses a property which is generated by PropertyChanged.SourceGenerator, on the same instance.
It can't work for getters which access properties on other types, or other instances of that type, or which are defined on base types.
It also only works if you reference the generated property, not its backing field (i.e. `LastName`, not `_lastName` above).


### Manual Dependencies with `[DependsOn]`

If automatic dependencies aren't working for you, you can also specify dependencies manually using the `[DependsOn]` attribute.
`[DependsOn]` takes the names of one or more generated properties, and means that a PropertyChanged event will be raised if any of those properties is set.

For example:

```cs
using PropertyChanged.SourceGenerator;
public partial class MyViewModel
{
	[Notify] private string _firstName;
	[Notify] private string _lastName;

	[DependsOn(nameof(FirstName), nameof(LastName))]
	public string FullName { get; set; }
}
```

The generated setters for `FirstName` and `LastName` will raise a PropertyChanged event for `FullName`.

As with automatic dependencies, `[DependsOn]` can only refer to properites which have been generated by PropertyChanged.SourceGenerator, on the same type.
It won't work for base types or derived types.


### Manual Dependencies with `[AlsoNotify]`

`[AlsoNotify]` is the opposite of `[DependsOn]`: you place it on a backing field which also has `[Notify]`, and PropertyChanged.SourceGenerator will insert code to raise a PropertyChanged for each named property whenever the generated property is set.

The named property or properties don't have to exist (although you'll get a warning if they don't), and you can raise PropertyChanged events for properties in base classes.

If you're naming a property which is generated by PropertyChanged.SourceGenerator, make sure you use the name of the generated property, and not the backing field.

For example:

```cs
using PropertyChanged.SourceGenerator;
public partial class MyViewModel
{
	[Notify, AlsoNotify(nameof(FullName))] private string _firstName;
	[Notify, AlsoNotify(nameof(FullName))] private string _lastName;

	public string FullName { get; set; }
}
```


Property Changed Hooks
----------------------

Hooks are a way for you to be told when a generated property is changed, without needing to subscribe to subscribe to a type's own PropertyChanged event.


### Type Hooks with `OnPropertyChanged`

The easiest way to be notified when any generated property has changed is to specify your own `OnPropertyChanged` method.
This is called by the generated property setters, and is responsible for raising the PropertyChanged event.

There are a number of different valid names and signatures for this method, listed in [Defining your ViewModel](#defining-your-viewmodel).
Some of these accept two `object` parameters, to which the old and new values of the property are passed.

Note that the old and new values for a property may be the same, if the property is being raised because of a [property dependency](#property-dependencies),


### Property Hooks with `On{PropertyName}Changed`

Let's say you have a generated property called `FirstName`.
If you define a method called `OnFirstNameChanged` in the same class, that method will be called every time `FirstName` changes.

This method can have two signatures:

 - `On{PropertyName}Changed()`
 - `On{PropertyName}Changed(T oldValue, T newValue)` where `T` is the type of the property with name `PropertyName`.

For example:

```cs
using PropertyChanged.SourceGenerator;
public partial class MyViewModel
{
	[Notify] private string _firstName;
	[Notify] private string _lastName;

	private void OnFirstNameChanged(string oldValue, string newValue)
	{
		// ...
	} 

	private void OnLastNameChanged()
	{
		// ...
	}
}
```


## Change tracking with `[IsChanged]`

Sometimes you need to keep track of whether any properties on a type have been set.

If you define a `bool` property and decorate it with `[IsChanged]`, then that property will be set to `true` whenever any generate properties are set.
It's then up to you to set it back to `false` when appropriate.

For example:

```cs
using PropertyChanged.SourceGenerator;
public partial class MyViewModel
{
	[IsChanged] public bool IsChanged { get; private set; }
	[Notify] private string _firstName;
}

var vm = new MyViewModel();
Assert.False(vm.IsChanged);

vm.FirstName = "Harry";
Assert.True(vm.IsChanged);
```

That `bool IsChanged` property can also be generated by PropertyChanged.SourceGenerator, if you want a PropertyChanged event to be raised when it changed;

```cs
using PropertyChanged.SourceGenerator;
public partial class MyViewModel
{
	[Notify, IsChanged] private bool _isChanged;
}
```


Configuration
-------------

Various aspects of PropertyChanged.SourceGenerator's behaviour can be configured through a [`.editorconfig` file](https://docs.microsoft.com/en-us/visualstudio/ide/create-portable-custom-editor-options).

If you have done already, great!
If not simply add a file called `.editorconfig` in the folder which contains your `.csproj` file (if you want those settings to apply to a single project), or next to your `.sln` file (to apply them to all projects in the solution).
There are various other ways to combine settings from different `.editorconfig` files, see the MSDN documentation.

All of PropertyChanged.SourceGenerator's settings must be in a `[*.cs]` section.


### Generated Property Names

There are a few settings which control how PropertyChanged.SourceGenerator turns the name of your backing field into the name of the property it generates.

```
[*.cs]

# A string to add to the beginning of any generated property name
# Default: ''
propertychanged.add_prefix =

# A string to remove from the beginning of any generated property name, if present
# Default: '_'
propertychanged.remove_prefix = _

# A string to add to the end of any generated property name
# Default: ''
propertychanged.add_suffix =

# A string to remove from the end of any generated property name
# Default: ''
propertychanged.remove_suffix =

# How the first letter of the generated property name should be capitalised
# Valid values: none, upper_case, lower_Case
# Default: 'upper_case'
propertychanged.first_letter_capitalisation = upper_case
```


### `OnPropertyChanged` Method Name

When PropertyChanged.SourceGenerator runs, it looks for a suitable pre-existing method which can be used to raise the PropertyChanged event.
If none is found, it will generate a suitable method itself, if it can.

The names of the pre-existing methods which it searches for, and the name of the method which it will generate, can be configured.

```
[*.cs]

# A ';' separated list of method names to search for when finding a method to raise the
# PropertyChanged event. If none is found, the first name listed here is used.
# Default: 'OnPropertyChanged;RaisePropertyChanged;NotifyOfPropertyChange;NotifyPropertyChanged'
propertychanged.onpropertychanged_method_name = OnPropertyChanged;RaisePropertyChanged;NotifyOfPropertyChange;NotifyPropertyChanged
```


Contributing
------------

It's great that you want to get involved!
Please [open a discussion](https://github.com/canton7/PropertyChanged.SourceGenerator/discussions/new) before doing any serious amount of work, so we can agree an approach before you waste any time.

Open a feature branch based on `develop` (**not** `master`), and make sure that you submit any Pull Requests to the `develop` branch.


Comparison to PropertyChanged.Fody
----------------------------------

PropertyChanged.SourceGenerator has the same goals as PropertyChanged.Fody.
Here are some of the differences:

 - PropertyChanged.Fody is able to rewrite your code, which PropertyChanged.SourceGenerator can only add to it (due to the design of Source Generators). This means that PropertyChanged.Fody is able to insert event-raising code directly into your property setters, whereas PropertyChanged.SourceGenerator needs to generate the whole property itself.
 - PropertyChanged.Fody supports some functionality which PropertyChanged.SourceGenerator does not, such as global interception. Please [let me know](https://github.com/canton7/PropertyChanged.SourceGenerator/discussions/new) if you need a bit of functionality which PropertyChanged.SourceGenerator doesn't yet support.
 - PropertyChanged.SourceGenerator supports some functionality which PropertyChanged.Fody does not, such as letting you define `On{PropertyName}Changed` methods which accept the old and new values of the property.
 - I don't [expect you to pay](https://github.com/Fody/Home/blob/master/pages/licensing-patron-faq.md) to use PropertyChanged.SourceGenerator, and will never close an issue or refuse a contribution because you're not giving me money.