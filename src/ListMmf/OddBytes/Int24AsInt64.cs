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
            if (_byte2 > 127)
            {
                // This is a negative number
                bytes[3] = 255;
                bytes[4] = 255;
                bytes[5] = 255;
                bytes[6] = 255;
                bytes[7] = 255;
            }
            var result = BitConverter.ToInt64(bytes, 0);
            return result;
        }
    }

    public static implicit operator long(Int24AsInt64 d)
    {
        return d.Value;
    }
}