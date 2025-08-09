// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

public enum DataType
{
    // Any other struct not defined below
    AnyStruct,

    // A BitArray
    Bit,
    SByte,
    Byte,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Single,
    Double,
    DateTime,
    UnixSeconds,
    Int24AsInt64,
    Int40AsInt64,
    Int48AsInt64,
    Int56AsInt64,
    UInt24AsInt64,
    UInt40AsInt64,
    UInt48AsInt64,
    UInt56AsInt64
}