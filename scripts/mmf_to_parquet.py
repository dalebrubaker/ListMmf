#!/usr/bin/env python3
"""
Convert ListMmf (.mmf or .bt) files to Parquet format.

Supports reading multiple related columns (e.g., OHLCV data) and combining
them into a single Parquet file with proper schema.

Examples:
    # Convert a single file
    python mmf_to_parquet.py data.mmf output.parquet

    # Convert OHLCV directory structure
    python mmf_to_parquet.py /path/to/ohlcv --output market_data.parquet \\
        --columns Timestamp Open High Low Close Volume

    # Auto-detect all .bt files in directory
    python mmf_to_parquet.py /path/to/data --output combined.parquet
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import List, Optional

import numpy as np
import pandas as pd

try:
    from listmmf_reader import read_mmf, read_header, ListMmfReadError
except ImportError:
    print("ERROR: Cannot import listmmf_reader. Make sure it's in the same directory.", file=sys.stderr)
    sys.exit(1)


def find_mmf_files(directory: Path, column_names: Optional[List[str]] = None) -> dict[str, Path]:
    """
    Find .mmf or .bt files in a directory.

    Args:
        directory: Directory to search
        column_names: If provided, look for files matching these names

    Returns:
        Dictionary mapping column name to file path
    """
    if not directory.is_dir():
        raise ValueError(f"Not a directory: {directory}")

    files = {}

    if column_names:
        # Look for specific column names
        for col in column_names:
            # Try both .mmf and .bt extensions
            for ext in ['.mmf', '.bt']:
                filepath = directory / f"{col}{ext}"
                if filepath.exists():
                    files[col] = filepath
                    break
            if col not in files:
                print(f"WARNING: Column '{col}' not found in {directory}", file=sys.stderr)
    else:
        # Auto-detect all .mmf and .bt files
        for ext in ['*.mmf', '*.bt']:
            for filepath in directory.glob(ext):
                # Use filename (without extension) as column name
                col_name = filepath.stem
                files[col_name] = filepath

    return files


def read_column(filepath: Path, column_name: str) -> pd.Series:
    """
    Read a ListMmf file into a pandas Series.

    Args:
        filepath: Path to the .mmf or .bt file
        column_name: Name for this column

    Returns:
        pandas Series with the data
    """
    print(f"Reading {column_name} from {filepath.name}...", end=' ')

    header = read_header(filepath)
    data = read_mmf(filepath, as_datetime=(column_name.lower() in ['timestamp', 'datetime', 'time']))

    print(f"{len(data):,} rows")

    return pd.Series(data, name=column_name)


def convert_to_parquet(
    files: dict[str, Path],
    output_path: Path,
    compression: str = 'snappy',
    index: bool = False
) -> pd.DataFrame:
    """
    Convert multiple ListMmf files to a single Parquet file.

    Args:
        files: Dictionary mapping column names to file paths
        output_path: Output Parquet file path
        compression: Parquet compression codec (snappy, gzip, brotli, zstd, none)
        index: Whether to write DataFrame index to Parquet

    Returns:
        The combined DataFrame
    """
    if not files:
        raise ValueError("No files to convert")

    # Read all columns
    columns = {}
    max_length = 0

    for col_name, filepath in files.items():
        try:
            series = read_column(filepath, col_name)
            columns[col_name] = series
            max_length = max(max_length, len(series))
        except ListMmfReadError as e:
            print(f"ERROR reading {col_name}: {e}", file=sys.stderr)
            sys.exit(1)

    # Check that all columns have the same length
    lengths = {name: len(series) for name, series in columns.items()}
    if len(set(lengths.values())) > 1:
        print("WARNING: Columns have different lengths:", file=sys.stderr)
        for name, length in lengths.items():
            print(f"  {name}: {length:,}", file=sys.stderr)
        print("Creating DataFrame with mismatched lengths (NaN will be filled).", file=sys.stderr)

    # Create DataFrame
    df = pd.DataFrame(columns)

    # Ensure output directory exists
    output_path.parent.mkdir(parents=True, exist_ok=True)

    # Write to Parquet
    print(f"\nWriting to {output_path} with {compression} compression...")
    df.to_parquet(output_path, compression=compression, index=index)

    # Report file sizes
    input_size = sum(f.stat().st_size for f in files.values())
    output_size = output_path.stat().st_size
    ratio = (1 - output_size / input_size) * 100 if input_size > 0 else 0

    print(f"\nConversion complete!")
    print(f"  Input:  {input_size:,} bytes ({input_size / (1024**2):.2f} MB)")
    print(f"  Output: {output_size:,} bytes ({output_size / (1024**2):.2f} MB)")
    print(f"  Compression: {ratio:.1f}% size reduction")
    print(f"  Rows: {len(df):,}")
    print(f"  Columns: {len(df.columns)}")

    return df


def convert_single_file(input_path: Path, output_path: Path, compression: str = 'snappy') -> pd.DataFrame:
    """
    Convert a single ListMmf file to Parquet.

    Args:
        input_path: Input .mmf or .bt file
        output_path: Output Parquet file
        compression: Parquet compression codec

    Returns:
        The DataFrame
    """
    col_name = input_path.stem
    series = read_column(input_path, col_name)
    df = pd.DataFrame({col_name: series})

    output_path.parent.mkdir(parents=True, exist_ok=True)

    print(f"\nWriting to {output_path} with {compression} compression...")
    df.to_parquet(output_path, compression=compression, index=False)

    input_size = input_path.stat().st_size
    output_size = output_path.stat().st_size
    ratio = (1 - output_size / input_size) * 100 if input_size > 0 else 0

    print(f"\nConversion complete!")
    print(f"  Input:  {input_size:,} bytes ({input_size / (1024**2):.2f} MB)")
    print(f"  Output: {output_size:,} bytes ({output_size / (1024**2):.2f} MB)")
    print(f"  Compression: {ratio:.1f}% size reduction")
    print(f"  Rows: {len(df):,}")

    return df


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Convert ListMmf files to Parquet format",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Single file
  %(prog)s data.mmf output.parquet

  # OHLCV directory with specific columns
  %(prog)s /data/ohlcv --output market.parquet --columns Timestamp Open High Low Close Volume

  # Auto-detect all columns in directory
  %(prog)s /data/columns --output combined.parquet

  # Use different compression
  %(prog)s data.mmf output.parquet --compression gzip
        """
    )

    parser.add_argument('input', type=str, help='Input .mmf/.bt file or directory')
    parser.add_argument('output', type=str, nargs='?', help='Output .parquet file (required for single file)')
    parser.add_argument('--output', '-o', dest='output_arg', type=str, help='Output .parquet file')
    parser.add_argument('--columns', '-c', nargs='+', help='Column names to read from directory')
    parser.add_argument(
        '--compression',
        choices=['snappy', 'gzip', 'brotli', 'zstd', 'none'],
        default='snappy',
        help='Parquet compression codec (default: snappy)'
    )
    parser.add_argument('--index', action='store_true', help='Write DataFrame index to Parquet')

    args = parser.parse_args()

    # Determine output path
    output_path = args.output_arg or args.output
    if not output_path:
        print("ERROR: Output file path is required", file=sys.stderr)
        parser.print_help()
        return 1

    input_path = Path(args.input)
    output_path = Path(output_path)

    try:
        if input_path.is_file():
            # Single file conversion
            if args.columns:
                print("WARNING: --columns ignored for single file input", file=sys.stderr)
            convert_single_file(input_path, output_path, args.compression)

        elif input_path.is_dir():
            # Directory conversion
            files = find_mmf_files(input_path, args.columns)

            if not files:
                print(f"ERROR: No .mmf or .bt files found in {input_path}", file=sys.stderr)
                if args.columns:
                    print(f"Searched for: {', '.join(args.columns)}", file=sys.stderr)
                return 1

            print(f"Found {len(files)} column(s): {', '.join(files.keys())}")
            convert_to_parquet(files, output_path, args.compression, args.index)

        else:
            print(f"ERROR: Input path does not exist: {input_path}", file=sys.stderr)
            return 1

        return 0

    except Exception as e:
        print(f"ERROR: {e}", file=sys.stderr)
        import traceback
        traceback.print_exc()
        return 1


if __name__ == "__main__":
    sys.exit(main())
