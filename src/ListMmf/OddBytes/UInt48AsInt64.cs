using System;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

public readonly struct UInt48AsInt64
{
    public const long MaxValue = 281474976710655L; // See OddBytesTests.GetMaxUnsignedValueLong(3)
    public const long MinValue = 0;

    // Must store individual bytes to keep this struct at 6 bytes
    private readonly byte _byte0;
    private readonly byte _byte1;
    private readonly byte _byte2;
    private readonly byte _byte3;
    private readonly byte _byte4;
    private readonly byte _byte5;

    public UInt48AsInt64(long value)
    {
        if (value > MaxValue)
        {
            throw new ArgumentException($"Value must be less than {MaxValue}", nameof(value));
        }
        _byte0 = (byte)(value & 0xFF);
        _byte1 = (byte)((value >> 8) & 0xFF);
        _byte2 = (byte)((value >> 16) & 0xFF);
        _byte3 = (byte)((value >> 24) & 0xFF);
        _byte4 = (byte)((value >> 32) & 0xFF);
        _byte5 = (byte)((value >> 40) & 0xFF);
    }

    private long Value
    {
        get
        {
            long v = (long)_byte0
                     | ((long)_byte1 << 8)
                     | ((long)_byte2 << 16)
                     | ((long)_byte3 << 24)
                     | ((long)_byte4 << 32)
                     | ((long)_byte5 << 40);
            return v;
        }
    }

    public static implicit operator long(UInt48AsInt64 d)
    {
        return d.Value;
    }
}
