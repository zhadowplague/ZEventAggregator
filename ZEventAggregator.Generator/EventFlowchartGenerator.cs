using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using ZEventAggregator.Types;

namespace ZEventAggregator.Generator;

[Generator]
public class EventFlowchartGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var methodEdges = context.SyntaxProvider
			.CreateSyntaxProvider(IsCandidateOnEventMethod, GetEventEdgesFromMethod)
			.Collect();

		context.RegisterSourceOutput(methodEdges, WriteFlowchartSource);
	}

	private static bool IsCandidateOnEventMethod(SyntaxNode node, CancellationToken _)
	{
		if (node is not MethodDeclarationSyntax method)
			return false;

		return method.Identifier.ValueText == nameof(IReceive<int>.OnEvent)
			&& method.ParameterList.Parameters.Count == 1
			&& (method.Body is not null || method.ExpressionBody is not null);
	}

	private static ImmutableArray<EventEdge> GetEventEdgesFromMethod(GeneratorSyntaxContext context, CancellationToken cancellationToken)
	{
		if (context.Node is not MethodDeclarationSyntax methodSyntax)
			return ImmutableArray<EventEdge>.Empty;

		if (context.SemanticModel.GetDeclaredSymbol(methodSyntax, cancellationToken) is not IMethodSymbol methodSymbol)
			return ImmutableArray<EventEdge>.Empty;

		if (methodSymbol.Parameters.Length != 1 || methodSymbol.ContainingType is null)
			return ImmutableArray<EventEdge>.Empty;

		var receiveTypes = methodSymbol.ContainingType.AllInterfaces
			.Where(IsReceiveInterface)
			.Select(static x => x.TypeArguments[0])
			.ToImmutableHashSet(SymbolEqualityComparer.Default);

		if (!receiveTypes.Contains(methodSymbol.Parameters[0].Type))
			return ImmutableArray<EventEdge>.Empty;

		var fromEvent = ToDisplayName(methodSymbol.Parameters[0].Type);
		var builder = ImmutableHashSet.CreateBuilder<EventEdge>();

		foreach (var invocation in methodSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
		{
			cancellationToken.ThrowIfCancellationRequested();

			var symbol = context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
			if (symbol is null)
				continue;

			if (!IsFireMethod(symbol))
				continue;

			if (symbol.TypeArguments.Length != 1)
				continue;

			var toEvent = ToDisplayName(symbol.TypeArguments[0]);
			builder.Add(new EventEdge(fromEvent, toEvent));
		}

		return builder.ToImmutableArray();
	}

	private static bool IsReceiveInterface(INamedTypeSymbol interfaceSymbol)
	{
		return interfaceSymbol.Name == nameof(IReceive)
			&& interfaceSymbol.TypeArguments.Length == 1
			&& interfaceSymbol.ContainingNamespace.ToDisplayString() == $"{nameof(ZEventAggregator)}.{nameof(ZEventAggregator.Types)}";
	}

	private static bool IsFireMethod(IMethodSymbol methodSymbol)
	{
		if (methodSymbol.Name != nameof(IEventAggregator.Fire) || methodSymbol.ContainingType is null)
			return false;

		var containingType = methodSymbol.ContainingType;
		if (containingType.ContainingNamespace.ToDisplayString() != $"{nameof(ZEventAggregator)}.{nameof(ZEventAggregator.Types)}")
			return false;

		return containingType.Name == nameof(IEventAggregator) || containingType.Name == nameof(EventAggregator);
	}

	private static string ToDisplayName(ITypeSymbol typeSymbol)
	{
		return typeSymbol
			.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
			.Replace("global::", string.Empty);
	}

	private static void WriteFlowchartSource(SourceProductionContext context, ImmutableArray<ImmutableArray<EventEdge>> input)
	{
		var lines = input
			.SelectMany(static x => x)
			.Select(static x => $"{x.From} -> {x.To}")
			.Distinct(StringComparer.Ordinal)
			.OrderBy(static x => x, StringComparer.Ordinal)
			.ToArray();

		var commentBlock = lines.Length == 0
			? "\t/* No event flow discovered. */"
			: "\t/*\n\t" + string.Join("\n\t", lines) + "\n\t*/";

		var source = $@"
namespace ZEventAggregator.Generator
{{
	public static class ZFlowChart
	{{
{commentBlock}
	}}
}}";

		context.AddSource("ZFlowChart.g.cs", SourceText.From(source, Encoding.UTF8));
	}

	private readonly struct EventEdge : IEquatable<EventEdge>
	{
		public readonly string From;
		public readonly string To;

		public EventEdge(string from, string to)
		{
			From = from;
			To = to;
		}

		public bool Equals(EventEdge other)
		{
			return string.Equals(From, other.From, StringComparison.Ordinal)
				&& string.Equals(To, other.To, StringComparison.Ordinal);
		}

		public override bool Equals(object obj)
		{
			return obj is EventEdge other && Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hash = 17;
				hash = hash * 23 + StringComparer.Ordinal.GetHashCode(From ?? string.Empty);
				hash = hash * 23 + StringComparer.Ordinal.GetHashCode(To ?? string.Empty);
				return hash;
			}
		}
	}
}
