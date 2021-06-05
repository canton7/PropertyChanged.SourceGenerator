#!/usr/bin/env dotnet-script

#r "nuget:SimpleTasks, 0.9.4"

using SimpleTasks;
using static SimpleTasks.SimpleTask;

#nullable enable

string testsDir = "src/PropertyChanged.SourceGenerator.UnitTests";

CreateTask("coverage").Run(() =>
{
    Command.Run("dotnet", $"test -p:AltCover=true {testsDir}");
    Command.Run("dotnet", $"reportgenerator -reports:{testsDir}/coverage.xml -targetdir:coverage -assemblyfilters:+PropertyChanged.SourceGenerator");
});

return InvokeTask(Args);
