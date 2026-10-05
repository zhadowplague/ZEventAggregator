using ZEventAggregator.Generator;
using ZEventAggregator.Types;

namespace ZEventAggregator.SmokeTest
{
	public readonly ref struct Payload
	{
		public readonly int Foo;
	}

	public class Bar : IReceive<int>
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
