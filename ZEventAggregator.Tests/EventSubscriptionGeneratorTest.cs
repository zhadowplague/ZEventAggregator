using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Linq;
using ZEventAggregator.Generator;
using ZEventAggregator.Types;

namespace ZEventAggregator.Tests;

[TestClass]
public class EventSubscriptionGeneratorTest
{
	[TestMethod]
	public void GeneratesSubscribeMethodForClassesImplementingIReceive()
	{
		const string source = """
			using ZEventAggregator.Types;

			namespace MyNamespace
			{
				public class MyEvent {}
				public class OtherEvent {}

				public partial class Target : IReceive<MyEvent>, IReceive<OtherEvent>
				{
					public void OnEvent(MyEvent @event) { }
					public void OnEvent(OtherEvent @event) { }
				}
			}
			""";

		var syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), "", null, TestContext.CancellationTokenSource.Token);
		var references = AppDomain.CurrentDomain.GetAssemblies()
			.Where(static a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
			.Select(static a => MetadataReference.CreateFromFile(a.Location))
			.Cast<MetadataReference>()
			.Append(MetadataReference.CreateFromFile(typeof(IReceive).Assembly.Location))
			.ToList();

		var compilation = CSharpCompilation.Create(
			assemblyName: "GeneratorTests",
			syntaxTrees: [syntaxTree],
			references: references,
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		GeneratorDriver driver = CSharpGeneratorDriver.Create(new EventSubscriptionGenerator().AsSourceGenerator());
		driver = driver.RunGenerators(compilation, TestContext.CancellationTokenSource.Token);
		var result = driver.GetRunResult();

		Assert.HasCount(1, result.Results);
		Assert.HasCount(1, result.Results[0].GeneratedSources);

		var generatedCode = result.Results[0].GeneratedSources[0].SourceText.ToString();
		Assert.Contains("bus.Listen<global::MyNamespace.MyEvent>(target);", generatedCode);
		Assert.Contains("bus.Listen<global::MyNamespace.OtherEvent>(target);", generatedCode);
	}

	[TestMethod]
	public void GeneratesGodotOverridesWhenAssemblyHasAttribute()
	{
		const string source = """
			using ZEventAggregator.Types;

			[assembly: GodotOverrides]

			namespace Godot
			{
				public class Node
				{
					public virtual void _EnterTree() { }
					public virtual void _ExitTree() { }
				}
			}

			namespace MyNamespace
			{
				public class MyEvent {}

				public partial class Target : Godot.Node, IReceive<MyEvent>
				{
					public void OnEvent(MyEvent @event) { }
				}
			}
			""";

		var result = RunGenerator<GodotEventSubscriptionOverrideGenerator>(source);

		Assert.HasCount(1, result.Results);
		Assert.HasCount(1, result.Results[0].GeneratedSources);

		var generatedCode = result.Results[0].GeneratedSources[0].SourceText.ToString();
		Assert.Contains("public override void _EnterTree()", generatedCode);
		Assert.Contains($"this.ListenToZEvents(global::{nameof(ZEventAggregator)}.{nameof(Types)}.{nameof(EventAggregator)}.{nameof(EventAggregator.Singleton)});", generatedCode);
		Assert.Contains("public override void _ExitTree()", generatedCode);
		Assert.Contains($"this.FreeZEvents(global::{nameof(ZEventAggregator)}.{nameof(Types)}.{nameof(EventAggregator)}.{nameof(EventAggregator.Singleton)});", generatedCode);
	}

	[TestMethod]
	public void DoesNotGenerateGodotOverridesWithoutAssemblyAttribute()
	{
		const string source = """
			using ZEventAggregator.Types;

			namespace Godot
			{
				public class Node
				{
					public virtual void _EnterTree() { }
					public virtual void _ExitTree() { }
				}
			}

			namespace MyNamespace
			{
				public class MyEvent {}

				public partial class Target : Godot.Node, IReceive<MyEvent>
				{
					public void OnEvent(MyEvent @event) { }
				}
			}
			""";

		var result = RunGenerator<GodotEventSubscriptionOverrideGenerator>(source);

		Assert.HasCount(1, result.Results);
		Assert.HasCount(0, result.Results[0].GeneratedSources);
	}

	[TestMethod]
	public void GeneratesMissingGodotOverrideWhenClassAlreadyDefinesOneLifecycleOverride()
	{
		const string source = """
			using ZEventAggregator.Types;

			[assembly: GodotOverrides]

			namespace Godot
			{
				public class Node
				{
					public virtual void _EnterTree() { }
					public virtual void _ExitTree() { }
				}
			}

			namespace MyNamespace
			{
				public class MyEvent {}

				public partial class Target : Godot.Node, IReceive<MyEvent>
				{
					public override void _EnterTree() { }
					public void OnEvent(MyEvent @event) { }
				}
			}
			""";

		var result = RunGenerator<GodotEventSubscriptionOverrideGenerator>(source);

		Assert.HasCount(1, result.Results);
		Assert.HasCount(1, result.Results[0].GeneratedSources);

		var generatedCode = result.Results[0].GeneratedSources[0].SourceText.ToString();
		Assert.DoesNotContain("public override void _EnterTree()", generatedCode);
		Assert.Contains("public override void _ExitTree()", generatedCode);
	}

	private GeneratorDriverRunResult RunGenerator<TGenerator>(string source)
		where TGenerator : IIncrementalGenerator, new()
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), "", null, TestContext.CancellationTokenSource.Token);
		var references = AppDomain.CurrentDomain.GetAssemblies()
			.Where(static a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
			.Select(static a => MetadataReference.CreateFromFile(a.Location))
			.Cast<MetadataReference>()
			.Append(MetadataReference.CreateFromFile(typeof(IReceive).Assembly.Location))
			.ToList();

		var compilation = CSharpCompilation.Create(
			assemblyName: "GeneratorTests",
			syntaxTrees: [syntaxTree],
			references: references,
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		GeneratorDriver driver = CSharpGeneratorDriver.Create(new TGenerator().AsSourceGenerator());
		driver = driver.RunGenerators(compilation, TestContext.CancellationTokenSource.Token);
		return driver.GetRunResult();
	}

	public TestContext TestContext { get; set; }
}
