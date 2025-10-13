using System;

namespace BruSoftware.ListMmf;

public sealed class DataTypeOverflowException : ListMmfException
{
    public DataTypeOverflowException(
        string path,
        DataType currentDataType,
        long attemptedValue,
        DataType suggestedDataType,
        long allowedMin,
        long allowedMax,
        string? seriesName = null)
        : base(CreateMessage(path, currentDataType, attemptedValue, suggestedDataType, allowedMin, allowedMax, seriesName))
    {
        Path = path;
        CurrentDataType = currentDataType;
        AttemptedValue = attemptedValue;
        SuggestedDataType = suggestedDataType;
        AllowedMin = allowedMin;
        AllowedMax = allowedMax;
        SeriesName = seriesName;
    }

    public string Path { get; }

    public string? SeriesName { get; }

    public DataType CurrentDataType { get; }

    public long AttemptedValue { get; }

    public DataType SuggestedDataType { get; }

    public long AllowedMin { get; }

    public long AllowedMax { get; }

    private static string CreateMessage(
        string path,
        DataType current,
        long value,
        DataType suggested,
        long allowedMin,
        long allowedMax,
        string? seriesName)
    {
        var scope = string.IsNullOrEmpty(seriesName) ? path : $"{seriesName} ({path})";
        return $"Value {value} exceeds range [{allowedMin}, {allowedMax}] for {current} in {scope}. Suggested upgrade: {suggested}.";
    }
}
