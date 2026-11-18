// Esempio minimo: DuckDB.NET (Irion.DuckDB.NET.Data.Full) + estensione bridge (Irion.DuckDb.Native.Bridge).
// Delt e istanza A sono due istanze DuckDB indipendenti nello stesso processo. Si esegue con exit code 0.
// Per provarlo: un progetto console net10.0 che referenzia Irion.DuckDB.NET.Data.Full e include questo file.

using DuckDB.NET.Data;
using DuckDB.NET.Native;
using DuckDB.NET.Native.Bridge;

// "DataSource=:memory:" apre ogni volta un'istanza NUOVA e indipendente (non condivisa).
var delt = new DuckDBConnection("DataSource=:memory:");      // legge, nessuna credenziale
var istanza = new DuckDBConnection("DataSource=:memory:");   // possiede la fonte (secret, ATTACH, ...)
delt.Open();
istanza.Open();

// Setup dell'istanza A: solo a livello di istanza (mai "CREATE SECRET ... IN connection").
Execute(istanza, "CREATE TABLE ordini AS SELECT range AS id, 'cliente_' || (range % 3) AS cliente FROM range(10)");

try
{
    // 1. Registrare l'istanza sulla Delt PRIMA di ogni ATTACH.
    Check(BridgeMethods.RegisterInstance(delt.NativeConnection, "A", istanza.NativeConnection, out var errore), errore);

    // 2. Lettura: la Delt vede l'istanza come catalogo attaccato. Con (READ_ONLY) passano solo SELECT e comandi di
    //    transazione: va bene per sola lettura, ma NON per il batch del punto 3 (che scrive nella sessione).
    Execute(delt, "ATTACH 'bridge:A' AS dba");
    Console.WriteLine($"righe in dba.ordini: {Scalar(delt, "SELECT count(*) FROM dba.ordini")}");
    Execute(delt, "CREATE TABLE locale AS SELECT range AS id FROM range(4)");
    Console.WriteLine($"join con una tabella locale: {Scalar(delt, "SELECT count(*) FROM dba.ordini o JOIN locale l ON o.id = l.id")}");

    // 3. Batch del designer: tra CONNECT e DISCONNECT ogni statement va alla SESSIONE dell'istanza e si manda con
    //    duckdb_query. DuckDBCommand (che fa la prepare) NON si puo' usare qui dentro.
    Query(delt, "CONNECT dba");
    Query(delt, "BEGIN");
    Query(delt, "CREATE TEMP TABLE tmp AS SELECT 42 AS x");
    Query(delt, "DISCONNECT");
    Console.WriteLine($"TEMP della sessione, letta dalla Delt: {Scalar(delt, "SELECT x FROM dba.tmp")}");
    Query(delt, "CONNECT dba");
    Query(delt, "ROLLBACK");
    Query(delt, "DISCONNECT");

    // 4. Scrittura da A nell'Isoldata (la Delt): registrare la Delt su A, attaccarla SENZA READ_ONLY ed eseguire
    //    lo statement sulla connessione di A, senza CONNECT.
    Check(BridgeMethods.RegisterInstance(istanza.NativeConnection, "isoldata", delt.NativeConnection, out errore), errore);
    Execute(istanza, "ATTACH 'bridge:isoldata' AS isoldata");
    Execute(istanza, "CREATE TABLE isoldata.main.copia AS SELECT id, cliente FROM ordini");

    // Le metriche si leggono SUBITO dopo lo statement, sulla connessione di A che lo ha eseguito.
    Check(BridgeMethods.WriteStats(istanza.NativeConnection, out var stats, out errore), errore);
    Console.WriteLine($"scrittura da A: {stats.Rows} righe, {stats.Bytes} byte, {stats.DurationMs} ms, esito {stats.Outcome}");
    if (stats.Outcome != BridgeWriteOutcome.Committed || stats.Rows != 10)
        throw new InvalidOperationException("esito della scrittura inatteso");
    Console.WriteLine($"righe in copia (sulla Delt): {Scalar(delt, "SELECT count(*) FROM main.copia")}");
}
finally
{
    // Chiusura: deregistrare, poi la Delt, poi l'istanza (l'ordine conta: i buffer dei dati appartengono all'istanza).
    BridgeMethods.UnregisterInstance(istanza.NativeConnection, "isoldata", out _);
    BridgeMethods.UnregisterInstance(delt.NativeConnection, "A", out _);
    delt.Dispose();
    istanza.Dispose();
}

static void Check(DuckDBState stato, string errore)
{
    if (stato != DuckDBState.Success) throw new InvalidOperationException(errore);
}

static void Execute(DuckDBConnection connection, string sql)
{
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    command.ExecuteNonQuery();
}

static object? Scalar(DuckDBConnection connection, string sql)
{
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    return command.ExecuteScalar();
}

// duckdb_query sulla connessione nativa: l'unico modo di eseguire statement fra CONNECT e DISCONNECT.
static void Query(DuckDBConnection connection, string sql)
{
    var stato = NativeMethods.Query.DuckDBQuery(connection.NativeConnection, sql, out var result);
    try
    {
        if (stato != DuckDBState.Success)
            throw new InvalidOperationException(NativeMethods.Query.DuckDBResultError(ref result));
    }
    finally
    {
        NativeMethods.Query.DuckDBDestroyResult(ref result);
    }
}
