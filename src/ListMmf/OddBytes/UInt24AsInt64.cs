using System;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

public readonly struct UInt24AsInt64
{
    public const long MaxValue = 16777215L; // See OddBytesTests.GetMaxUnsignedValueLong(3)
    public const long MinValue = 0;

    // Must store individual bytes to keep this struct at 3 bytes
    private readonly byte _byte0;
    private readonly byte _byte1;
    private readonly byte _byte2;

    public UInt24AsInt64(long value)
    {
        if (value > MaxValue)
        {
            throw new ArgumentException($"Value must be less than {MaxValue}", nameof(value));
        }
        var bytes = BitConverter.GetBytes(value);
        _byte0 = bytes[0];
        _byte1 = bytes[1];
        _byte2 = bytes[2];
    }

    private long Value
    {
        get
        {
            var bytes = new byte[8];
            bytes[0] = _byte0;
            bytes[1] = _byte1;
            bytes[2] = _byte2;
            return BitConverter.ToInt64(bytes, 0);
        }
    }

    public static implicit operator long(UInt24AsInt64 d)
    {
        return d.Value;
    }
}