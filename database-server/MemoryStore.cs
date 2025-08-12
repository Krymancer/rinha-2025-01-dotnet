namespace Backend.Database;

public record StoredItem(
    long Timestamp,
    decimal Value,
    string Processor
);

public class MemoryStore
{
  private const int AmountMask = 0xffff;      // 16 bits for amount (max $655.35)
  private const int TimestampMask = 0xffff;   // 16 bits for timestamp (max ~18 hours)

  private readonly long _createdAt;
  private readonly List<int> _items = [];
  private readonly List<string> _processors = [];
  private readonly object _lock = new();

  public MemoryStore()
  {
    _createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  }

  private int Pack(decimal amount, long timestampMs)
  {
    var cents = (int)Math.Round(amount * 100, 0, MidpointRounding.AwayFromZero);

    if (cents > AmountMask)
    {
      throw new ArgumentException($"Amount too high: maximum ${AmountMask / 100.0:F2} (current: ${amount:F2})");
    }

    var rel = timestampMs - _createdAt;

    if (rel < 0 || rel > TimestampMask)
    {
      throw new ArgumentException($"Timestamp out of range: maximum {TimestampMask}ms");
    }

    return ((int)rel << 16) | cents;
  }

  private (decimal Amount, long Timestamp) Unpack(int packed)
  {
    var cents = packed & AmountMask;
    var rel = (packed >> 16) & TimestampMask;

    return (
        Amount: cents / 100.0m,
        Timestamp: _createdAt + rel
    );
  }

  public void Add(long timestampMs, decimal value)
  {
    Add(timestampMs, value, "default");
  }

  public void Add(long timestampMs, decimal value, string processor)
  {
    var packed = Pack(value, timestampMs);

    lock (_lock)
    {
      _items.Add(packed);
      _processors.Add(processor);
    }

    Console.WriteLine($"add {DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}");
  }

  public List<StoredItem> GetAll()
  {
    var result = new List<StoredItem>();

    lock (_lock)
    {
      for (int i = 0; i < _items.Count; i++)
      {
        var unpacked = Unpack(_items[i]);
        var processor = i < _processors.Count ? _processors[i] : "default";

        result.Add(new StoredItem(
            Timestamp: unpacked.Timestamp,
            Value: unpacked.Amount,
            Processor: processor
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
      _processors.Clear();
    }
  }
}
