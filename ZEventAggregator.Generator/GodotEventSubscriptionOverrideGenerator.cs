using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using ZEventAggregator.Types;

namespace ZEventAggregator.Generator;

[Generator]
public class GodotEventSubscriptionOverrideGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var candidates = context.SyntaxProvider
			.CreateSyntaxProvider(IsCandidateClass, GetCandidateInfo)
			.Where(static x => x is not null)
			.Collect();

		var hasGodotOverridesAttribute = context.CompilationProvider
			.Select(static (compilation, _) => HasGodotOverridesAttribute(compilation.Assembly));

		context.RegisterSourceOutput(hasGodotOverridesAttribute.Combine(candidates), GenerateOutput);
	}

	private static bool IsCandidateClass(SyntaxNode node, CancellationToken _)
	{
		return node is ClassDeclarationSyntax;
	}

	private static CandidateInfo? GetCandidateInfo(GeneratorSyntaxContext context, CancellationToken _)
	{
		if (context.Node is not ClassDeclarationSyntax classSyntax)
			return null;

		if (context.SemanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
			return null;

		if (classSymbol.ContainingType is not null)
			return null;

		if (!InheritsFromGodotNode(classSymbol))
			return null;

		if (!classSymbol.AllInterfaces.Any(IsReceiveInterface))
			return null;

		var methods = classSymbol.GetMembers().OfType<IMethodSymbol>();
		var hasEnterTree = methods.Any(static x => x.Name == "_EnterTree" && x.Parameters.Length == 0);
		var hasExitTree = methods.Any(static x => x.Name == "_ExitTree" && x.Parameters.Length == 0);

		return new CandidateInfo(classSymbol, hasEnterTree, hasExitTree);
	}

	private static bool IsReceiveInterface(INamedTypeSymbol interfaceSymbol)
	{
		return interfaceSymbol.Name == nameof(IReceive)
			&& interfaceSymbol.TypeArguments.Length == 1
			&& interfaceSymbol.ContainingNamespace.ToDisplayString() == $"{nameof(ZEventAggregator)}.{nameof(ZEventAggregator.Types)}";
	}

	private static bool InheritsFromGodotNode(INamedTypeSymbol classSymbol)
	{
		for (var baseType = classSymbol.BaseType; baseType is not null; baseType = baseType.BaseType)
		{
			if (baseType.Name == "Node" && baseType.ContainingNamespace.ToDisplayString() == "Godot")
				return true;
		}

		return false;
	}

	private static bool HasGodotOverridesAttribute(IAssemblySymbol assembly)
	{
		return assembly.GetAttributes().Any(static x =>
			x.AttributeClass?.Name == nameof(GodotOverrides)
			&& x.AttributeClass.ContainingNamespace.ToDisplayString() == $"{nameof(ZEventAggregator)}.{nameof(ZEventAggregator.Types)}");
	}

	private static void GenerateOutput(SourceProductionContext context, (bool HasGodotOverrides, ImmutableArray<CandidateInfo?> Candidates) input)
	{
		if (!input.HasGodotOverrides)
			return;

		var grouped = new Dictionary<INamedTypeSymbol, CandidateInfo>(SymbolEqualityComparer.Default);
		foreach (var candidate in input.Candidates)
		{
			if (!candidate.HasValue)
				continue;

			var value = candidate.Value;
			if (grouped.TryGetValue(value.ContainingClass, out var existing))
			{
				grouped[value.ContainingClass] = new CandidateInfo(value.ContainingClass, existing.HasEnterTree || value.HasEnterTree, existing.HasExitTree || value.HasExitTree);
				continue;
			}

			grouped[value.ContainingClass] = value;
		}

		foreach (var candidate in grouped.Values)
		{
			if (candidate.HasEnterTree && candidate.HasExitTree)
				continue;

			var source = GenerateOverrideCode(candidate.ContainingClass, candidate.HasEnterTree, candidate.HasExitTree);
			var hintName = $"{SanitizeName(candidate.ContainingClass.ToDisplayString())}_GodotSubscriptionOverrides.g.cs";
			context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
		}
	}

	private static string GenerateOverrideCode(INamedTypeSymbol classSymbol, bool hasEnterTree, bool hasExitTree)
	{
		var containingNamespace = classSymbol.ContainingNamespace.IsGlobalNamespace
			? string.Empty
			: classSymbol.ContainingNamespace.ToDisplayString();

		var typeParameters = classSymbol.TypeParameters.Length == 0
			? string.Empty
			: "<" + string.Join(", ", classSymbol.TypeParameters.Select(static x => x.Name)) + ">";

		var enterTreeMethod = hasEnterTree
			? string.Empty
			: $@"
		public override void _EnterTree()
		{{
			this.{EventSubscribingConstants.SubscriptionMethodName}(global::{nameof(ZEventAggregator)}.{nameof(Types)}.{nameof(EventAggregator)}.{nameof(EventAggregator.Singleton)});
		}}";

		var exitTreeMethod = hasExitTree
			? string.Empty
			: $@"
		public override void _ExitTree()
		{{
			this.{EventSubscribingConstants.UnSubscriptionMethodName}(global::{nameof(ZEventAggregator)}.{nameof(Types)}.{nameof(EventAggregator)}.{nameof(EventAggregator.Singleton)});
		}}";

		var source = $@"
{(string.IsNullOrWhiteSpace(containingNamespace) ? string.Empty : $"namespace {containingNamespace}\n{{")}
	partial class {classSymbol.Name}{typeParameters}
	{{
		{enterTreeMethod}
		{exitTreeMethod}
	}}
{(string.IsNullOrWhiteSpace(containingNamespace) ? string.Empty : "}")}";

		return source;
	}

	private static string SanitizeName(string name)
	{
		var builder = new StringBuilder(name.Length);
		foreach (var c in name)
			builder.Append(char.IsLetterOrDigit(c) ? c : '_');

		return builder.ToString();
	}

	public readonly struct CandidateInfo(INamedTypeSymbol ContainingClass, bool HasEnterTree, bool HasExitTree)
	{
		public readonly INamedTypeSymbol ContainingClass = ContainingClass;
		public readonly bool HasEnterTree = HasEnterTree;
		public readonly bool HasExitTree = HasExitTree;
	}
}
