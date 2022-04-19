using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator;


public class MethodSignatureSet<T>
{
    private readonly IReadOnlyList<MethodSignature<T>> methodSignatures;

    public MethodSignatureSet(params MethodSignature<T>[] methodSignatures)
        : this((IReadOnlyList<MethodSignature<T>>)methodSignatures) { }

    public MethodSignatureSet(IReadOnlyList<MethodSignature<T>> methodSignatures)
    {
        this.methodSignatures = methodSignatures;
    }

    public bool TryFindMatchingOverload(
        IEnumerable<IMethodSymbol> methods,
        ITypeSymbol callingType,
        Compilation compilation,
        out IMethodSymbol? foundMethod,
        out T? result)
    {
        var methodsList = methods.Where(x => this.IsAccessibleNormalMethod(x, callingType, compilation)).ToList();

        foreach (var methodSignature in this.methodSignatures)
        {
            foreach (var method in methodsList)
            {
                if (methodSignature.Matches(method))
                {
                    foundMethod = method;
                    result = methodSignature.ResultFactory(method);
                    return true;
                }
            }
        }

        foundMethod = null;
        result = default;
        return false;
    }

    private bool IsAccessibleNormalMethod(IMethodSymbol method, ITypeSymbol typeSymbol, Compilation compilation) =>
        !method.IsGenericMethod &&
        method.ReturnsVoid &&
        compilation.IsSymbolAccessibleWithin(method, typeSymbol);
}

//public static class MethodSignature
//{
//    public static MethodSignature<T> Create<T>(T result, params ParameterSignature[] parameters) =>
//        new MethodSignature<T>(parameters, result);
//}

public class MethodSignature<T>
{
    private readonly IReadOnlyList<ParameterSignature> parameters;

    public Func<IMethodSymbol, T> ResultFactory { get; }

    public MethodSignature(Func<IMethodSymbol, T> resultFactory, params ParameterSignature[] parameters)
    {
        this.parameters = parameters;
        this.ResultFactory = resultFactory;
    }

    public bool Matches(IMethodSymbol method)
    {
        if (method.Parameters.Length != this.parameters.Count)
        {
            return false;
        }

        for (int i = 0; i < method.Parameters.Length; i++)
        {
            if (!this.parameters[i].Matches(method.Parameters[i]))
            {
                return false;
            }    
        }

        return true;
    }
}

public abstract class ParameterSignature
{
    public bool Matches(IParameterSymbol parameter)
    {
        if (!IsNormalParameter(parameter))
        {
            return false;
        }

        return this.MatchesImpl(parameter);
    }

    private static bool IsNormalParameter(IParameterSymbol parameter) =>
        parameter.RefKind == RefKind.None;

    protected abstract bool MatchesImpl(IParameterSymbol parameter);
}

public class SpecialTypeParameterSignature : ParameterSignature
{
    private readonly SpecialType specialType;

    public SpecialTypeParameterSignature(SpecialType specialType) => this.specialType = specialType;

    protected override bool MatchesImpl(IParameterSymbol parameter) => parameter.Type.SpecialType == this.specialType;
}
