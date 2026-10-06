using ZEventAggregator.Generator;
using ZEventAggregator.Types;

[assembly:GodotOverrides]

namespace ZEventAggregator.SmokeTest
{
	public readonly ref struct Payload
	{
		public readonly int Foo;
	}

	public partial class Node
	{
#pragma warning disable IDE1006 // Naming Styles
		public virtual void _EnterTree() { }
		public virtual void _ExitTree() { }
#pragma warning restore IDE1006 // Naming Styles
	}

	public partial class Bar : Node, IReceive<int>
	{
		readonly IEventAggregator _test;

		public Bar()
		{
			_test = new EventAggregator();
			this.ListenToZEvents(_test);
			this.FreeZEvents(_test);
			Console.WriteLine(nameof(ZFlowChart));
		}

		public void OnEvent(int @event)
		{
			_test.Fire<Payload>(new());
		}
	}
}
