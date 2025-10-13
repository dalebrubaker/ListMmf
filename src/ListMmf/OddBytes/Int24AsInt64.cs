using System;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

public readonly struct Int24AsInt64
{
    public const long MaxValue = 16777215L / 2 - 1; // See OddBytesTests.GetMaxUnsignedValueLong(3)
    public const long MinValue = -16777215L / 2;

    // Must store individual bytes to keep this struct at 3 bytes
    private readonly byte _byte0;
    private readonly byte _byte1;
    private readonly byte _byte2;

    public Int24AsInt64(long value)
    {
        if (value > MaxValue)
        {
            throw new ArgumentException($"Value must be less than {MaxValue}", nameof(value));
        }
        if (value < MinValue)
        {
            throw new ArgumentException($"Value must be greater than {MinValue}", nameof(value));
        }
        // Store least-significant 3 bytes (little-endian) without allocations
        _byte0 = (byte)(value & 0xFF);
        _byte1 = (byte)((value >> 8) & 0xFF);
        _byte2 = (byte)((value >> 16) & 0xFF);
    }

    private long Value
    {
        get
        {
            // Reconstruct signed 24-bit value with sign extension
            long v = (long)_byte0
                     | ((long)_byte1 << 8)
                     | ((long)_byte2 << 16);
            if ((_byte2 & 0x80) != 0)
            {
                // Negative number: sign-extend to 64 bits
                v |= (-1L) << 24;
            }
            return v;
        }
    }

    public static implicit operator long(Int24AsInt64 d)
    {
        return d.Value;
    }
}
