using System;
using System.Collections.Generic;
using System.Globalization;

namespace DuckDB.NET.Data.Profiling;

/// <summary>
/// Strongly-typed, immutable view over profiling metric entries that can be created from
/// an <see cref="IDictionary{string, object}"/> and exported back to a dictionary.
/// </summary>
public readonly record struct ProfilingInfoMetrics(
    double? ElapsedMilliseconds,
    long? Rows,
    long? ResultSetSize,
    long? ScannedRows,
    long? BytesRead,
    long? BytesWritten,
    long? TotalMemoryAllocated,
    long? PeakMemoryAllocation,
    int? Spills,
    double? WalWriteLatencyMilliseconds,
    string? Statement)
{

    public static ProfilingInfoMetrics FromDictionary(IDictionary<string, object> dict)
    {
        if (dict is null) throw new ArgumentNullException(nameof(dict));

        static double? ToNullableDouble(object? v)
        {
            if (v is null) return null;
            if (v is double d) return d;
            if (v is float f) return Convert.ToDouble(f);
            if (v is decimal dec) return Convert.ToDouble(dec);
            if (v is long l) return Convert.ToDouble(l);
            if (v is int i) return Convert.ToDouble(i);
            if (v is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var r)) return r;
            if (v is TimeSpan ts) return ts.TotalMilliseconds;
            if (v is IConvertible conv) return Convert.ToDouble(conv);
            return null;
        }

        static long? ToNullableLong(object? v)
        {
            if (v is null) return null;
            if (v is long l) return l;
            if (v is int i) return i;
            if (v is short s) return s;
            if (v is double d) return Convert.ToInt64(d);
            if (v is decimal dec) return Convert.ToInt64(dec);
            if (v is string str && long.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var r)) return r;
            if (v is IConvertible conv) return Convert.ToInt64(conv);
            return null;
        }

        static int? ToNullableInt(object? v)
        {
            if (v is null) return null;
            if (v is int i) return i;
            if (v is long l) return (int)l;
            if (v is string s && int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var r)) return r;
            if (v is IConvertible conv) return Convert.ToInt32(conv);
            return null;
        }

        dict.TryGetValue("Elapsed", out var elapsed);
        dict.TryGetValue("Rows", out var rows);
        dict.TryGetValue("ResultSetSize", out var resultSetSize);
        dict.TryGetValue("ScannedRows", out var scannedRows);
        dict.TryGetValue("BytesRead", out var bytesRead);
        dict.TryGetValue("BytesWritten", out var bytesWritten);
        dict.TryGetValue("TotalMemoryAllocated", out var totalMemoryAllocated);
        dict.TryGetValue("PeakMemoryAllocation", out var peakMemoryAllocation);
        dict.TryGetValue("Spills", out var spills);
        dict.TryGetValue("WALWriteLatency", out var walWriteLatency);
        dict.TryGetValue("Statement", out var statement);

        return new ProfilingInfoMetrics(
            ElapsedMilliseconds: ToNullableDouble(elapsed),
            Rows: ToNullableLong(rows),
            ResultSetSize: ToNullableLong(resultSetSize),
            ScannedRows: ToNullableLong(scannedRows),
            BytesRead: ToNullableLong(bytesRead),
            BytesWritten: ToNullableLong(bytesWritten),
            TotalMemoryAllocated: ToNullableLong(totalMemoryAllocated),
            PeakMemoryAllocation: ToNullableLong(peakMemoryAllocation),
            Spills: ToNullableInt(spills),
            WalWriteLatencyMilliseconds: ToNullableDouble(walWriteLatency),
            Statement: statement?.ToString());
    }

    public IDictionary<string, object?> ToDictionary()
    {
        var dict = new Dictionary<string, object?>(11);

        if (ElapsedMilliseconds is not null) dict["Elapsed"] = ElapsedMilliseconds;
        if (Rows is not null) dict["Rows"] = Rows;
        if (ResultSetSize is not null) dict["ResultSetSize"] = ResultSetSize;
        if (ScannedRows is not null) dict["ScannedRows"] = ScannedRows;
        if (BytesRead is not null) dict["BytesRead"] = BytesRead;
        if (BytesWritten is not null) dict["BytesWritten"] = BytesWritten;
        if (TotalMemoryAllocated is not null) dict["TotalMemoryAllocated"] = TotalMemoryAllocated;
        if (PeakMemoryAllocation is not null) dict["PeakMemoryAllocation"] = PeakMemoryAllocation;
        if (Spills is not null) dict["Spills"] = Spills;
        if (WalWriteLatencyMilliseconds is not null) dict["WALWriteLatency"] = WalWriteLatencyMilliseconds;
        if (Statement is not null) dict["Statement"] = Statement;

        return dict;
    }
}
