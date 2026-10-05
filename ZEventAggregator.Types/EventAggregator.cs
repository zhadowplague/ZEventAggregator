using System;
using System.Collections.Generic;

namespace ZEventAggregator.Types;

public class EventAggregator : IEventAggregator
{
	readonly Dictionary<Type, List<IReceive>> _subscribers = new();

#if NETSTANDARD
	public void Fire<T>(T @event)
#else
	public void Fire<T>(T @event) where T : allows ref struct
#endif
	{
		if (_subscribers.TryGetValue(typeof(T), out var subs))
		{
			foreach (var sub in subs)
			{
				(sub as IReceive<T>).OnEvent(@event);
			}
		}
	}

#if NETSTANDARD
	public void Free<T>(IReceive<T> obj)
#else
	public void Free<T>(IReceive<T> obj) where T : allows ref struct
#endif
	{
		var type = typeof(T);
		if (_subscribers.TryGetValue(type, out var value))
			value.Remove(obj);
	}

#if NETSTANDARD
	public void Listen<T>(IReceive<T> obj)
#else
	public void Listen<T>(IReceive<T> obj) where T : allows ref struct
#endif
	{
		var type = typeof(T);
		if (!_subscribers.ContainsKey(type))
			_subscribers[type] = [];
		_subscribers[type].Add(obj);
	}
}
