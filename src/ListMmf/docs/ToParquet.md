Yes, it seems quite feasible to read SmallestInt64 files and convert them to Parquet, though there are some considerations for each approach:

## Python Approach

Python would be **more challenging** because:

1. **Header Parsing**: You'd need to manually parse the ListMmf header structure:
   - First 4 bytes: Version (int32)
   - Next 4 bytes: DataType (int32) 
   - Next 8 bytes: Count (int64)
   - Additional headers for specific types (e.g., BitArray has an extra 8 bytes for Length)

2. **Odd-byte Type Handling**: The odd-byte types (Int24, Int40, etc.) would require custom unpacking:
```python
import struct
import numpy as np

def read_int24(bytes_data):
    # Read 3 bytes and sign-extend to 8 bytes
    value = int.from_bytes(bytes_data[:3], 'little', signed=False)
    if bytes_data[2] & 0x80:  # Check sign bit
        value |= 0xFFFFFFFFFF000000  # Sign extend
    return np.int64(value).astype(np.int64)
```

3. **Memory Mapping**: You could use Python's `mmap` for efficient reading:
```python
import mmap
import pandas as pd
import pyarrow.parquet as pq

def read_listmmf_to_parquet(mmf_path, parquet_path):
    with open(mmf_path, 'rb') as f:
        with mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ) as mmf:
            # Read header
            version = struct.unpack('<i', mmf[0:4])[0]
            data_type = struct.unpack('<i', mmf[4:8])[0]
            count = struct.unpack('<q', mmf[8:16])[0]
            
            # Map DataType enum values
            data_type_map = {
                0: 'Empty', 1: 'Bit', 2: 'SByte', 3: 'Byte',
                4: 'Int16', 5: 'UInt16', 6: 'Int32', 7: 'UInt32',
                8: 'Int64', 9: 'UInt64', 10: 'Single', 11: 'Double',
                12: 'DateTime', 13: 'UnixSeconds',
                14: 'Int24AsInt64', 15: 'Int40AsInt64', # etc...
            }
            
            # Read data based on type
            data = read_data_by_type(mmf, data_type, count)
            
            # Convert to DataFrame and save as Parquet
            df = pd.DataFrame({'value': data})
            df.to_parquet(parquet_path)
```

## C# Approach

C# would be **much more straightforward** because:

1. **Direct Library Usage**: You can reference the ListMmf library directly:
```csharp
using BruSoftware.ListMmf;
using Parquet;
using Parquet.Data;

public async Task ConvertToParquet(string mmfPath, string parquetPath)
{
    // Automatically handles all types including odd-byte ones
    using var list = new SmallestInt64ListMmf(DataType.Empty, mmfPath, 
                                              access: MemoryMappedFileAccess.Read);
    
    // Read all values
    var values = new long[list.Count];
    for (long i = 0; i < list.Count; i++)
    {
        values[i] = list[i];
    }
    
    // Create Parquet file
    var schema = new Schema(new DataField<long>("value"));
    using var file = File.OpenWrite(parquetPath);
    using var writer = await ParquetWriter.CreateAsync(schema, file);
    
    var dataFields = new[] { new DataColumn(schema.Fields[0], values) };
    using var groupWriter = writer.CreateRowGroup();
    await groupWriter.WriteAsync(dataFields);
}
```

2. **Automatic Type Handling**: The library handles all the complexity of odd-byte types internally.

3. **Bulk Export Utility**: You could create a generic utility:
```csharp
public static class ListMmfToParquet
{
    public static async Task ExportFile(string mmfPath, string parquetPath)
    {
        var (version, dataType, count) = UtilsListMmf.GetHeaderInfo(mmfPath);
        
        switch (dataType)
        {
            case DataType.Bit:
                await ExportBitArray(mmfPath, parquetPath);
                break;
            case DataType.DateTime:
            case DataType.UnixSeconds:
                await ExportDateTime(mmfPath, parquetPath);
                break;
            default:
                // All integer types can use SmallestInt64ListMmf
                await ExportInt64(mmfPath, parquetPath);
                break;
        }
    }
}
```

## Recommendation

I'd recommend **C#** for this task because:
1. No need to reverse-engineer the binary format
2. Automatic handling of all odd-byte types
3. Type safety and compile-time checking
4. Can use the same exact library that created the files
5. Better performance for large files

If you must use Python, you'd want to:
1. Create a detailed specification of the binary format
2. Build a Python package that mirrors the C# type handling
3. Extensively test against files created by the C# library
4. Consider using Cython or similar for performance-critical odd-byte unpacking

Alternatively, you could create a C# command-line tool that does the conversion and call it from Python if you need Python integration.