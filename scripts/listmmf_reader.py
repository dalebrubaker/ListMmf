#!/usr/bin/env python3
"""
Read-only snapshot reader for ListMmf (.mmf or .bt) files.

Supports zero-copy memory-mapped reading of standard data types.
Does NOT support odd-byte types (Int24/40/48/56, UInt24/40/48/56).

File format (little-endian):
  Bytes 0-3:   Version (int32)
  Bytes 4-7:   DataType enum (int32)
  Bytes 8-15:  Count (int64)
  Bytes 16+:   Array data (Count × element_width)
"""

from __future__ import annotations

import struct
import numpy as np
from pathlib import Path
from typing import Tuple
from dataclasses import dataclass


# DataType enum mapping (from src/ListMmf/DataType.cs)
DATA_TYPE_NAMES = {
    0: "AnyStruct",
    1: "Bit",
    2: "SByte",
    3: "Byte",
    4: "Int16",
    5: "UInt16",
    6: "Int32",
    7: "UInt32",
    8: "Int64",
    9: "UInt64",
    10: "Single",
    11: "Double",
    12: "DateTime",
    13: "UnixSeconds",
    14: "Int24AsInt64",
    15: "Int40AsInt64",
    16: "Int48AsInt64",
    17: "Int56AsInt64",
    18: "UInt24AsInt64",
    19: "UInt40AsInt64",
    20: "UInt48AsInt64",
    21: "UInt56AsInt64",
}

# Map DataType enum to numpy dtype (only standard types)
DTYPE_MAP = {
    2: np.int8,      # SByte
    3: np.uint8,     # Byte
    4: np.int16,     # Int16
    5: np.uint16,    # UInt16
    6: np.int32,     # Int32
    7: np.uint32,    # UInt32
    8: np.int64,     # Int64
    9: np.uint64,    # UInt64
    10: np.float32,  # Single
    11: np.float64,  # Double
    12: np.int64,    # DateTime (stored as .NET ticks)
    13: np.int32,    # UnixSeconds
}

# Odd-byte types (not supported for direct numpy reading)
ODD_BYTE_TYPES = {14, 15, 16, 17, 18, 19, 20, 21}

HEADER_SIZE = 16


@dataclass
class ListMmfHeader:
    """Header information from a ListMmf file."""
    version: int
    datatype: int
    datatype_name: str
    count: int


class ListMmfReadError(Exception):
    """Exception raised when reading ListMmf files."""
    pass


def read_header(filepath: str | Path) -> ListMmfHeader:
    """
    Read the 16-byte header from a ListMmf file.

    Args:
        filepath: Path to the .mmf or .bt file

    Returns:
        ListMmfHeader with version, datatype, and count

    Raises:
        ListMmfReadError: If file is invalid or too small
    """
    filepath = Path(filepath)

    if not filepath.exists():
        raise ListMmfReadError(f"File not found: {filepath}")

    if filepath.stat().st_size < HEADER_SIZE:
        raise ListMmfReadError(f"File too small (< {HEADER_SIZE} bytes): {filepath}")

    try:
        with open(filepath, 'rb') as f:
            header_bytes = f.read(HEADER_SIZE)

        version, datatype, count = struct.unpack('<iiq', header_bytes)
        datatype_name = DATA_TYPE_NAMES.get(datatype, f"Unknown({datatype})")

        return ListMmfHeader(
            version=version,
            datatype=datatype,
            datatype_name=datatype_name,
            count=count
        )
    except Exception as e:
        raise ListMmfReadError(f"Failed to read header from {filepath}: {e}")


def read_listmmf(filepath: str | Path, copy: bool = False) -> np.ndarray:
    """
    Read a ListMmf file into a numpy array.

    By default, uses memory mapping (zero-copy). Set copy=True to load into RAM.

    Args:
        filepath: Path to the .mmf or .bt file
        copy: If True, load data into memory; if False (default), use memory map

    Returns:
        numpy array with the data

    Raises:
        ListMmfReadError: If file format is invalid or unsupported

    Note:
        - DateTime values are returned as int64 .NET ticks (100ns since 0001-01-01)
        - UnixSeconds values are returned as int32 seconds since 1970-01-01
        - Use convert_datetime() to convert DateTime to pandas datetime64
        - Odd-byte types (Int24/40/48/56) are not supported
    """
    filepath = Path(filepath)
    header = read_header(filepath)

    # Check for unsupported odd-byte types
    if header.datatype in ODD_BYTE_TYPES:
        raise ListMmfReadError(
            f"Odd-byte type {header.datatype_name} is not supported. "
            f"Consider exporting from C# to a standard format first."
        )

    # Check for special types
    if header.datatype == 0:  # AnyStruct
        raise ListMmfReadError(f"Cannot read AnyStruct files (DataType=0)")

    if header.datatype == 1:  # Bit (BitArray)
        raise ListMmfReadError(
            f"BitArray files require special handling (not yet implemented). "
            f"Use the C# library or implement bit unpacking."
        )

    # Get numpy dtype
    dtype = DTYPE_MAP.get(header.datatype)
    if dtype is None:
        raise ListMmfReadError(f"Unsupported DataType: {header.datatype_name}")

    # Read data via memory map or copy
    try:
        if copy:
            # Load into RAM
            data = np.memmap(filepath, dtype=dtype, mode='r', offset=HEADER_SIZE, shape=(header.count,))
            return np.array(data)  # Copy to RAM
        else:
            # Zero-copy memory map
            return np.memmap(filepath, dtype=dtype, mode='r', offset=HEADER_SIZE, shape=(header.count,))
    except Exception as e:
        raise ListMmfReadError(f"Failed to read data from {filepath}: {e}")


def convert_datetime(ticks: np.ndarray) -> np.ndarray:
    """
    Convert .NET DateTime ticks to pandas datetime64[ns].

    .NET ticks are 100-nanosecond intervals since 0001-01-01 00:00:00.
    Pandas datetime64[ns] uses nanoseconds since 1970-01-01 00:00:00 (Unix epoch).

    Args:
        ticks: Array of int64 .NET ticks

    Returns:
        Array of datetime64[ns] values
    """
    # .NET epoch (0001-01-01) to Unix epoch (1970-01-01) offset in ticks
    # 621355968000000000 ticks = days from 0001-01-01 to 1970-01-01
    DOTNET_TO_UNIX_TICKS = 621355968000000000

    # Convert: .NET ticks → Unix nanoseconds
    unix_ns = (ticks - DOTNET_TO_UNIX_TICKS) * 100

    return unix_ns.astype('datetime64[ns]')


def convert_unixseconds(seconds: np.ndarray) -> np.ndarray:
    """
    Convert Unix seconds (int32) to pandas datetime64[ns].

    Args:
        seconds: Array of int32 Unix seconds

    Returns:
        Array of datetime64[ns] values
    """
    return seconds.astype('datetime64[s]').astype('datetime64[ns]')


# Convenience function for common usage
def read_mmf(filepath: str | Path, as_datetime: bool = True) -> np.ndarray:
    """
    Read a ListMmf file with automatic datetime conversion.

    Args:
        filepath: Path to the .mmf or .bt file
        as_datetime: If True, convert DateTime/UnixSeconds to datetime64[ns]

    Returns:
        numpy array (with datetime conversion if applicable)
    """
    data = read_listmmf(filepath, copy=False)

    if as_datetime:
        header = read_header(filepath)
        if header.datatype == 12:  # DateTime
            return convert_datetime(data)
        elif header.datatype == 13:  # UnixSeconds
            return convert_unixseconds(data)

    return data


if __name__ == "__main__":
    import sys

    if len(sys.argv) < 2:
        print("Usage: listmmf_reader.py <file.mmf>")
        print("\nReads and displays info about a ListMmf file.")
        sys.exit(1)

    filepath = sys.argv[1]

    try:
        # Read header
        header = read_header(filepath)
        print(f"File: {filepath}")
        print(f"Version: {header.version}")
        print(f"DataType: {header.datatype_name} ({header.datatype})")
        print(f"Count: {header.count:,}")

        # Try to read data
        data = read_mmf(filepath, as_datetime=True)
        print(f"\nData shape: {data.shape}")
        print(f"Data dtype: {data.dtype}")
        print(f"\nFirst 10 values:")
        print(data[:10])

        if len(data) > 10:
            print(f"\nLast 10 values:")
            print(data[-10:])

    except ListMmfReadError as e:
        print(f"ERROR: {e}", file=sys.stderr)
        sys.exit(1)
