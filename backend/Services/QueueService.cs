using System.Collections.Concurrent;

namespace Backend.Services;

public class QueueService<T> where T : class
{
  private readonly ConcurrentQueue<T> _queue = new();
  private volatile int _count = 0;

  public void Enqueue(T item)
  {
    _queue.Enqueue(item);
    Interlocked.Increment(ref _count);
  }

  public bool TryDequeue(out T? item)
  {
    if (_queue.TryDequeue(out item))
    {
      Interlocked.Decrement(ref _count);
      return true;
    }
    return false;
  }

  public T? Peek()
  {
    _queue.TryPeek(out var item);
    return item;
  }

  public int Count => _count;

  public bool IsEmpty => _count == 0;

  public void Clear()
  {
    while (_queue.TryDequeue(out _))
    {
      Interlocked.Decrement(ref _count);
    }
  }

  public List<T> DequeueMultiple(int maxCount)
  {
    var items = new List<T>();
    var actualCount = Math.Min(maxCount, _count);

    for (int i = 0; i < actualCount; i++)
    {
      if (TryDequeue(out var item) && item != null)
      {
        items.Add(item);
      }
      else
      {
        break;
      }
    }

    return items;
  }
}
