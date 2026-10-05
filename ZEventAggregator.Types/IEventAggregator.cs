namespace ZEventAggregator.Types
{
	public interface IEventAggregator
	{
#if NETSTANDARD
		void Fire<T>(T @event);
		void Free<T>(IReceive<T> obj);
		void Listen<T>(IReceive<T> obj);
#else
		void Fire<T>(T @event) where T : allows ref struct;
		void Free<T>(IReceive<T> obj) where T : allows ref struct;
		void Listen<T>(IReceive<T> obj) where T : allows ref struct;
#endif
	}
}