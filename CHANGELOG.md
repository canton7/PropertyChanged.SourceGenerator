v1.1.0
------

 - Restructure as an incremental source generator


v1.0.8
------

 - Support the user explicitly implementing the `PropertyChanged`/`PropertyChanging` event (#38)

v1.0.7
------

 - Allow automatic property depenency resolution to be disabled project-wide (#36)
 - Tweak what errors are raised when a suitable method to raise the PropertyChanged event could not be found (#33)
 - Properly support explicitly-implemented properties (#35)
 - Support generating virtual properties (#27)
 - Add `<inheritdoc/>` to generated events (#29)
 - Fix README documentation on remove_{prefixes/suffixes} (#25)

v1.0.6
------

 - Don't crash when analysing a property which refers to itself (#24)

v1.0.5
------

 - Support placing attributes on generated properties with `[PropertyAttribute]`

v1.0.4
------

 - Add support for `INotifyPropertyChanging`
 - Generate doc comments on `OnPropertyChanged`/`OnPropertyChanging`

v1.0.3
------

 - Handle malformed XML doc comments
 - Don't propagate crashes to Roslyn, as it gets stuck in the "Generator is not generating files" state
 - Add README to NuGet package

v1.0.2
------

 - Correctly handle sealed types (#11)
 - Copy XML documentation from the field to generated property
 - Support automatic/manual `DependsOn` properties which refer to other properties
 - Support automatic/manual `DependsOn` with properties defined in the base class
 - Introduce the `OnAnyPropertyChanged` hook: using this is now recommended above overriding `OnPropertyChanged`

v1.0.1
------

 - Correctly detect generic base types (#3)
 - Fix handling partial inner classes (#2)
 - Fix crash when two types with the same name exist in different namespaces (#4)
 - README fixes and improvements

v1.0.0
------

 - Initial release
