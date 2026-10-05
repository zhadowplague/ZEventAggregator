using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;

namespace ZEventAggregator.Generator;

public static class SyntaxMapper
{
	public static AttributeSyntax AttributeFromType(Type attributeType)
	{
		return SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(attributeType.Name));
	}

	public static INamedTypeSymbol GetKnownSymbol(Compilation compilation, Type t)
	{
		return (INamedTypeSymbol)compilation.GetTypeByMetadataName(t.FullName);
	}
}
