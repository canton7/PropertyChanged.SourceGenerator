using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using PropertyChanged.SourceGenerator.Analysis;

namespace PropertyChanged.SourceGenerator;

public class Generator
{
    public const string EventArgsCacheName = "EventArgsCache";

    private readonly IndentedTextWriter writer = new(new StringWriter());
    private readonly EventArgsCache eventArgsCache;

    public Generator(EventArgsCache eventArgsCache)
    {
        this.eventArgsCache = eventArgsCache;
     
        this.writer.WriteLine(StringConstants.FileHeader);
    }

    public void Generate(TypeAnalysis typeAnalysis)
    {
        // SG'd files default to 'disable'
        if (typeAnalysis.NullableContext != NullableContextOptions.Disable)
        {
            this.writer.WriteLine(NullableContextToComment(typeAnalysis.NullableContext));
        }

        if (typeAnalysis.ContainingNamespace != null)
        {
            this.writer.WriteLine($"namespace {typeAnalysis.ContainingNamespace}");
            this.writer.WriteLine("{");
            this.writer.Indent++;

            this.GenerateType(typeAnalysis);

            this.writer.Indent--;
            this.writer.WriteLine("}");
        }
        else
        {
            this.GenerateType(typeAnalysis);
        }
    }

    private void GenerateType(TypeAnalysis typeAnalysis)
    {
        foreach (string outerType in typeAnalysis.OuterTypes.AsEnumerable().Reverse())
        {
            this.writer.WriteLine($"partial {outerType}");
            this.writer.WriteLine("{");
            this.writer.Indent++;
        }

        this.writer.Write($"partial {typeAnalysis.TypeDeclaration}");
        var interfaces = new List<string>();
        if (typeAnalysis.INotifyPropertyChanged.RequiresInterface)
        {
            interfaces.Add("global::System.ComponentModel.INotifyPropertyChanged");
        }
        if (typeAnalysis.INotifyPropertyChanging.RequiresInterface)
        {
            interfaces.Add("global::System.CompoenntModel.INotifyPropertyChanging");
        }
        if (interfaces.Count > 0)
        {
            this.writer.Write(" : ");
            this.writer.Write(string.Join(", ", interfaces));
        }

        this.writer.WriteLine();
        
        this.writer.WriteLine("{");
        this.writer.Indent++;
        
        string nullable = typeAnalysis.NullableContext.HasFlag(NullableContextOptions.Annotations) ? "?" : "";
        if (typeAnalysis.INotifyPropertyChanged.RequiresEvent)
        {
            this.writer.WriteLine("/// <inheritdoc />");
            this.writer.WriteLine($"public event global::System.ComponentModel.PropertyChangedEventHandler{nullable} PropertyChanged;");
        }
        if (typeAnalysis.INotifyPropertyChanging.RequiresEvent)
        {
            this.writer.WriteLine("/// <inheritdoc />");
            this.writer.WriteLine($"public event global::System.ComponentModel.PropertyChangingEventHandler{nullable} PropertyChanging;");
        }

        foreach (var member in typeAnalysis.Members)
        {
            this.GenerateMember(typeAnalysis, member);
        }

        this.GenerateRaisePropertyChangingOrChangedMethod(typeAnalysis, typeAnalysis.INotifyPropertyChanged, x => x.OnPropertyNameChanged);
        this.GenerateRaisePropertyChangingOrChangedMethod(typeAnalysis, typeAnalysis.INotifyPropertyChanging, x => x.OnPropertyNameChanging);
        
        this.writer.Indent--;
        this.writer.WriteLine("}");

        for (int i = 0; i < typeAnalysis.OuterTypes.Count; i++)
        {
            this.writer.Indent--;
            this.writer.WriteLine("}");
        }
    }

    private void GenerateRaisePropertyChangingOrChangedMethod(
        TypeAnalysis typeAnalysis,
        InterfaceAnalysis interfaceAnalysis,
        Func<IMember, OnPropertyNameChangedInfo?> onPropertyNameChangedOrChangingGetter)
    {
        if (interfaceAnalysis.RaiseMethodType == RaisePropertyChangedMethodType.None ||
            (interfaceAnalysis.RaiseMethodType == RaisePropertyChangedMethodType.Override &&
            typeAnalysis.BaseDependsOn.Count == 0 &&
            interfaceAnalysis.OnAnyPropertyChangedOrChangingInfo == null))
        {
            return;
        }

        const string propertyNameParamName = "propertyName";
        const string eventArgsParamName = "eventArgs";
        const string oldValueParamName = "oldValue";
        const string newValueParamName = "newValue";

        if (interfaceAnalysis.RaiseMethodType != RaisePropertyChangedMethodType.Override)
        {
            this.writer.WriteLine("/// <summary>");
            this.writer.WriteLine($"/// Raises the {interfaceAnalysis.EventName} event");
            this.writer.WriteLine("/// </summary>");
            switch (interfaceAnalysis.RaiseMethodSignature.NameType)
            {
                case RaisePropertyChangedOrChangingNameType.String:
                    // I don't think it's currently possible to have non-override + string eventName, but in case things change...
                    this.writer.WriteLine($"/// <param name=\"{propertyNameParamName}\">The name of the property to raise the event for</param>");
                    break;
                case RaisePropertyChangedOrChangingNameType.PropertyChangedEventArgs:
                    this.writer.WriteLine($"/// <param name=\"{eventArgsParamName}\">The EventArgs to use to raise the event</param>");
                    break;
            }
            if (interfaceAnalysis.RaiseMethodSignature.HasOld)
            {
                this.writer.WriteLine($"/// <param name=\"{oldValueParamName}\">Current value of the property</param>");
            }
            if (interfaceAnalysis.RaiseMethodSignature.HasNew)
            {
                this.writer.WriteLine($"/// <param name=\"{newValueParamName}\">New value of the property</param>");
            }
        }

        this.writer.Write(AccessibilityToString(interfaceAnalysis.RaiseMethodSignature.Accessibility));
        switch (interfaceAnalysis.RaiseMethodType)
        {
            case RaisePropertyChangedMethodType.Virtual:
                this.writer.Write("virtual ");
                break;
            case RaisePropertyChangedMethodType.Override:
                this.writer.Write("override ");
                break;
            case RaisePropertyChangedMethodType.None:
            case RaisePropertyChangedMethodType.NonVirtual:
                break;
        }
        this.writer.Write($"void {interfaceAnalysis.RaiseMethodName}(");
        string propertyNameOrEventArgsName = null!;
        string propertyNameAccessor = null!;
        switch (interfaceAnalysis.RaiseMethodSignature.NameType)
        {
            case RaisePropertyChangedOrChangingNameType.String:
                this.writer.Write($"string {propertyNameParamName}");
                propertyNameOrEventArgsName = propertyNameParamName;
                propertyNameAccessor = propertyNameParamName;
                break;
            case RaisePropertyChangedOrChangingNameType.PropertyChangedEventArgs:
                this.writer.Write(interfaceAnalysis.EventArgsFullyQualifiedTypeName);
                this.writer.Write($" {eventArgsParamName}");
                propertyNameOrEventArgsName = eventArgsParamName;
                propertyNameAccessor = $"{eventArgsParamName}.PropertyName";
                break;
        }
        if (interfaceAnalysis.RaiseMethodSignature.HasOld)
        {
            this.writer.Write($", object {oldValueParamName}");
        }
        if (interfaceAnalysis.RaiseMethodSignature.HasNew)
        {
            this.writer.Write($", object {newValueParamName}");
        }
        this.writer.WriteLine(")");

        this.writer.WriteLine("{");
        this.writer.Indent++;

        if (interfaceAnalysis.OnAnyPropertyChangedOrChangingInfo is { } onAnyPropertyChangedOrChangingInfo)
        {
            this.writer.Write($"this.{onAnyPropertyChangedOrChangingInfo.Name}({propertyNameAccessor}");
            if (onAnyPropertyChangedOrChangingInfo.HasOld)
            {
                this.writer.Write(interfaceAnalysis.RaiseMethodSignature.HasOld ? $", {oldValueParamName}" : ", (object)null");
            }
            if (onAnyPropertyChangedOrChangingInfo.HasNew)
            {
                this.writer.Write(interfaceAnalysis.RaiseMethodSignature.HasNew ? $", {newValueParamName}" : ", (object)null");
            }
            this.writer.WriteLine(");");
        }

        switch (interfaceAnalysis.RaiseMethodType)
        {
            case RaisePropertyChangedMethodType.Virtual:
            case RaisePropertyChangedMethodType.NonVirtual:
                // If we're generating our own, we always use PropertyChangedEventArgs
                Trace.Assert(interfaceAnalysis.RaiseMethodSignature.NameType == RaisePropertyChangedOrChangingNameType.PropertyChangedEventArgs);
                this.writer.WriteLine($"this.{interfaceAnalysis.EventName}?.Invoke(this, {eventArgsParamName});");
                break;
            case RaisePropertyChangedMethodType.Override:
                this.writer.Write($"base.{interfaceAnalysis.RaiseMethodName}({propertyNameOrEventArgsName}");
                if (interfaceAnalysis.RaiseMethodSignature.HasOld)
                {
                    this.writer.Write($", {oldValueParamName}");
                }
                if (interfaceAnalysis.RaiseMethodSignature.HasNew)
                {
                    this.writer.Write($", {newValueParamName}");
                }
                this.writer.WriteLine(");");
                break;
            case RaisePropertyChangedMethodType.None:
                break;
        }

        if (typeAnalysis.BaseDependsOn.Count > 0)
        {
            this.writer.WriteLine($"switch ({propertyNameAccessor})");
            this.writer.WriteLine("{");
            this.writer.Indent++;

            var baseDependsOn = typeAnalysis.BaseDependsOn.ToLookup(x => x.baseProperty);
            foreach (var group in baseDependsOn)
            {
                this.writer.WriteLine($"case {EscapeString(group.Key)}:");
                this.writer.WriteLine("{");
                this.writer.Indent++;
                foreach (var (_, notifyProperty) in group)
                {
                    var onPropertyNameChangedOrChangingInfo = onPropertyNameChangedOrChangingGetter(notifyProperty);
                    if (onPropertyNameChangedOrChangingInfo?.HasNew == true ||
                        (interfaceAnalysis.CanCallRaiseMethod && interfaceAnalysis.RaiseMethodSignature.HasNew))
                    {
                        this.GenerateNewVariable(notifyProperty);
                    }
                    this.GenerateOnPropertyNameChangedOrChangingIfNecessary(notifyProperty, onPropertyNameChangedOrChangingInfo, hasOldVariable: false);
                    this.GenerateRaiseEvent(interfaceAnalysis, notifyProperty.Name, notifyProperty.IsCallable, hasOldVariable: false);
                }
                this.writer.Indent--;
                this.writer.WriteLine("}");
                this.writer.WriteLine("break;");
            }

            this.writer.Indent--;
            this.writer.WriteLine("}");
        }

        this.writer.Indent--;
        this.writer.WriteLine("}");
    }

    private void GenerateMember(TypeAnalysis type, MemberAnalysis member)
    {
        string backingMemberReference = GetBackingMemberReference(member);

        if (member.NullableContextOverride is { } context)
        {
            this.writer.WriteLine(NullableContextToComment(context));
        }

        var (propertyAccessibility, getterAccessibility, setterAccessibility) = CalculateAccessibilities(member);

        foreach (string line in member.DocComment)
        {
            this.writer.WriteLine($"/// {line}");
        }

        foreach (string attr in member.AttributesForGeneratedProperty)
        {
            this.writer.WriteLine(attr);
        }

        this.writer.WriteLine($"{propertyAccessibility}{(member.IsVirtual ? "virtual " : "")}{member.FullyQualifiedTypeName} {member.Name}");
        this.writer.WriteLine("{");
        this.writer.Indent++;

        this.writer.WriteLine($"{getterAccessibility}get => {backingMemberReference};");
        this.writer.WriteLine($"{setterAccessibility}set");
        this.writer.WriteLine("{");
        this.writer.Indent++;

        this.writer.WriteLine("if (!global::System.Collections.Generic.EqualityComparer<" +
            member.FullyQualifiedTypeName +
            $">.Default.Equals(value, {backingMemberReference}))");
        this.writer.WriteLine("{");
        this.writer.Indent++;

        this.GenerateOldVariablesIfNecessary(type, member);

        this.GenerateOnPropertyNameChangedOrChangingIfNecessary(member, member.OnPropertyNameChanging, hasOldVariable: true);
        this.GenerateRaiseEvent(type.INotifyPropertyChanging, member.Name, isCallable: true, hasOldVariable: true);
        foreach (var alsoNotify in member.AlsoNotify.OrderBy(x => x.Name))
        {
            this.GenerateOnPropertyNameChangedOrChangingIfNecessary(alsoNotify, alsoNotify.OnPropertyNameChanging, hasOldVariable: true);
            this.GenerateRaiseEvent(type.INotifyPropertyChanging, alsoNotify.Name, alsoNotify.IsCallable, hasOldVariable: true);
        }

        this.writer.WriteLine($"{backingMemberReference} = value;");

        this.GenerateNewVariablesIfNecessary(type, member);

        this.GenerateOnPropertyNameChangedOrChangingIfNecessary(member, member.OnPropertyNameChanged, hasOldVariable: true);
        this.GenerateRaiseEvent(type.INotifyPropertyChanged, member.Name, isCallable: true, hasOldVariable: true);
        foreach (var alsoNotify in member.AlsoNotify.OrderBy(x => x.Name))
        {
            this.GenerateOnPropertyNameChangedOrChangingIfNecessary(alsoNotify, alsoNotify.OnPropertyNameChanged, hasOldVariable: true);
            this.GenerateRaiseEvent(type.INotifyPropertyChanged, alsoNotify.Name, alsoNotify.IsCallable, hasOldVariable: true);
        }

        if (type.IsChangedPropertyName != null && type.IsChangedPropertyName != member.Name)
        {
            this.writer.WriteLine($"this.{type.IsChangedPropertyName} = true;");
        }

        this.writer.Indent--;
        this.writer.WriteLine("}");
        this.writer.Indent--;
        this.writer.WriteLine("}");
        this.writer.Indent--;
        this.writer.WriteLine("}");

        if (member.NullableContextOverride != null)
        {
            this.writer.WriteLine(NullableContextToComment(type.NullableContext));
        }
    }

    private static string GetBackingMemberReference(MemberAnalysis member) =>
        "this." + member.BackingMemberSymbolName;

    private void GenerateOldVariablesIfNecessary(TypeAnalysis type, MemberAnalysis member)
    {
        Write(member, GetBackingMemberReference(member));

        foreach (var alsoNotify in member.AlsoNotify)
        {
            if (alsoNotify.IsCallable)
            {
                Write(alsoNotify, $"this.{alsoNotify.Name}");
            }
        }

        void Write<T>(T member, string backingMemberReference) where T : IMember
        {
            if (type.INotifyPropertyChanged.RaiseMethodSignature.HasOld || member.OnPropertyNameChanged?.HasOld == true ||
                type.INotifyPropertyChanging.RaiseMethodSignature.HasOld || member.OnPropertyNameChanging?.HasOld == true)
            {
                this.writer.WriteLine($"{member.FullyQualifiedTypeName} old_{member.Name} = {backingMemberReference};");
            }
        }
    }

    private void GenerateNewVariablesIfNecessary(TypeAnalysis type, MemberAnalysis member)
    {
        Write(member, GetBackingMemberReference(member));

        foreach (var alsoNotify in member.AlsoNotify)
        {
            if (alsoNotify.IsCallable)
            {
                Write(alsoNotify);
            }
        }

        void Write<T>(T member, string? backingMemberReferenceOverride = null) where T : IMember
        {
            if (type.INotifyPropertyChanged.RaiseMethodSignature.HasNew || member.OnPropertyNameChanged?.HasNew == true ||
                type.INotifyPropertyChanging.RaiseMethodSignature.HasNew || member.OnPropertyNameChanging?.HasNew == true)
            {
                this.GenerateNewVariable(member, backingMemberReferenceOverride);
            }
        }
    }

    private void GenerateNewVariable<T>(T member, string? backingMemberReferenceOverride = null) where T : IMember
    {
        string backingMemberReference = backingMemberReferenceOverride ?? $"this.{member.Name}";
        this.writer.WriteLine($"{member.FullyQualifiedTypeName} new_{member.Name} = {backingMemberReference};");
    }

    private void GenerateRaiseEvent(InterfaceAnalysis interfaceAnalysis, string? propertyName, bool isCallable, bool hasOldVariable)
    {
        if (!interfaceAnalysis.CanCallRaiseMethod)
        {
            return;
        }

        this.writer.Write($"this.{interfaceAnalysis.RaiseMethodName}(");

        switch (interfaceAnalysis.RaiseMethodSignature.NameType)
        {
            case RaisePropertyChangedOrChangingNameType.PropertyChangedEventArgs:
                string cacheName = this.eventArgsCache.Get(propertyName, interfaceAnalysis.EventArgsFullyQualifiedTypeName);
                this.writer.Write($"global::PropertyChanged.SourceGenerator.Internal.{EventArgsCacheName}.{cacheName}");
                break;

            case RaisePropertyChangedOrChangingNameType.String:
                this.writer.Write(EscapeString(propertyName));
                break;
        }

        if (interfaceAnalysis.RaiseMethodSignature.HasOld)
        {
            if (isCallable && hasOldVariable)
            {
                this.writer.Write($", old_{propertyName}");
            }
            else
            {
                this.writer.Write(", (object)null");
            }
        }
        if (interfaceAnalysis.RaiseMethodSignature.HasNew)
        {
            if (isCallable)
            {
                this.writer.Write($", new_{propertyName}");
            }
            else
            {
                this.writer.Write(", (object)null");
            }
        }

        this.writer.WriteLine(");");
    }

    private void GenerateOnPropertyNameChangedOrChangingIfNecessary<T>(
        T member,
        OnPropertyNameChangedInfo? onPropertyNameChangedInfo,
        bool hasOldVariable) where T : IMember
    {
        if (onPropertyNameChangedInfo != null)
        {
            this.writer.Write($"this.{onPropertyNameChangedInfo.Value.Name}(");
            if (onPropertyNameChangedInfo.Value.HasOld)
            {
                if (hasOldVariable)
                {
                    this.writer.Write($"old_{member.Name}");
                }
                else
                {
                    this.writer.Write($"default({member.FullyQualifiedTypeName})");
                }
            }
            if (onPropertyNameChangedInfo.Value.HasNew)
            {
                if (onPropertyNameChangedInfo.Value.HasOld)
                {
                    this.writer.Write(", ");
                }
                this.writer.Write($"new_{member.Name}");
            }
            this.writer.WriteLine(");");
        }
    }

    public static EventArgsCache CreateEventArgsCache(IEnumerable<TypeAnalysis> typeAnalyses)
    {
        // Annoyingly, this has to be perfectly synced with the code above which calls EventArgsCache.Get

        var builder = new EventArgsCacheBuilder();

        foreach (var typeAnalysis in typeAnalyses)
        {
            Inspect(typeAnalysis, typeAnalysis.INotifyPropertyChanged);
            Inspect(typeAnalysis, typeAnalysis.INotifyPropertyChanging);
        }

        void Inspect(TypeAnalysis typeAnalysis, InterfaceAnalysis interfaceAnalysis)
        {
            if (interfaceAnalysis.CanCallRaiseMethod &&
                interfaceAnalysis.RaiseMethodSignature.NameType == RaisePropertyChangedOrChangingNameType.PropertyChangedEventArgs)
            {
                foreach (var member in typeAnalysis.Members)
                {
                    builder.Add(member.Name, interfaceAnalysis.EventName, interfaceAnalysis.EventArgsFullyQualifiedTypeName);

                    foreach (var alsoNotify in member.AlsoNotify)
                    {
                        builder.Add(alsoNotify.Name, interfaceAnalysis.EventName, interfaceAnalysis.EventArgsFullyQualifiedTypeName);
                    }
                }

                foreach (var (_, notifyProperty) in typeAnalysis.BaseDependsOn)
                {
                    builder.Add(notifyProperty.Name, interfaceAnalysis.EventName, interfaceAnalysis.EventArgsFullyQualifiedTypeName);
                }
            }
        }

        return builder.ToCache();
    }

    private static (string property, string getter, string setter) CalculateAccessibilities(MemberAnalysis member)
    {
        string property;
        string getter = "";
        string setter = "";

        if (member.GetterAccessibility >= member.SetterAccessibility)
        {
            property = AccessibilityToString(member.GetterAccessibility);
            setter = member.GetterAccessibility == member.SetterAccessibility
                ? ""
                : AccessibilityToString(member.SetterAccessibility);
        }
        else
        {
            property = AccessibilityToString(member.SetterAccessibility);
            getter = AccessibilityToString(member.GetterAccessibility);
        }

        return (property, getter, setter);
    }

    private static string NullableContextToComment(NullableContextOptions context)
    {
        return context switch
        {
            NullableContextOptions.Disable => "#nullable disable",
            NullableContextOptions.Warnings => "#nullable enable warnings",
            NullableContextOptions.Annotations => "#nullable enable annotations",
            NullableContextOptions.Enable => "#nullable enable"
        };
    }

    private static string AccessibilityToString(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public ",
            Accessibility.ProtectedOrInternal => "protected internal ",
            Accessibility.Internal => "internal ",
            Accessibility.Protected => "protected ",
            Accessibility.ProtectedAndInternal => "private protected ",
            Accessibility.Private => "private ",
            _ => throw new ArgumentException("Unknown member", nameof(accessibility)),
        };
    }

    private static string EscapeString(string? str)
    {
        return str == null
            ? "null"
            : "@\"" + str.Replace("\"", "\"\"") + "\"";
    }

    public void GenerateNameCache()
    {
        this.writer.WriteLine("namespace PropertyChanged.SourceGenerator.Internal");
        this.writer.WriteLine("{");
        this.writer.Indent++;

        this.writer.WriteLine($"internal static class {EventArgsCacheName}");
        this.writer.WriteLine("{");
        this.writer.Indent++;

        var backingFieldNames = new HashSet<string>();
        foreach (var (cacheName, eventArgsTypeName, propertyName) in this.eventArgsCache.GetEntries().OrderBy(x => x.cacheName))
        {
            string backingFieldName = "_" + cacheName;
            for (int i = 0; !backingFieldNames.Add(backingFieldName); i++)
            {
                backingFieldName = $"_{cacheName}{i}";
            }
            this.writer.WriteLine($"private static {eventArgsTypeName} {backingFieldName};");
            this.writer.WriteLine($"public static {eventArgsTypeName} {cacheName} => " +
                $"{backingFieldName} ??= new {eventArgsTypeName}({EscapeString(propertyName)});");
        }

        this.writer.Indent--;
        this.writer.WriteLine("}");

        this.writer.Indent--;
        this.writer.WriteLine("}");
    }

    public override string ToString() => this.writer.InnerWriter.ToString();
}
