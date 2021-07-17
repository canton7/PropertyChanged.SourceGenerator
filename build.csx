#!/usr/bin/env dotnet-script

#r "nuget:SimpleTasks, 0.9.4"

using SimpleTasks;
using static SimpleTasks.SimpleTask;

#nullable enable

string libDir = "src/PropertyChanged.SourceGenerator";
string testsDir = "src/PropertyChanged.SourceGenerator.UnitTests";

string nugetDir = "NuGet";

string CommonFlags(string? version, string? configuration) =>
    $"--configuration={configuration ?? "Release"} -p:VersionPrefix=\"{version ?? "0.0.0"}\"";

CreateTask("build").Run((string versionOpt, string configurationOpt) =>
{
    var flags = CommonFlags(versionOpt, configurationOpt);
    Command.Run("dotnet", $"build {flags} \"{libDir}\"");
});

CreateTask("package").DependsOn("build").Run((string version, string configurationOpt) =>
{
    var flags = CommonFlags(version, configurationOpt) + $" --no-build --output=\"{nugetDir}\"";
    Command.Run("dotnet", $"pack {flags} \"{libDir}\"");
});

CreateTask("test").Run(() =>
{
    Command.Run("dotnet", $"test \"{testsDir}\"");
});

CreateTask("coverage").Run(() =>
{
    Command.Run("dotnet", $"test -p:AltCover=true {testsDir}");
    Command.Run("dotnet", $"reportgenerator -reports:{testsDir}/coverage.xml -targetdir:coverage -assemblyfilters:+PropertyChanged.SourceGenerator");
});

return InvokeTask(Args);
