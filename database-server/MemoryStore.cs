using Backend.Models;

namespace Backend.Database;

public class MemoryStore
{
  private const int AmountMask = 0x7ff;
  private const int TimestampMask = 0x1fffff;

  private readonly long _createdAt;
  private readonly List<int> _items = [];
  private readonly object _lock = new();

  public MemoryStore()
  {
    _createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  }

  private int Pack(decimal amount, long timestampMs)
  {
    var cents = (int)(amount * 100 + 0.5m);

    if (cents > AmountMask)
    {
      throw new ArgumentException($"Amount too high: maximum R$ {AmountMask / 100.0:F2} (current: R$ {amount:F2})");
    }

    var rel = timestampMs - _createdAt;

    if (rel < 0 || rel > TimestampMask)
    {
      throw new ArgumentException($"Timestamp out of range: maximum {TimestampMask}ms (~{TimestampMask / 60000.0:F1} min)");
    }

    return (int)(((uint)rel << 11) | (uint)cents);
  }

  private (decimal Amount, long Timestamp) Unpack(int packed)
  {
    var cents = packed & AmountMask;
    var rel = ((uint)packed >> 11) & TimestampMask;

    return (
        Amount: cents * 0.01m,
        Timestamp: _createdAt + rel
    );
  }

  public void Add(long timestampMs, decimal value)
  {
    var packed = Pack(value, timestampMs);

    lock (_lock)
    {
      _items.Add(packed);
    }

    Console.WriteLine($"add {DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}");
  }

  public List<StoredItem> GetAll()
  {
    var result = new List<StoredItem>();

    lock (_lock)
    {
      foreach (var packed in _items)
      {
        var unpacked = Unpack(packed);
        result.Add(new StoredItem(
            Timestamp: unpacked.Timestamp,
            Value: unpacked.Amount,
            Processor: ProcessorType.Default
        ));
      }
    }

    return result;
  }

  public void Clear()
  {
    lock (_lock)
    {
      _items.Clear();
    }
  }
}

public record StoredItem(
    long Timestamp,
    decimal Value,
    ProcessorType Processor
);
