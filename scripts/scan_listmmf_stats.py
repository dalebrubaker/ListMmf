#!/usr/bin/env python3
"""
Scan a directory tree for ListMmf/Smallest*.bt files and report:

- Total .bt file count and total size (bytes, GB, GiB)
- DataType distribution (by count of files and items)
- Count of files using odd-byte integer types (Int24/40/48/56 and UInt variants)
- Bytes saved by using odd-byte types vs nearest standard dtype (Int24→Int32, others→Int64)
- Savings grouped by DataType

Header layout (little-endian):
  int32 Version, int32 DataType, int64 Count   => 16 bytes total

DataType enum (from src/ListMmf/DataType.cs):
  0 AnyStruct, 1 Bit, 2 SByte, 3 Byte, 4 Int16, 5 UInt16,
  6 Int32, 7 UInt32, 8 Int64, 9 UInt64, 10 Single, 11 Double,
  12 DateTime, 13 UnixSeconds,
  14 Int24AsInt64, 15 Int40AsInt64, 16 Int48AsInt64, 17 Int56AsInt64,
  18 UInt24AsInt64, 19 UInt40AsInt64, 20 UInt48AsInt64, 21 UInt56AsInt64

Note: For BitArray, the underlying array starts after an extra 8-byte length field,
but we only need the main 16-byte header for this report.
"""

from __future__ import annotations

import argparse
import os
import sys
import struct
from dataclasses import dataclass
from collections import defaultdict, Counter
from typing import Dict, Tuple


# DataType mapping from src/ListMmf/DataType.cs
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
    12: "DateTime",      # .NET ticks in Int64
    13: "UnixSeconds",   # Int32 seconds since 1970-01-01
    14: "Int24AsInt64",
    15: "Int40AsInt64",
    16: "Int48AsInt64",
    17: "Int56AsInt64",
    18: "UInt24AsInt64",
    19: "UInt40AsInt64",
    20: "UInt48AsInt64",
    21: "UInt56AsInt64",
}

# Element width (in bytes) for each DataType when stored on disk as ListMmf<T>
# Odd-byte types use the exact byte count shown in the type name
ELEM_WIDTH_BYTES = {
    1: 1,   # BitArray is bit-packed, but Count in header refers to int32 words; we don't compute savings for Bit here
    2: 1,
    3: 1,
    4: 2,
    5: 2,
    6: 4,
    7: 4,
    8: 8,
    9: 8,
    10: 4,
    11: 8,
    12: 8,  # ticks
    13: 4,  # seconds
    14: 3,
    15: 5,
    16: 6,
    17: 7,
    18: 3,
    19: 5,
    20: 6,
    21: 7,
}

# Define which DataTypes are considered "odd-byte" (not natively supported dtype widths in NumPy)
ODD_BYTE_DT = {14, 15, 16, 17, 18, 19, 20, 21}

# Define the "fallback" standard byte width per odd-byte DataType for comparison:
#  - Int24/UInt24 → 4 bytes (Int32/UInt32)
#  - Int40/48/56, UInt40/48/56 → 8 bytes (Int64/UInt64)
ODD_FALLBACK_WIDTH = {
    14: 4,  # Int24AsInt64 → Int32
    18: 4,  # UInt24AsInt64 → UInt32
    15: 8,  # Int40AsInt64 → Int64
    16: 8,  # Int48AsInt64 → Int64
    17: 8,  # Int56AsInt64 → Int64
    19: 8,  # UInt40AsInt64 → UInt64
    20: 8,  # UInt48AsInt64 → UInt64
    21: 8,  # UInt56AsInt64 → UInt64
}


LISTMMF_HEADER_SIZE = 16  # version:int32, dataType:int32, count:int64


@dataclass
class FileInfo:
    path: str
    size_bytes: int
    version: int
    data_type: int
    count: int


def read_header(path: str) -> FileInfo | None:
    try:
        size = os.path.getsize(path)
        if size < LISTMMF_HEADER_SIZE:
            print(f"WARN: File too small for header: {path}", file=sys.stderr)
            return None
        with open(path, "rb") as f:
            b = f.read(LISTMMF_HEADER_SIZE)
        version, data_type, count = struct.unpack("<iiq", b)
        return FileInfo(path=path, size_bytes=size, version=version, data_type=data_type, count=count)
    except Exception as e:
        print(f"ERROR: Failed reading header for {path}: {e}", file=sys.stderr)
        return None


def scan_dir(base_dir: str) -> Dict[str, object]:
    total_bytes = 0
    total_files = 0

    by_dtype_files = Counter()       # number of files per dtype
    by_dtype_items = Counter()       # number of items across files per dtype
    by_dtype_bytes = Counter()       # total file bytes per dtype

    odd_files = 0
    odd_saved_bytes_total = 0
    odd_saved_bytes_by_dtype = Counter()

    results: list[FileInfo] = []

    for root, _, files in os.walk(base_dir):
        for fn in files:
            if not fn.lower().endswith(".bt"):
                continue
            path = os.path.join(root, fn)
            info = read_header(path)
            if info is None:
                continue

            total_files += 1
            total_bytes += info.size_bytes

            dt = info.data_type
            by_dtype_files[dt] += 1
            by_dtype_items[dt] += max(0, info.count)
            by_dtype_bytes[dt] += info.size_bytes

            if dt in ODD_BYTE_DT:
                odd_files += 1
                odd = ELEM_WIDTH_BYTES.get(dt)
                fb = ODD_FALLBACK_WIDTH[dt]
                if odd is not None and info.count >= 0:
                    saved = (fb - odd) * info.count
                    # Guard against negative or pathological counts
                    if saved > 0:
                        odd_saved_bytes_total += saved
                        odd_saved_bytes_by_dtype[dt] += saved

            results.append(info)

    return {
        "total_files": total_files,
        "total_bytes": total_bytes,
        "by_dtype_files": by_dtype_files,
        "by_dtype_items": by_dtype_items,
        "by_dtype_bytes": by_dtype_bytes,
        "odd_files": odd_files,
        "odd_saved_bytes_total": odd_saved_bytes_total,
        "odd_saved_bytes_by_dtype": odd_saved_bytes_by_dtype,
    }


def format_bytes(n: int) -> str:
    return f"{n:,} B"


def to_gb(n: int) -> float:
    return n / 1_000_000_000.0  # decimal GB


def to_gib(n: int) -> float:
    return n / (1024.0 ** 3)    # GiB


def main() -> int:
    parser = argparse.ArgumentParser(description="Scan .bt files and report DataType distribution and odd-byte savings")
    parser.add_argument("base", nargs="?", default="/Volumes/T500Pro/BruTrader21Data/Data",
                        help="Base directory to scan (default: %(default)s)")
    parser.add_argument("--gb", dest="use_gib", action="store_false",
                        help="Report decimal GB instead of GiB (default is GiB)")
    parser.add_argument("--gib", dest="use_gib", action="store_true",
                        help="Report GiB (binary) (default)")
    parser.set_defaults(use_gib=True)
    args = parser.parse_args()

    base_dir = args.base
    stats = scan_dir(base_dir)

    total_files = stats["total_files"]
    total_bytes = stats["total_bytes"]
    odd_files = stats["odd_files"]
    odd_saved_total = stats["odd_saved_bytes_total"]

    by_dtype_files = stats["by_dtype_files"]
    by_dtype_items = stats["by_dtype_items"]
    by_dtype_bytes = stats["by_dtype_bytes"]
    odd_saved_by_dtype = stats["odd_saved_bytes_by_dtype"]

    conv = to_gib if args.use_gib else to_gb
    unit = "GiB" if args.use_gib else "GB"

    print(f"Base directory: {base_dir}")
    print(f".bt files: {total_files:,}")
    print(f"Total size: {format_bytes(total_bytes)}  ({conv(total_bytes):,.2f} {unit})")
    print()
    print("By DataType:")
    # Sort by dtype code for stable order
    for dt in sorted(by_dtype_files.keys()):
        name = DATA_TYPE_NAMES.get(dt, f"Unknown({dt})")
        files = by_dtype_files[dt]
        items = by_dtype_items[dt]
        bytes_total = by_dtype_bytes[dt]
        print(f"  {dt:2d} {name:16s}  files={files:8,d}  items={items:14,d}  size={conv(bytes_total):10.2f} {unit}")

    print()
    print(f"Odd-byte dtypes (not native NumPy widths): files={odd_files:,}")
    print(f"Saved bytes by using odd-byte types vs standard widths: {format_bytes(odd_saved_total)}  ({conv(odd_saved_total):,.2f} {unit})")
    print("Savings by DataType:")
    # Show all odd-byte types, even those with zero entries
    for dt in sorted(ODD_BYTE_DT):
        name = DATA_TYPE_NAMES.get(dt, str(dt))
        saved = odd_saved_by_dtype.get(dt, 0)
        elem_w = ELEM_WIDTH_BYTES.get(dt, 0)
        fb_w = ODD_FALLBACK_WIDTH.get(dt, 0)
        print(f"  {dt:2d} {name:16s}  elem={elem_w}B  fallback={fb_w}B  saved={format_bytes(saved)}  ({conv(saved):,.4f} {unit})")

    print()
    print("Notes:")
    print("- Savings assume Int24→Int32 and 40/48/56-bit→Int64 equivalence.")
    print("- 'items' is Count from header; for BitArray it reflects underlying int32 words, not logical bits.")
    print("- Sizes include file header and OS page rounding; size totals are actual on-disk sizes.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())

