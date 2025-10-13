using System;

namespace BruSoftware.ListMmf;

/// <summary>
/// Utility methods for working with DataType min/max values and type selection.
/// </summary>
public static class DataTypeUtils
{
    /// <summary>
    /// Gets the minimum and maximum values that can be stored in the specified DataType.
    /// </summary>
    /// <param name="dataType">The data type to query.</param>
    /// <returns>A tuple containing (minValue, maxValue) for the specified type.</returns>
    /// <exception cref="NotSupportedException">If the DataType is not supported or cannot be represented as long values.</exception>
    public static (long minValue, long maxValue) GetMinMaxValues(DataType dataType)
    {
        switch (dataType)
        {
            case DataType.AnyStruct:
                throw new NotSupportedException($"Unable to determine min/max values for {nameof(DataType)}.{dataType}");
            case DataType.Bit:
                return (0, 1);
            case DataType.SByte:
                return (SByte.MinValue, SByte.MaxValue);
            case DataType.Byte:
                return (Byte.MinValue, Byte.MaxValue);
            case DataType.Int16:
                return (Int16.MinValue, Int16.MaxValue);
            case DataType.UInt16:
                return (UInt16.MinValue, UInt16.MaxValue);
            case DataType.Int32:
                return (Int32.MinValue, Int32.MaxValue);
            case DataType.UInt32:
                return (UInt32.MinValue, UInt32.MaxValue);
            case DataType.Int64:
                return (Int64.MinValue, Int64.MaxValue);
            case DataType.UInt64:
                return (0, long.MaxValue);
            case DataType.UnixSeconds:
                return (int.MinValue, int.MaxValue);
            case DataType.Single:
            case DataType.Double:
            case DataType.DateTime:
                throw new NotSupportedException($"DataTypeUtils does not support {nameof(DataType)}.{dataType}");
            case DataType.Int24AsInt64:
                return (Int24AsInt64.MinValue, Int24AsInt64.MaxValue);
            case DataType.Int40AsInt64:
                return (Int40AsInt64.MinValue, Int40AsInt64.MaxValue);
            case DataType.Int48AsInt64:
                return (Int48AsInt64.MinValue, Int48AsInt64.MaxValue);
            case DataType.Int56AsInt64:
                return (Int56AsInt64.MinValue, Int56AsInt64.MaxValue);
            case DataType.UInt24AsInt64:
                return (UInt24AsInt64.MinValue, UInt24AsInt64.MaxValue);
            case DataType.UInt40AsInt64:
                return (UInt40AsInt64.MinValue, UInt40AsInt64.MaxValue);
            case DataType.UInt48AsInt64:
                return (UInt48AsInt64.MinValue, UInt48AsInt64.MaxValue);
            case DataType.UInt56AsInt64:
                return (UInt56AsInt64.MinValue, UInt56AsInt64.MaxValue);
            default:
                throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
        }
    }

    /// <summary>
    /// Determines the smallest integer DataType that can hold the specified range of values.
    /// Automatically selects between signed and unsigned types based on whether minValue is negative.
    /// </summary>
    /// <param name="minValue">The minimum value that needs to be stored.</param>
    /// <param name="maxValue">The maximum value that needs to be stored.</param>
    /// <returns>The smallest DataType that can accommodate the range.</returns>
    /// <exception cref="NotSupportedException">If no suitable DataType can accommodate the range.</exception>
    public static DataType GetSmallestInt64DataType(long minValue, long maxValue)
    {
        if (minValue < 0)
        {
            // return signed type
            if (maxValue <= SByte.MaxValue && minValue >= SByte.MinValue)
            {
                return DataType.SByte;
            }
            if (maxValue <= Int16.MaxValue && minValue >= Int16.MinValue)
            {
                return DataType.Int16;
            }
            if (maxValue <= Int24AsInt64.MaxValue && minValue >= Int24AsInt64.MinValue)
            {
                return DataType.Int24AsInt64;
            }
            if (maxValue <= Int32.MaxValue && minValue >= Int32.MinValue)
            {
                return DataType.Int32;
            }
            if (maxValue <= Int40AsInt64.MaxValue && minValue >= Int40AsInt64.MinValue)
            {
                return DataType.Int40AsInt64;
            }
            if (maxValue <= Int48AsInt64.MaxValue && minValue >= Int48AsInt64.MinValue)
            {
                return DataType.Int48AsInt64;
            }
            if (maxValue <= Int56AsInt64.MaxValue && minValue >= Int56AsInt64.MinValue)
            {
                return DataType.Int56AsInt64;
            }
            if (maxValue <= Int64.MaxValue && minValue >= Int64.MinValue)
            {
                return DataType.Int64;
            }
            throw new NotSupportedException($"Unexpected. minValue={minValue} maxValue={maxValue}");
        }
        // return unsigned type
        if (maxValue == 0)
        {
            // minValue and maxValue are both 0. Don't even create a file.
            return DataType.Bit;
        }
        if (maxValue <= 1)
        {
            // Can fit in BitArray
            return DataType.Bit;
        }
        if (maxValue <= Byte.MaxValue)
        {
            return DataType.Byte;
        }
        if (maxValue <= UInt16.MaxValue)
        {
            return DataType.UInt16;
        }
        if (maxValue <= UInt24AsInt64.MaxValue)
        {
            return DataType.UInt24AsInt64;
        }
        if (maxValue <= UInt32.MaxValue)
        {
            return DataType.UInt32;
        }
        if (maxValue <= UInt40AsInt64.MaxValue)
        {
            return DataType.UInt40AsInt64;
        }
        if (maxValue <= UInt48AsInt64.MaxValue)
        {
            return DataType.UInt48AsInt64;
        }
        if (maxValue <= UInt56AsInt64.MaxValue)
        {
            return DataType.UInt56AsInt64;
        }
        if (maxValue <= Int64.MaxValue)
        {
            // Can fit in UInt64, but we can't go bigger than long because we're returning long, so just use long
            return DataType.Int64;
        }
        throw new NotSupportedException($"Unexpected. minValue={minValue} maxValue={maxValue}");
    }
}
