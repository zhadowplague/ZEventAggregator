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

	public TestContext TestContext { get; set; }
}
