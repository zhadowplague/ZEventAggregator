using ZEventAggregator.Types;

namespace ZEventAggregator.SmokeTest
{
	public class Class1 : IReceive<int>
	{
		public Class1()
		{
			var test = new EventAggregator();
			this.ListenToZEvents(test);
			this.FreeZEvents(test);
		}

		public void OnEvent(int @event)
		{
			
		}
	}
}
