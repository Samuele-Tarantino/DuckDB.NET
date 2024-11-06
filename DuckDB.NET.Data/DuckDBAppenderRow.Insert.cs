using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Numerics;

using DuckDB.NET.Data.Extensions;
using DuckDB.NET.Native;

namespace DuckDB.NET.Data;

partial class DuckDBAppenderRow {

    public DuckDBAppenderRow InsertValue(object? value, int columnOrdinal) {

        if (value.IsNull())
            InsertValueInternal((int?)null, columnOrdinal);
        else if (value is bool || value is SqlBoolean)
            InsertValueInternal((bool)value, columnOrdinal);
        else if (value is BigInteger)
            InsertValueInternal((BigInteger)value, columnOrdinal);
        else if (value is long || value is SqlInt64)
            InsertValueInternal((long)value, columnOrdinal);
        else if (value is int || value is SqlInt32)
            InsertValueInternal((int)value, columnOrdinal);
        else if (value is short || value is SqlInt16)
            InsertValueInternal((short)value, columnOrdinal);
        else if (value is ulong)
            InsertValueInternal((ulong)value, columnOrdinal);
        else if (value is uint)
            InsertValueInternal((uint)value, columnOrdinal);
        else if (value is ushort)
            InsertValueInternal((ushort)value, columnOrdinal);
        else if (value is byte || value is SqlByte)
            InsertValueInternal((byte)value, columnOrdinal);
        else if (value is byte[] || value is SqlBinary)
#if NET6_0_OR_GREATER
            InsertValueInternal((byte[])value, columnOrdinal);
#else
            throw new NotSupportedException();
#endif
        else if (value is string || value is SqlString)
            InsertValueInternal((string)value, columnOrdinal);
#if NET6_0_OR_GREATER
        else if (value is DateOnly)
            InsertValueInternal((DateOnly)value, columnOrdinal);
        else if (value is TimeOnly)
            InsertValueInternal((TimeOnly)value, columnOrdinal);
#endif
        else if (value is DuckDBDateOnly)
            InsertValueInternal((DuckDBDateOnly)value, columnOrdinal);
        else if (value is DuckDBTimeOnly)
            InsertValueInternal((DuckDBTimeOnly)value, columnOrdinal);
        else if (value is DateTime || value is SqlDateTime)
            InsertValueInternal((DateTime)value, columnOrdinal);
        else if (value is DateTimeOffset)
            InsertValueInternal((DateTimeOffset)value, columnOrdinal);
        else if (value is TimeSpan)
            InsertValueInternal((TimeSpan)value, columnOrdinal);
        else if (value is decimal || value is SqlDecimal)
            InsertValueInternal((decimal)value, columnOrdinal);
        else if (value is double || value is SqlDouble)
            InsertValueInternal((double)value, columnOrdinal);
        else if (value is float)
            InsertValueInternal((float)value, columnOrdinal);
        else if (value is Guid || value is SqlGuid)
            InsertValueInternal((Guid)value, columnOrdinal);
        else
            throw new NotImplementedException(value?.GetType().FullName);

        return this;
    }

    public DuckDBAppenderRow InsertNullValue(int columnOrdinal) => InsertValueInternal<int?>(null, columnOrdinal); //Doesn't matter what type T we pass to Append when passing null.

    public DuckDBAppenderRow InsertValue(bool? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

#if NET6_0_OR_GREATER

    public DuckDBAppenderRow InsertValue(byte[]? value, int columnOrdinal) => InsertSpan(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(Span<byte> value, int columnOrdinal) => InsertSpan(value, columnOrdinal);
#endif

    public DuckDBAppenderRow InsertValue(string? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(decimal? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(Guid? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(BigInteger? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    #region Append Signed Int

    public DuckDBAppenderRow InsertValue(sbyte? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(short? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(int? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(long? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    #endregion

    #region Append Unsigned Int

    public DuckDBAppenderRow InsertValue(byte? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(ushort? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(uint? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(ulong? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    #endregion

    #region Append Float

    public DuckDBAppenderRow InsertValue(float? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(double? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    #endregion

    #region Append Temporal
#if NET6_0_OR_GREATER
    public DuckDBAppenderRow InsertValue(DateOnly? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(TimeOnly? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);
#endif

    public DuckDBAppenderRow InsertValue(DuckDBDateOnly? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(DuckDBTimeOnly? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);


    public DuckDBAppenderRow InsertValue(DateTime? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(DateTimeOffset? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    public DuckDBAppenderRow InsertValue(TimeSpan? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    #endregion

    #region Composite Types

    public DuckDBAppenderRow InsertValue<T>(IEnumerable<T>? value, int columnOrdinal) => InsertValueInternal(value, columnOrdinal);

    #endregion

    private DuckDBAppenderRow InsertValueInternal<T>(T? value, int columnOrdinal) {
        insertMode = true;

        CheckColumnAccess(columnOrdinal);

        vectorWriters[columnOrdinal].WriteValue(value, rowIndex);

        columnCompiledArray[columnOrdinal] = true;

        return this;
    }

#if NET6_0_OR_GREATER

    private unsafe DuckDBAppenderRow InsertSpan(Span<byte> val, int columnOrdinal) {
        if (val == null) {
            return AppendNullValue();
        }

        CheckColumnAccess(columnOrdinal);

        fixed (byte* pSource = val) {
            vectorWriters[columnOrdinal].AppendBlob(pSource, val.Length, rowIndex);
        }

        return this;
    }

#endif

    private void CheckColumnAccess(int columnOrdinal) {
        if (columnOrdinal >= vectorWriters.Length)
            throw new IndexOutOfRangeException($"The table {qualifiedTableName} has {vectorWriters.Length} columns but you are trying to append value for column {columnOrdinal + 1}");
    }

}
