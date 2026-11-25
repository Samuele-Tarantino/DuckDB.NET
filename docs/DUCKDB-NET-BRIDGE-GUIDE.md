# Guida: usare l'estensione `bridge` con DuckDB.NET (Irion)

**Per chi è:** chi scrive o mantiene un host .NET (o il suo assistente AI) che usa `Irion.DuckDB.NET.Data.Full` / `Irion.DuckDB.NET.Bindings.Full` con la libreria nativa `Irion.DuckDb.Native.Bridge`.
**Cosa descrive:** come il pacchetto si collega alla libreria nativa, la C API del bridge esposta in C# (`DuckDB.NET.Native.Bridge.BridgeMethods`) e le regole d'uso lato .NET. La guida completa dell'estensione (funzioni SQL, pushdown, log, thread, memoria, limiti) è nel pacchetto nativo.
**Fonti di verità:** [examples/BridgeQuickStart/Program.cs](examples/BridgeQuickStart/Program.cs) (compilato ed eseguito, exit code 0) e `BridgeMethodsTests` (23 test, `DuckDB.NET.Test/Bridge/` nel repo `DuckDB.NET`). Se un comportamento non è lì né nella guida del bridge, non è garantito.

## 1. La libreria nativa

- La libreria nativa **non** è più quella ufficiale di DuckDB: è `Irion.DuckDb.Native.Bridge` (DuckDB 2.0 con l'estensione `bridge` collegata dentro `duckdb.dll` / `libduckdb.so`). Nessun `LOAD`, nessuna `.duckdb_extension`.
- `Irion.DuckDB.NET.Bindings.Full` ha una **dipendenza** da quel pacchetto, per qualunque `BuildType`; `Data` dipende da Bindings. Chi referenzia Data o Bindings riceve la libreria in `runtimes/<rid>/native/` nella cartella di output (`win-x64` e `linux-x64`), senza altro. I pacchetti `Bindings`/`Data` **non** contengono dll native.
- La versione della libreria nativa è nel `PackageReference` di `Bindings.csproj` (e nelle dipendenze del `.nuspec` del pacchetto). Per cambiarla: pubblicare il nuovo `Irion.DuckDb.Native.Bridge`, aggiornare la versione, rilanciare i test, rigenerare i pacchetti.
- **Una sola libreria per processo.** Gli handle (`DuckDBConnection.NativeConnection`, ...) valgono solo con questa libreria: non mischiarli con un altro DuckDB caricato nel processo.
- Il pacchetto nativo contiene la propria documentazione, della versione del pacchetto: `~/.nuget/packages/irion.duckdb.native.bridge/<versione>/docs/` (`HOST-GUIDE.md` è il riferimento per l'host, `host-claude-snippet.md`, `src/include/bridge_capi.hpp`, esempi). **Usare quella della versione referenziata.**

## 2. Vocabolario

| Termine | Cosa è |
|---|---|
| **Delt** | Istanza DuckDB che esegue le query, senza credenziali. In C#: una `DuckDBConnection` |
| **Istanza** (A, B, ...) | Istanza DuckDB **privata** con le credenziali di *una* fonte (secret, `ATTACH`, estensioni). L'host la prepara, il bridge la usa |
| **Isoldata** | Database della Delt dove vivono tabelle e viste degli esecutori (tabelle di output) |
| **Batch** | Script del designer eseguito sulla sessione di un'istanza (`CONNECT` ... `DISCONNECT`) |

## 3. La C API in C#

`DuckDB.NET.Native.Bridge.BridgeMethods` (assembly `Irion.DuckDB.NET.Bindings`). Ogni funzione ha un overload "raw" (`Span<byte> error, int errorSize`) e uno comodo con `out string error` (buffer da 1024 byte, già decodificato). Ritornano `DuckDBState.Success` / `DuckDBState.Error`; il messaggio ha il prefisso `Invalid Input Error: bridge: ...`.

| Metodo | Funzione C | Cosa fa |
|---|---|---|
| `RegisterInstance(delt, name, istanza, out error)` | `bridge_register_instance` | Registra l'istanza `name` sulla Delt. L'istanza deve essere **diversa** dalla Delt; il nome non distingue maiuscole/minuscole e non si registra due volte. Il bridge tiene viva l'istanza: l'host può chiudere la sua connessione |
| `UnregisterInstance(delt, name, out error)` | `bridge_unregister_instance` | Toglie l'istanza. I cataloghi attaccati smettono di funzionare con errore chiaro |
| `WriteStats(connessioneA, out stats, out error)` | `bridge_write_stats` | Esito dell'ultima scrittura da A (§6): `Rows`, `Bytes`, `DurationMs`, `Outcome` (`None`, `Committed`, `RolledBack`) |

Le istanze si ottengono con `new DuckDBConnection("DataSource=:memory:")` + `Open()`: ogni connection string `:memory:` non condivisa apre un'istanza **nuova e indipendente** (due istanze separate = Delt e A). `DuckDBConnection.Duplicate()` invece apre un'altra connessione **sulla stessa** istanza. Gli handle da passare sono `connection.NativeConnection`, aperti per tutta la chiamata.

Le funzioni SQL e le impostazioni (`ATTACH 'bridge:<istanza>'`, `bridge_instances()`, `bridge_remote(...)`, `bridge_explain_analyze(...)`, `bridge_enable_filter_pushdown`, `bridge_enable_projection_pushdown`, `bridge_batch_queue_max_bytes`) non sono C API: si usano con SQL normale ed sono descritte nella guida del pacchetto nativo.

## 4. Il protocollo, passo per passo

```text
1. delt = new DuckDBConnection("DataSource=:memory:"); Open()           (nessuna credenziale)
2. istanza = new DuckDBConnection("DataSource=:memory:"); Open()         (UNA istanza per fonte)
3. setup dell'istanza, solo a livello di ISTANZA:                        CREATE TEMPORARY SECRET, LOAD, ATTACH, SET, viste
                                                                         (mai "CREATE SECRET ... IN connection")
4. BridgeMethods.RegisterInstance(delt, "A", istanza)                    PRIMA di ogni ATTACH
5. SQL sulla Delt (DuckDBCommand):                                       ATTACH 'bridge:A' AS dba [(READ_ONLY)]
                                                                         SELECT ... FROM dba.ordini o JOIN locale l ON ...
                                                                         CREATE TABLE isoldata.x AS SELECT * FROM dba.ordini WHERE ...
6. batch del designer (§5):                                              CONNECT dba; ...; DISCONNECT   (duckdb_query!)
7. chiusura (§7):                                                        DETACH, UnregisterInstance; Delt, POI istanza
```

Non c'è più abilitazione per connessione: registrare l'istanza basta, e qualunque connessione della Delt può fare `ATTACH 'bridge:A'` e usarla. **Conseguenza di sicurezza:** chi può aprire una connessione sulla Delt usa **tutte** le istanze registrate (e, senza `READ_ONLY`, ci può scrivere). Se serve separare, l'host lo fa a monte: chi riceve una connessione, quali istanze registra, `ATTACH ... (READ_ONLY)`.

`ATTACH ... (READ_ONLY)` lascia passare solo `SELECT` e comandi di transazione: scritture, `CREATE TEMP TABLE` e il batch del §5 sono rifiutati. Per il batch serve un `ATTACH` senza `READ_ONLY`.

## 5. Il batch del designer: `CONNECT` / `DISCONNECT` e `duckdb_query`

Dopo `CONNECT <alias>` ogni statement della connessione va alla **sessione dell'istanza**, fino a `DISCONNECT`. Regole lato .NET:

- **`DuckDBCommand` non si può usare fra `CONNECT` e `DISCONNECT`.** ADO.NET passa dalla prepare, e sotto `CONNECT` DuckDB 2.0.0 risponde `INTERNAL Error: PREPARE succeeded but the prepared statement was not registered` *dopo* aver già eseguito lo statement (difetto del motore, [duckdb/duckdb#27269](https://github.com/duckdb/duckdb/issues/27269), riferito come risolto in DuckDB 2.0.1: da verificare quando la libreria nativa passa a quella versione).
- Fra `CONNECT` e `DISCONNECT` (compresi) si usa `duckdb_query`: `NativeMethods.Query.DuckDBQuery(connection.NativeConnection, sql, out var result)`, una SQL alla volta, poi `DuckDBResultError(ref result)` se fallisce e **sempre** `DuckDBDestroyResult(ref result)` (anche dopo un errore). Fuori dal batch `DuckDBCommand` va bene (anche `ATTACH` e il CTAS dell'output).
- **Una connessione della Delt per tutto il batch**: la sessione è della connessione. Un'altra connessione non vede le TEMP né la transazione.
- Dopo il primo statement sotto `CONNECT` le letture di quel catalogo, da quella connessione, vanno sulla sessione (vedono TEMP e transazione) anche dopo `DISCONNECT`.
- La Delt è in autocommit: l'output già scritto (`CREATE TABLE isoldata.out ...`) **non si annulla** con il `ROLLBACK` del designer su A.
- Un errore dentro una transazione la invalida: serve `ROLLBACK` sotto `CONNECT`. Un `COMMIT` dopo un errore risponde «riuscito» ma non conferma nulla.
- Una connessione dell'istanza regge **uno stream alla volta**: con la bandiera alzata due letture della sessione nella stessa query danno errore (non una coda). Dettagli e rimedi (nomi a quattro parti, `bridge_remote`) nella guida del pacchetto nativo.

## 6. Scrittura da A nell'Isoldata e metriche

Per far scrivere l'output direttamente ad A (invece che alla Delt che legge da A):

```csharp
// la Delt diventa un'istanza di A, e A la attacca SENZA READ_ONLY
BridgeMethods.RegisterInstance(istanza.NativeConnection, "isoldata", delt.NativeConnection, out var errore);
Execute(istanza, "ATTACH 'bridge:isoldata' AS isoldata");

// statement sulla connessione di A, SENZA CONNECT
Execute(istanza, "CREATE TABLE isoldata.main.t AS SELECT ... FROM <oggetti di A>");
Execute(istanza, "INSERT INTO isoldata.main.t SELECT ... FROM <oggetti di A>");

// subito dopo (anche se lo statement e' fallito), sulla STESSA connessione di A:
BridgeMethods.WriteStats(istanza.NativeConnection, out var stats, out errore);
```

- `stats.Outcome`: `Committed`, `RolledBack` (righe e byte sono quelli raggiunti prima dell'annullamento) o `None` (nessuna scrittura contata).
- **Ogni statement che parte sulla connessione azzera i numeri** (anche `ROLLBACK` o una `SELECT`): si leggono subito dopo la scrittura. Non sono cumulativi: se serve il totale lo somma l'host.
- `Bytes` sono i byte di dati copiati dal sink, **non compressi**: non la dimensione su disco dell'Isoldata.
- Una `CTAS` o `INSERT` **spinta intera** alla Delt (sorgente solo costante o solo dell'Isoldata) non passa dal sink e dà `None`.
- L'handle da passare è la connessione di A che ha eseguito lo statement, non l'alias e non la connessione della Delt (se è la Delt a scrivere in una fonte, vale la connessione della Delt).
- La scrittura è atomica per istruzione ma il commit è l'ultimo atto dell'istruzione, non del batch: il `ROLLBACK` del designer su A non toglie le righe già scritte. Le scritture sull'Isoldata non devono essere concorrenti (le serializza l'host).
- Se l'Isoldata è un file attaccato alla Delt (non il database predefinito) il catalogo va nel nome: `isoldata.<catalogo>.t` (tre parti nel `CREATE`). `isoldata.main.t` scrive nel database **predefinito** della Delt.

## 7. Ciclo di vita e regole che rompono tutto se violate

1. **Ordine di chiusura:** `DETACH` / `UnregisterInstance`, poi la **Delt**, poi le **istanze**. Attenzione alle `using`: due `using var` dichiarate come Delt e poi istanza si chiudono al contrario (istanza prima della Delt), quindi nell'esempio la chiusura è esplicita, nel `finally`.
2. **Registrare prima di usare:** `RegisterInstance` prima di ogni `ATTACH 'bridge:...'`. Un file Isoldata con viste su `dba.<oggetto>` non si riapre da solo: la vista dipende da un'istanza registrata nel processo.
3. **Il setup dell'istanza è a livello di istanza** (`CREATE TEMPORARY SECRET`, mai `IN connection`) e finito prima della registrazione.
4. **Una connessione, uno stream:** non eseguire un'altra query su una connessione con un risultato aperto.
5. **Nessun secret nelle SQL** del designer né nei log: restano nel setup dell'istanza. A livello DEBUG il log scrive la SQL inviata.
6. **Non usare la libreria del bridge insieme a un'altra DuckDB** passando handle dall'una all'altra.
7. **Stringhe UTF-8** (i P/Invoke di `BridgeMethods` usano `StringMarshalling.Utf8`); il bridge le copia, valgono solo durante la chiamata.
8. **Impostazioni `bridge_*`:** si impostano con `SET` **dopo** l'apertura, mai con `duckdb_set_config` all'apertura (l'apertura fallisce). `bridge_batch_queue_max_bytes` si imposta sull'istanza A.
9. **Rientri e backlink** (A legge un oggetto dell'Isoldata): l'oggetto nell'Isoldata deve essere **confermato** prima; un ciclo è rifiutato a runtime con `bridge: ciclo di backlink: ...`. Prima di chiudere si deregistrano le istanze reciproche.
10. Il push (`BRIDGE_PUSH`) non esiste più: le letture sono sempre pull.

## 8. Non provato

- **Linux:** la libreria `linux-x64` è nel pacchetto nativo ma i test di questo repo (`BridgeMethodsTests`) sono eseguiti solo su Windows.
- **`WriteStats` con esito `RolledBack`:** i test coprono `Committed` e `None`, non una scrittura fallita a metà.
- La CI su GitHub non raggiunge la feed interna (`telemaco.intranet.irion.it`): il restore dei pacchetti fallisce lì.
