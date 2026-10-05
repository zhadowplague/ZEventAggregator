using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
public class EventSubscriptionGenerator : IIncrementalGenerator
{

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var candidateClasses = context.SyntaxProvider.CreateSyntaxProvider(IsCandidateClass, GetReceiverInfo)
			.Where(static x => x is not null);

		var collected = candidateClasses.Collect();
		context.RegisterSourceOutput(collected, GenerateOutput);
	}

	private static bool IsCandidateClass(SyntaxNode node, CancellationToken _)
	{
		return node is ClassDeclarationSyntax classDeclaration && classDeclaration.BaseList is not null;
	}

	private static ReceiverInfo? GetReceiverInfo(GeneratorSyntaxContext ctx, CancellationToken _)
	{
		var classSyntax = (ClassDeclarationSyntax)ctx.Node;
		if (ctx.SemanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
			return null;

		var eventTypes = classSymbol.AllInterfaces
			.Where(IsReceiveInterface)
			.Select(static x => x.TypeArguments[0])
			.ToImmutableArray();

		if (eventTypes.IsDefaultOrEmpty)
			return null;

		return new ReceiverInfo(classSymbol, eventTypes);
	}

	private static bool IsReceiveInterface(INamedTypeSymbol interfaceSymbol)
	{
		return interfaceSymbol.Name == nameof(IReceive)
			&& interfaceSymbol.TypeArguments.Length == 1
			&& interfaceSymbol.ContainingNamespace.ToDisplayString() == $"{nameof(ZEventAggregator)}.{nameof(ZEventAggregator.Types)}";
	}

	private static void GenerateOutput(SourceProductionContext spc, ImmutableArray<ReceiverInfo?> receivers)
	{
		var grouped = new Dictionary<INamedTypeSymbol, HashSet<ITypeSymbol>>(SymbolEqualityComparer.Default);

		foreach (var receiver in receivers)
		{
			if (!receiver.HasValue)
				continue;

			var receiverValue = receiver.Value;

			if (!grouped.TryGetValue(receiverValue.ContainingClass, out var list))
			{
				list = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
				grouped[receiverValue.ContainingClass] = list;
			}

			foreach (var eventType in receiverValue.EventTypes)
				list.Add(eventType);
		}

		foreach (var kvp in grouped)
		{
			var eventTypeNames = kvp.Value
				.Select(static x => x.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
				.OrderBy(static x => x)
				.ToList();

			var source = GeneratePropertyCode(kvp.Key, eventTypeNames);
			var hintName = $"{SanitizeName(kvp.Key.ToDisplayString())}_EventSubscription.g.cs";
			spc.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
		}
	}

	private static string SanitizeName(string name)
	{
		var builder = new StringBuilder(name.Length);
		foreach (var c in name)
			builder.Append(char.IsLetterOrDigit(c) ? c : '_');

		return builder.ToString();
	}

	public static string GeneratePropertyCode(INamedTypeSymbol classSymbol, List<string> eventTypes)
	{
		var containingNamespace = classSymbol.ContainingNamespace.IsGlobalNamespace
			? string.Empty
			: classSymbol.ContainingNamespace.ToDisplayString();

		var typeName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var extensionClassName = $"{SanitizeName(classSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))}{EventSubscribingConstants.SubscriptionExtensionClassName}";
		var listenLines = string.Join("\r\n            ", eventTypes.Select(static x => $"bus.{nameof(EventAggregator.Listen)}<{x}>(target);"));
		var freeLines = string.Join("\r\n            ", eventTypes.Select(static x => $"bus.{nameof(EventAggregator.Free)}<{x}>(target);"));

		if (string.IsNullOrWhiteSpace(containingNamespace))
		{
			return $@"
internal static class {extensionClassName}
{{
	internal static void {EventSubscribingConstants.SubscriptionMethodName}(this {typeName} target, global::{nameof(ZEventAggregator)}.{nameof(ZEventAggregator.Types)}.{nameof(IEventAggregator)} bus)
	{{
		{listenLines}
	}}
	internal static void {EventSubscribingConstants.UnSubscriptionMethodName}(this {typeName} target, global::{nameof(ZEventAggregator)}.{nameof(ZEventAggregator.Types)}.{nameof(IEventAggregator)} bus)
	{{
		{freeLines}
	}}
}}";
		}

		return $@"
namespace {containingNamespace}
{{
	internal static class {extensionClassName}
	{{
		internal static void {EventSubscribingConstants.SubscriptionMethodName}(this {typeName} target, global::{nameof(ZEventAggregator)}.{nameof(ZEventAggregator.Types)}.{nameof(IEventAggregator)} bus)
		{{
			{listenLines}
		}}
		internal static void {EventSubscribingConstants.UnSubscriptionMethodName}(this {typeName} target, global::{nameof(ZEventAggregator)}.{nameof(ZEventAggregator.Types)}.{nameof(IEventAggregator)} bus)
		{{
			{freeLines}
		}}
	}}
}}";
	}

	public readonly struct ReceiverInfo(INamedTypeSymbol ContainingClass, ImmutableArray<ITypeSymbol> EventTypes)
	{
		public readonly INamedTypeSymbol ContainingClass = ContainingClass;
		public readonly ImmutableArray<ITypeSymbol> EventTypes = EventTypes;
	}
}
