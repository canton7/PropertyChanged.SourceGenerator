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
