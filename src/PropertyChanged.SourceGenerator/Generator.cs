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

        if (typeAnalysis.TypeSymbol.ContainingNamespace is { IsGlobalNamespace: false } @namespace)
        {
            this.writer.WriteLine($"namespace {@namespace.ToDisplayString(SymbolDisplayFormats.Namespace)}");
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
        var outerTypes = new List<INamedTypeSymbol>();
        for (var outerType = typeAnalysis.TypeSymbol.ContainingType; outerType != null; outerType = outerType.ContainingType)
        {
            outerTypes.Add(outerType);
        }
        foreach (var outerType in outerTypes.AsEnumerable().Reverse())
        {
            this.writer.WriteLine($"partial {outerType.ToDisplayString(SymbolDisplayFormats.TypeDeclaration)}");
            this.writer.WriteLine("{");
            this.writer.Indent++;
        }

        this.writer.Write($"partial {typeAnalysis.TypeSymbol.ToDisplayString(SymbolDisplayFormats.TypeDeclaration)}");
        if (typeAnalysis.INotifyPropertyChanged.RequiresInterface)
        {
            this.writer.Write(" : global::System.ComponentModel.INotifyPropertyChanged");
        }
        this.writer.WriteLine();
        
        this.writer.WriteLine("{");
        this.writer.Indent++;

        if (typeAnalysis.INotifyPropertyChanged.RequiresEvent)
        {
            string nullable = typeAnalysis.NullableContext.HasFlag(NullableContextOptions.Annotations) ? "?" : "";
            this.writer.WriteLine($"public event global::System.ComponentModel.PropertyChangedEventHandler{nullable} PropertyChanged;");
        }

        foreach (var member in typeAnalysis.Members)
        {
            this.GenerateMember(typeAnalysis, member);
        }

        this.GenerateRaisePropertyChangedMethod(typeAnalysis);
        
        this.writer.Indent--;
        this.writer.WriteLine("}");

        for (int i = 0; i < outerTypes.Count; i++)
        {
            this.writer.Indent--;
            this.writer.WriteLine("}");
        }
    }

    private void GenerateRaisePropertyChangedMethod(TypeAnalysis typeAnalysis)
    {
        var interfaceAnalysis = typeAnalysis.INotifyPropertyChanged;
        var baseDependsOn = typeAnalysis.BaseDependsOn.ToLookup(x => x.baseProperty);
        if (interfaceAnalysis.RaiseMethodType == RaisePropertyChangedMethodType.None ||
            (interfaceAnalysis.RaiseMethodType == RaisePropertyChangedMethodType.Override &&
            baseDependsOn.Count == 0 &&
            typeAnalysis.INotifyPropertyChanged.OnAnyPropertyChangedInfo == null))
        {
            return;
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
            case RaisePropertyChangedNameType.String:
                this.writer.Write("string propertyName");
                propertyNameOrEventArgsName = "propertyName";
                propertyNameAccessor = "propertyName";
                break;
            case RaisePropertyChangedNameType.PropertyChangedEventArgs:
                this.writer.Write("global::System.ComponentModel.PropertyChangedEventArgs eventArgs");
                propertyNameOrEventArgsName = "eventArgs";
                propertyNameAccessor = "eventArgs.PropertyName";
                break;
        }
        if (interfaceAnalysis.RaiseMethodSignature.HasOldAndNew)
        {
            this.writer.Write(", object oldValue, object newValue");
        }
        this.writer.WriteLine(")");

        this.writer.WriteLine("{");
        this.writer.Indent++;

        if (typeAnalysis.INotifyPropertyChanged.OnAnyPropertyChangedInfo is { } onAnyPropertyChangedInfo)
        {
            this.writer.Write($"this.{onAnyPropertyChangedInfo.Name}({propertyNameAccessor}");
            switch (onAnyPropertyChangedInfo.Signature)
            {
                case OnPropertyNameChangedSignature.Parameterless:
                    break;
                case OnPropertyNameChangedSignature.OldAndNew:
                    if (interfaceAnalysis.RaiseMethodSignature.HasOldAndNew)
                    {
                        this.writer.Write(", oldValue, newValue");
                    }
                    else
                    {
                        this.writer.Write(", (object)null, (object)null");
                    }
                    break;
            }
            this.writer.WriteLine(");");
        }

        switch (interfaceAnalysis.RaiseMethodType)
        {
            case RaisePropertyChangedMethodType.Virtual:
            case RaisePropertyChangedMethodType.NonVirtual:
                // If we're generating our own, we always use PropertyChangedEventArgs
                Trace.Assert(interfaceAnalysis.RaiseMethodSignature.NameType == RaisePropertyChangedNameType.PropertyChangedEventArgs);
                this.writer.WriteLine("this.PropertyChanged?.Invoke(this, eventArgs);");
                break;
            case RaisePropertyChangedMethodType.Override:
                this.writer.Write($"base.{interfaceAnalysis.RaiseMethodName}({propertyNameOrEventArgsName}");
                if (interfaceAnalysis.RaiseMethodSignature.HasOldAndNew)
                {
                    this.writer.Write(", oldValue, newValue");
                }
                this.writer.WriteLine(");");
                break;
            case RaisePropertyChangedMethodType.None:
                break;
        }

        if (baseDependsOn.Count > 0)
        {
            this.writer.WriteLine($"switch ({propertyNameAccessor})");
            this.writer.WriteLine("{");
            this.writer.Indent++;

            foreach (var group in baseDependsOn)
            {
                this.writer.WriteLine($"case {EscapeString(group.Key)}:");
                this.writer.Indent++;
                foreach (var (_, notifyProperty) in group)
                {
                    this.GenerateOnPropertyNameChangedIfNecessary(notifyProperty, hasOldVariable: false);
                    this.GenerateRaiseEvent(typeAnalysis, notifyProperty.Name, notifyProperty.IsCallable, hasOldVariable: false);
                }
                this.writer.WriteLine("break;");
                this.writer.Indent--;
            }

            this.writer.Indent--;
            this.writer.WriteLine("}");
        }

        this.writer.Indent--;
        this.writer.WriteLine("}");
    }

    private void GenerateMember(TypeAnalysis type, MemberAnalysis member)
    {
        string backingMemberReference = "this." + member.BackingMember.ToDisplayString(SymbolDisplayFormats.SymbolName);

        if (member.NullableContextOverride is { } context)
        {
            this.writer.WriteLine(NullableContextToComment(context));
        }

        var (propertyAccessibility, getterAccessibility, setterAccessibility) = CalculateAccessibilities(member);

        if (member.DocComment != null)
        {
            foreach (string line in member.DocComment)
            {
                this.writer.WriteLine($"/// {line}");
            }
        }

        this.writer.WriteLine($"{propertyAccessibility}{member.Type.ToDisplayString(SymbolDisplayFormats.MethodOrPropertyReturnType)} {member.Name}");
        this.writer.WriteLine("{");
        this.writer.Indent++;

        this.writer.WriteLine($"{getterAccessibility}get => {backingMemberReference};");
        this.writer.WriteLine($"{setterAccessibility}set");
        this.writer.WriteLine("{");
        this.writer.Indent++;

        this.writer.WriteLine("if (!global::System.Collections.Generic.EqualityComparer<" +
            member.Type.ToDisplayString(SymbolDisplayFormats.TypeParameter) +
            $">.Default.Equals(value, {backingMemberReference}))");
        this.writer.WriteLine("{");
        this.writer.Indent++;

        this.GenerateOldVariableIfNecessary(type, member);
        foreach (var alsoNotify in member.AlsoNotify)
        {
            this.GenerateOldVariableIfNecessary(type, alsoNotify);
        }

        this.writer.WriteLine($"{backingMemberReference} = value;");

        this.GenerateOnPropertyNameChangedIfNecessary(member, hasOldVariable: true);
        this.GenerateRaiseEvent(type, member.Name, isCallable: true, hasOldVariable: true);
        foreach (var alsoNotify in member.AlsoNotify.OrderBy(x => x.Name))
        {
            this.GenerateOnPropertyNameChangedIfNecessary(alsoNotify, hasOldVariable: true);
            this.GenerateRaiseEvent(type, alsoNotify.Name, alsoNotify.IsCallable, hasOldVariable: true);
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

    private void GenerateOldVariableIfNecessary<T>(TypeAnalysis type, T member) where T : IMember
    {
        if (member.IsCallable &&
            (type.INotifyPropertyChanged.RaiseMethodSignature.HasOldAndNew ||
                member.OnPropertyNameChanged?.Signature == OnPropertyNameChangedSignature.OldAndNew))
        {
            this.writer.WriteLine($"{member.Type!.ToDisplayString(SymbolDisplayFormats.MethodOrPropertyReturnType)} old_{member.Name} = this.{member.Name};");
        }
    }

    private void GenerateRaiseEvent(TypeAnalysis type, string? propertyName, bool isCallable, bool hasOldVariable)
    {
        if (!type.INotifyPropertyChanged.CanCallRaiseMethod)
        {
            return;
        }

        this.writer.Write($"this.{type.INotifyPropertyChanged.RaiseMethodName}(");

        switch (type.INotifyPropertyChanged.RaiseMethodSignature.NameType)
        {
            case RaisePropertyChangedNameType.PropertyChangedEventArgs:
                string cacheName = this.eventArgsCache.GetOrAdd(propertyName);
                this.writer.Write($"global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.{cacheName}");
                break;

            case RaisePropertyChangedNameType.String:
                this.writer.Write(EscapeString(propertyName));
                break;
        }

        if (type.INotifyPropertyChanged.RaiseMethodSignature.HasOldAndNew)
        {
            if (isCallable)
            {
                this.writer.Write(", ");
                if (hasOldVariable)
                {
                    this.writer.Write($"old_{propertyName}");
                }
                else
                {
                    this.writer.Write("(object)null");
                }
                this.writer.Write($", this.{propertyName}");
            }
            else
            {
                this.writer.Write(", (object)null, (object)null");
            }
        }
        this.writer.WriteLine(");");
    }

    private void GenerateOnPropertyNameChangedIfNecessary<T>(T member, bool hasOldVariable) where T : IMember
    {
        if (member.OnPropertyNameChanged != null)
        {
            this.writer.Write($"this.{member.OnPropertyNameChanged.Value.Name}(");
            switch (member.OnPropertyNameChanged.Value.Signature)
            {
                case OnPropertyNameChangedSignature.Parameterless:
                    break;
                case OnPropertyNameChangedSignature.OldAndNew:
                    if (hasOldVariable)
                    {
                        this.writer.Write($"old_{member.Name}");
                    }
                    else
                    {
                        this.writer.Write($"default({member.Type!.ToDisplayString(SymbolDisplayFormats.MethodOrPropertyReturnType)})");
                    }
                    this.writer.Write($", this.{member.Name}");
                    break;
            }
            this.writer.WriteLine(");");
        }
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

        this.writer.WriteLine("internal static class PropertyChangedEventArgsCache");
        this.writer.WriteLine("{");
        this.writer.Indent++;

        foreach (var (cacheName, propertyName) in this.eventArgsCache.GetEntries().OrderBy(x => x.cacheName))
        {
            string backingFieldName = "_" + cacheName;
            for (int i = 0; this.eventArgsCache.ContainsCacheName(backingFieldName); i++)
            {
                backingFieldName = $"_{cacheName}{i}";
            }
            this.writer.WriteLine($"private static global::System.ComponentModel.PropertyChangedEventArgs {backingFieldName};");
            this.writer.WriteLine($"public static global::System.ComponentModel.PropertyChangedEventArgs {cacheName} => " +
                $"{backingFieldName} ??= new global::System.ComponentModel.PropertyChangedEventArgs({EscapeString(propertyName)});");
        }

        this.writer.Indent--;
        this.writer.WriteLine("}");

        this.writer.Indent--;
        this.writer.WriteLine("}");
    }

    public override string ToString() => this.writer.InnerWriter.ToString();
}
