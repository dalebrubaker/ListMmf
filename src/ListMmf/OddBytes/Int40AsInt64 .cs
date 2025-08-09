using System;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

public readonly struct Int40AsInt64
{
    public const long MaxValue = 1099511627775L / 2 - 1; // See OddBytesTests.GetMaxUnsignedValueLong(3)
    public const long MinValue = -1099511627775L / 2;

    // Must store individual bytes to keep this struct at 5 bytes
    private readonly byte _byte0;
    private readonly byte _byte1;
    private readonly byte _byte2;
    private readonly byte _byte3;
    private readonly byte _byte4;

    public Int40AsInt64(long value)
    {
        if (value > MaxValue)
        {
            throw new ArgumentException($"Value must be less than {MaxValue}", nameof(value));
        }
        if (value < MinValue)
        {
            throw new ArgumentException($"Value must be greater than {MinValue}", nameof(value));
        }
        var bytes = BitConverter.GetBytes(value);
        _byte0 = bytes[0];
        _byte1 = bytes[1];
        _byte2 = bytes[2];
        _byte3 = bytes[3];
        _byte4 = bytes[4];
    }

    private long Value
    {
        get
        {
            var bytes = new byte[8];
            bytes[0] = _byte0;
            bytes[1] = _byte1;
            bytes[2] = _byte2;
            bytes[3] = _byte3;
            bytes[4] = _byte4;
            if (_byte4 > 127)
            {
                // This is a negative number
                bytes[5] = 255;
                bytes[6] = 255;
                bytes[7] = 255;
            }
            return BitConverter.ToInt64(bytes, 0);
        }
    }

    public static implicit operator long(Int40AsInt64 d)
    {
        return d.Value;
    }
}