namespace ZEventAggregator.Types;

public interface IReceive { }
#if NETSTANDARD
public interface IReceive<T> : IReceive
#else
public interface IReceive<T> : IReceive where T : allows ref struct
#endif
{
	/// <summary>
	/// Event receiver callback, should not be called manually
	/// </summary>
	/// <param name="event"></param>
	void OnEvent(T @event);
}
