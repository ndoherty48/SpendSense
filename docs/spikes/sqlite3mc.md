# Spike: encrypting SpendSense.db with SQLite3 Multiple Ciphers

- Date: 2026-10-17
- Branch: `spike/sqlite3mc` (throwaway, not for merging as is)
- Tests: `Tests/SpendSense.Tests/Encryption/Sqlite3McSpikeTests.cs` (19 tests, real temp files)

This spike answers four questions before an ADR commits SpendSense to an encrypted database the way Milo has
one: does the engine reject a wrong key, can an existing plaintext database be converted in place, does the
SQLite backup API used by `BackupService` survive encryption (including old plaintext backups), and is
connection pooling safe with a key interceptor. It doesn't build key storage or UI.

## Short answers

| Question | Answer |
|---|---|
| (a) Wrong key | A wrong key or no key fails with `SQLITE_NOTADB` (26) on the first real statement. The key pragma itself always "succeeds". |
| (b) Convert in place | `PRAGMA journal_mode = DELETE` then `PRAGMA hexrekey` works. Rekeying while in WAL mode is refused. A keyed `VACUUM INTO 'file:…?hexkey=…'` also works and leaves the source untouched. |
| (c) Backup API | Encrypted → same key: works. Encrypted → different key: works (pages re-encrypted for the target). Encrypted ↔ plaintext, either way: **refused** (`SQLITE_ERROR`, "backup is not supported with incompatible source and target databases"). Old plaintext backups restore if first staged through a keyed `VACUUM INTO`. |
| (d) Pooling | Pooling works with the interceptor: it re-applies the key on every logical open. But a pooled native handle keeps its key, so code that opens the same connection string without the key still reads the data. Cost: **`hexkey` runs PBKDF2 on every open, about 40 ms**, pooled or not. A raw key (`'raw:<hex>'`) costs about 0.1–0.4 ms. |

The biggest surprise is the last one, and it applies to Milo too: see [Gotchas](#gotchas).

## Versions

| Piece | Version |
|---|---|
| .NET SDK (`global.json`) | `11.0.100-rc.2.26504.105`, `rollForward: disable`, prerelease allowed (same as Milo) |
| Workload set | `11.0.100-rc.2.26513.2` (Milo's CI pin; `maui-android/ios/maccatalyst` `11.0.0-rc.2.26505.5`) |
| Target frameworks | `net11.0`, `net11.0-android`, `net11.0-ios`, `net11.0-maccatalyst` (+ Windows on Windows hosts) |
| EF Core | `Microsoft.EntityFrameworkCore`, `.Design`, `.Sqlite.Core` `11.0.0-rc.2.26504.105` |
| Engine | `SQLite3MC.PCLRaw.bundle` 2.4.0 → `SQLitePCLRaw.core` 3.0.5; reports `SQLite3 Multiple Ciphers 2.4.0`, SQLite 3.53.4, default cipher `chacha20` (sqleet-compatible, `kdf_iter` 64007) |
| MAUI | `Microsoft.Maui.Controls` / `.Core` / `Components.WebView.Maui` `11.0.0-rc.2.26505.5` |
| Other bumps to match Milo | `Microsoft.Extensions.Http.Resilience` / `.ServiceDiscovery` 10.10.0, `Aspire.Hosting.Maui` 13.4.6-preview.1.26319.6, `xunit.v3.mtp-v2` 4.0.2 |

Unchanged and fine on net11: MudBlazor 9.5.0, Plugin.LocalNotification 14.1.0, bUnit 2.11.3. The existing suite
(221 tests) passes on net11 without code changes. Restore resolves only `SQLitePCLRaw.core` 3.0.5: no
`e_sqlite3` native library is pulled in once `.Sqlite` is swapped for `.Sqlite.Core`.

`SQLitePCL.Batteries_V2.Init()` is called at the top of `AddSpendSenseDb()`, in `MyDbContextFactory`
(for `dotnet ef`), and in a `[ModuleInitializer]` in the test project (`Support/SqliteSetup.cs`). The tests also
passed before the module initializer was added: Microsoft.Data.Sqlite looks for `SQLitePCL.Batteries_V2` by
reflection when it starts. Keep the explicit calls anyway, because trimming on iOS can remove a type that is
only found by reflection.

## (a) Opening with the right and wrong key

- EF creates a new database encrypted from its first page when the interceptor runs `PRAGMA hexkey` in
  `ConnectionOpened`. EF still switches a new database to WAL, and WAL works with the cipher.
- A wrong key or no key: `PRAGMA hexkey` returns normally, and the first statement that reads the file throws
  `SqliteException` with `SqliteErrorCode == 26` ("file is not a database"). Through EF, the first query
  throws. The "key missing / wrong" check therefore has to be a probe query (e.g.
  `SELECT COUNT(*) FROM sqlite_master`), not the pragma.
- The file doesn't start with `SQLite format 3\0` once encrypted, which gives a cheap "is this plaintext?"
  check for migration.

## (b) Converting an existing plaintext database

The app's database today is created by EF in WAL mode. Once EF's last connection closes (unpooled), the
-wal and -shm files are checkpointed away. With the default pooling they stay until the pool is cleared.

| Approach | Result |
|---|---|
| `PRAGMA hexrekey` while in WAL mode, after anything has read the file | **Refused**: `SQLITE_ERROR`, "Rekeying an unencrypted database is not supported in WAL journal mode." The file is untouched. |
| `PRAGMA hexrekey` as the *first* statement on a fresh connection, in WAL mode | Slips past the check (sqlite3mc doesn't know it's WAL yet), and every re-encrypted page goes into the -wal (≈ the whole DB). The main file stays plaintext until the checkpoint at close. The result was sound in testing, but this is the unsupported path, so **don't rely on it**. |
| `PRAGMA journal_mode = DELETE`, then `PRAGMA hexrekey` | **Works.** -wal/-shm are removed by the mode switch and none are left after the rekey. Data intact, file encrypted, `integrity_check` ok. WAL can be switched back on afterwards. |
| `journal_mode = DELETE` while another connection (including a closed *pooled* one) is open | `SQLITE_BUSY` (5, "database is locked"). Call `SqliteConnection.ClearAllPools()` first. |
| `VACUUM INTO 'file:<target>?hexkey=<hex>'` from the plaintext source (opened read-only) | **Works.** Target is encrypted with that key, in DELETE mode. The source and its WAL are untouched. `?key=raw:<hex>` works too. |
| `VACUUM INTO '<plain path>'` | Copies the *source's* cipher: plaintext from plaintext, encrypted with the same key from an encrypted source. |

Recommended conversion, at startup before EF opens anything:

1. `SqliteConnection.ClearAllPools()`; if the file has the plaintext header, open it unpooled.
2. `PRAGMA journal_mode = DELETE;` then `PRAGMA rekey = 'raw:<hex>';` (or `hexrekey`, see (d)); close.
3. Let EF open it keyed. EF only sets WAL when it creates a database, so set `PRAGMA journal_mode = WAL` once
   explicitly if WAL is still wanted.

The alternative (keyed `VACUUM INTO` a new file, then swap files) never modifies the original until the swap.
It needs twice the disk space and a careful rename, and it also leaves the old plaintext file's blocks on flash.
Rekey in place runs inside one rollback-journal transaction, so it is crash-safe the way any SQLite write is.
Neither approach scrubs plaintext that the flash storage has already written elsewhere.

## (c) The backup API (`SqliteConnection.BackupDatabase`)

`BackupService` uses it three ways: `CreateBackup` (live → new file), `SaveSafetyCopy` (live → file) and
`Restore` (backup file → live).

| Source → target | Result |
|---|---|
| Encrypted (key A) → new file keyed with A | Works; target readable with A only. |
| Encrypted (A) → new file keyed with B | Works: the target is re-encrypted with B. Readable with B only. A cheap way to give an exported backup its own key. |
| Encrypted (A) → plaintext file | **Fails**: `SQLITE_ERROR` (1) "backup is not supported with incompatible source and target databases". |
| Plaintext backup → encrypted live DB (today's `Restore`) | **Fails** the same way; the live DB is left unchanged. |
| Plaintext backup → keyed `VACUUM INTO` staging file → backup API into encrypted live DB | **Works**; live DB stays encrypted and in WAL mode, with the backup's data. |
| Encrypted → encrypted live DB in WAL mode | Works. |

So with encryption:

- `CreateBackup` / `SaveSafetyCopy` must key the target connection before `BackupDatabase`, with the device key
  or a separate backup key. Which key is an ADR decision: a device-key backup can't be restored on a new phone.
  Milo uses a separate backup key.
- `Restore` must check whether the file is plaintext (header check), and if so stage it through
  `VACUUM INTO 'file:<staging>?…key…'` before `BackupDatabase`. Old `.db` backups then keep restoring.
- `Inspect` opens the backup read-only with no key; an encrypted backup would show as "isn't a SpendSense
  backup" (26 is caught as `SqliteException`). It needs to try the key, or the header check, first.
- `ATTACH` on a keyed connection uses the main database's key unless told otherwise. Attaching a plaintext
  file fails with 26, and `ATTACH … KEY ''` reads it. Useful for a row-by-row import, not needed by the plan
  above.

## (d) Connection pooling and per-open cost

**Pooling is functionally OK.** EF raises `ConnectionOpened` on every logical open, even when the pool hands back
an already-open native handle, so the interceptor re-applies the key each time (5 contexts → 5 key pragmas).
Re-keying an already-keyed handle is fine; re-keying it with a wrong key locks it again (26), and the right key
unlocks it.

**But a pooled handle keeps its key.** After one keyed open, a `new SqliteConnection(sameConnectionString)` with
no key pragma at all reads the data, because it gets the keyed handle back. With pooling, whether a connection
is keyed depends on the pool, not on the interceptor. Inside the app that is harmless: it's the app's own
key. But anything that wants to prove the key is right (a "key missing" check) must use an unpooled connection
or clear the pool first.

**The KDF cost** (Intel i7-9750H, x64, Debug; mean over 20 opens + one `COUNT(*)`, two runs):

| Key form | Unpooled | Pooled |
|---|---|---|
| `PRAGMA hexkey = '<64 hex>'` | 38–42 ms | 36–42 ms |
| `PRAGMA key = 'raw:<64 hex>'` (= `"x'<64 hex>'"`) | 0.3–0.4 ms | 0.1–0.2 ms |
| `Password=<passphrase>` keyword, pooled | — | 0.04–0.07 ms |
| Plaintext | 1.2–1.7 ms | 0.02–0.03 ms |

- `hexkey` only *decodes* the hex. The bytes are then treated as a passphrase and run through chacha20's
  PBKDF2 (`kdf_iter` 64007). This is on every open, and pooling doesn't help, because the interceptor re-keys the
  handle each time. SpendSense opens a connection per `DbContext` operation, so a page with several queries
  would add hundreds of ms. Expect several times worse on a low-end Android phone.
- `'raw:<hex>'` and `"x'<hex>'"` are the same thing (each opens the other's database) and skip the KDF. They are
  a *different key* from `hexkey` of the same bytes, so pick one form before any user data is encrypted.
  `PRAGMA rekey = 'raw:<hex>'` and `?key=raw:<hex>` in a `VACUUM INTO` URI work too.
- The `Password=` keyword (Microsoft.Data.Sqlite runs `PRAGMA key` only when it opens a *new* native handle) hides
  the KDF behind pooling. It puts the key in the connection string, which Milo avoided to keep it out of logs.
- Recommendation: a raw 256-bit key via the interceptor (`PRAGMA key = 'raw:…'`), with pooling left on.
  Unpooled with a raw key is also cheap, if the "pool keeps the key" behaviour is unwanted.

## Platform builds and native loading

- `net11.0` (tests): builds and runs; loads `runtimes/osx-x64/native/libsqlite3mc.dylib`.
- `net11.0-maccatalyst`, `net11.0-ios`: **not built here**: this Mac has only Command Line Tools, no Xcode
  ("Could not find a valid Xcode developer path"). Needs a machine with Xcode 26 or the CI `macos-26` runner.
- `net11.0-android`: see "Android build" below.

What the packages ship (`SQLite3MC.PCLRaw.lib` 2.4.0):

- **iOS**: `runtimes/ios-arm64/native/sqlite3mc.a` and `iossimulator-{arm64,x64}/native/sqlite3mc.a`, a
  *static* library, and the provider has a `net9.0-ios18.0` build that binds to it. Its `buildTransitive`
  targets only add a `NativeFileReference` for `browser-wasm`, so iOS linking relies on the .NET iOS SDK picking
  up `.a` files from NuGet `runtimes/<rid>/native`. Verify with a device/simulator build that
  `sqlite3mc_version()` returns, and that `Batteries_V2.Init()` isn't trimmed. If it fails to link, add
  `<NativeReference Include="…/sqlite3mc.a" Kind="Static" ForceLoad="true" />`.
- **Mac Catalyst**: `maccatalyst-{arm64,x64}/native/libsqlite3mc.dylib`, a dynamic library. Check it is
  embedded and signed in the `.app` (Milo ships the same).
- **Android**: `android-{arm,arm64,x86,x64}/native/libsqlite3mc.so`.
- Windows: `win-{x64,x86,arm64}`. Milo's CI builds those.

## Gotchas

1. **`hexkey` is not a raw key.** It costs ≈40 ms per open on a desktop CPU. Milo's
   `docs/design/data-protection.md` says it "skips the passphrase KDF"; it doesn't, and Milo runs with
   `Pooling=False`, so it pays this on every open. Moving to `'raw:'` changes the effective key, so existing Milo
   vaults would need a one-off `PRAGMA rekey = 'raw:…'`.
2. Rekeying a plaintext DB in WAL mode is refused, except as the first statement on a connection, where it
   slips past the check. Always switch to DELETE first.
3. Closed pooled connections still hold the file: `journal_mode` changes fail with `SQLITE_BUSY` until
   `ClearAllPools()`. `ClearAllPools()` is process-wide: test classes that call it (via `TempFileSystem`) broke
   the pooling tests when run in parallel, so the spike class runs in its own non-parallel collection.
4. The key pragma never fails on a wrong key; only the next read does.
5. The backup API won't mix plaintext and encrypted databases in either direction: every `BackupService` path
   needs a keyed connection on both ends, plus staging for legacy plaintext backups.

## What it would take (for the ADR)

- .NET 11 / EF Core 11 RC 2 across the solution (this branch), and CI pinned to the workload set
  (`dotnet workload install maui --version 11.0.100-rc.2.26513.2`; `tests.yml` currently installs the latest
  `maui`, which won't match an RC SDK).
- A device key in SecureStorage / Keystore / DPAPI (not built), applied with `PRAGMA key = 'raw:…'` from a
  `DbConnectionInterceptor`.
- Startup conversion of an existing plaintext DB (above), before migrations.
- `BackupService`: keyed backup targets, a decision on the backup key, header-aware `Inspect`, and the staged
  restore for plaintext backups.
