# ADR 0004: Encryption at rest

- Status: Accepted
- Date: 2026-10-18
- Amends: [ADR 0003](0003-backup-and-export.md) (decisions 1 and 2: what a backup is and how it restores)
- Background: [`docs/spikes/sqlite3mc.md`](../spikes/sqlite3mc.md)

## Context

`SpendSense.db` holds every transaction, account name, balance and note, in plaintext. Anyone who copies the
file can read it: a file manager on a rooted phone, a lost laptop disk, or an OS backup restored somewhere
else. Backups are worse. ADR 0003 made a backup a copy of the database file, and users are told to keep it in
iCloud Drive or Google Drive, where it can be shared by mistake.

The sibling Milo project already encrypts its database with SQLite3 Multiple Ciphers (sqlite3mc), and
SpendSense moved onto the same engine in ndoherty48/SpendSense#39 without setting a key. The spike answered the risky questions
(wrong key behaviour, converting an existing plaintext database, the backup API, pooling and cost).

| Protects against | Doesn't protect against |
|---|---|
| The database file being copied off the device | Someone using an unlocked device with SpendSense open (an app lock is a separate, later feature) |
| An OS backup or file sync picking up readable data | Malware running as the user on an unlocked device |
| A backup file being found in a cloud folder or shared by mistake | A forgotten backup password with no recovery code: that backup is unreadable by design |
| A backup file being edited or corrupted unnoticed | CSV exports, which are plaintext spreadsheets by design |

There is no unlock screen. A budgeting app that asks for a password before showing this month's spending would
be used less, and the device's own lock already guards an unlocked phone.

## Decisions

### 1. The database is encrypted with a random device key

- **Engine:** sqlite3mc's default cipher, ChaCha20-Poly1305. Every page is encrypted and authenticated.
- **Key:** 32 random bytes from `RandomNumberGenerator`, created on first launch. It's applied as raw key
  material, `PRAGMA key = 'raw:<hex>'`, by `DatabaseKeyInterceptor` as the first statement on every connection
  EF opens. The raw form skips the cipher's passphrase KDF, which `hexkey` doesn't (about 40 ms per open on
  a desktop, worse on a phone). Connection pooling stays on.
- The key is never logged and never put in a connection string. It lives in memory in `DatabaseKey` for the
  life of the process.
- **Storage:** `ISecretStore`, injected like `IPreferences`.

  | Platform | Store |
  |---|---|
  | iOS, Android, Mac Catalyst | MAUI `SecureStorage` (Keychain; Android Keystore-backed preferences) |
  | Windows | `DpapiSecretStore`: DPAPI, current-user scope, under `AppData/SpendSense/secrets`. The app is unpackaged (`WindowsPackageType=None`), where `SecureStorage` needs package identity |
  | Mac Catalyst, Debug, unsigned | Keychain needs the `keychain-access-groups` entitlement, which needs a provisioning profile. Debug builds fall back to a file in `AppData/SpendSense/dev-secrets` so `dotnet run -f net11.0-maccatalyst` keeps working. Release builds never do |

- On iOS the Keychain item keeps MAUI's default accessibility (after first unlock). It isn't `ThisDeviceOnly`, so
  an encrypted iPhone backup restored to a new iPhone brings the key with the database and the data stays
  readable.

### 2. Opening the database is a step the UI waits for

Startup no longer migrates in `MauiProgram`. `DatabaseGate` (wrapping the router) calls
`DatabaseOpener.Open()` before any page renders:

1. Read the device key. With no key and no database, create a key: this is a new install.
2. With no key and a **plaintext** database, this is an upgrade from before this ADR: create and store a key,
   then encrypt the file (decision 3).
3. With no key and an **encrypted** database, the data can't be read: show the recovery screen (decision 4).
4. Check the key with an unpooled probe (`SELECT count(*) FROM sqlite_master`). A wrong key raises
   `SQLITE_NOTADB` (26) here, not at the key pragma. A wrong key also goes to the recovery screen.
5. Migrate, then generate due recurring transactions and check notifications, as `MauiProgram` used to.

### 3. Existing data is encrypted in place on the first launch after the update

The key is stored **before** the file is touched, so a crash part-way leaves a stored key and a plaintext file,
which the next launch converts again. The other order could lose the key to an encrypted file.

1. Clear the connection pool, so no handle holds the file.
2. `PRAGMA journal_mode = DELETE`: sqlite3mc refuses to rekey a plaintext database in WAL mode.
3. `PRAGMA rekey = 'raw:<hex>'`, then `PRAGMA journal_mode = WAL` again.

The conversion runs in one rollback-journal transaction, so it is as crash-safe as any SQLite write. The safety
copies ADR 0003 keeps under `AppData/SpendSense/Backups` are encrypted the same way, and the export cache is
emptied. Plaintext blocks that the flash storage already wrote elsewhere can't be scrubbed by an app.

### 4. A missing key has a recovery screen, not a crash

The key goes missing when secure storage is cleared, the app is reinstalled with its data left behind, or an
OS restore brings back the file without the key. `Recover` explains this and offers:

- **Restore from a backup.** Pick a file, unlock it with its password or recovery code, confirm, then the locked
  database is set aside (as below) and the backup is restored into a new one.
- **Start fresh.** The locked database is moved, never deleted, into `AppData/SpendSense/Locked/<timestamp>/`
  in case the key comes back. The old key isn't deleted either: it's kept in `ISecretStore` under
  `spendsense.db.device-key.locked-<timestamp>`, since a damaged file reports a missing key too. A new key and
  an empty database are created.

### 5. A backup is a zip protected by a password and a recovery code

`SpendSense-backup-YYYY-MM-DD-HHmmss.zip`:

```
manifest.json    plaintext, no personal data
SpendSense.db    a copy of the database, encrypted with this backup's own key
```

- **Backup key:** each backup gets a fresh random 256-bit key. The live database is copied into the backup with
  SQLite's backup API onto a connection keyed with it (the spike showed the API re-encrypts pages for a
  differently keyed target).
- **Key slots:** `manifest.json` holds the backup key twice, each copy encrypted (wrapped) with AES-256-GCM:
  - **password:** key from PBKDF2-SHA256, 600,000 iterations (OWASP 2023), 16-byte salt. The password is
    NFC-normalised first, so it types the same on every platform.
  - **recovery code:** 128 random bits shown as 26 Base32 characters in groups (`7KQ2-M9XD-…`). It already has
    full entropy, so HKDF-SHA256 is enough.
  - The associated data is the manifest's format, version, creation time and cipher plus the slot type, so
    editing the manifest makes unwrapping fail.

  ```json
  {
    "format": "spendsense-backup",
    "formatVersion": 1,
    "createdUtc": "2026-10-18T09:30:00.0000000Z",
    "cipher": "sqlite3mc-chacha20",
    "keySlots": [
      { "type": "password", "kdf": "PBKDF2-SHA256", "iterations": 600000, "salt": "…", "nonce": "…", "wrappedKey": "…" },
      { "type": "recoveryCode", "kdf": "HKDF-SHA256", "salt": "…", "nonce": "…", "wrappedKey": "…" }
    ]
  }
  ```

- **Setting the password:** the first "Back up now" asks for a backup password (at least 10 characters, typed
  twice), then shows the recovery code once, with Copy. Preferences → Data → Backup password changes it, which
  also makes a new recovery code. Older backup files keep the password and code they were made with.
- **Remembered on this device:** the password-derived key, the recovery-derived key and their salts (never
  the password or the code) are kept in `ISecretStore`, so later backups don't ask again. This adds no new
  exposure: the same store already holds the device key, which opens all the data.
- A forgotten password and a lost recovery code mean that backup can't be opened. The set-up screen says so.

### 6. Restore accepts both formats and asks only when it must

1. **Plaintext `.db`** (a backup made before this ADR): checked as in ADR 0003, then copied into a keyed
   staging file with `VACUUM INTO 'file:…?key=raw:…'` and from there into the live database. The backup API
   won't copy between a plaintext and an encrypted database directly.
2. **Encrypted `.zip`:** the manifest is checked (wrong format; newer format version). The keys remembered on
   this device are tried first, so a backup made here with the current password opens without asking.
   Otherwise the user enters the password or the recovery code; a wrong one says so, with unlimited retries
   (the KDF cost is the defence). The database is extracted to the cache directory, still under the backup key,
   summarised read-only, and after confirmation copied into the live database, which re-encrypts it with the
   device key.

Everything else in ADR 0003 decision 2 stays: the summary, the newer-version check, the safety copy (now
encrypted with the device key) and migrating an older backup.

### 7. CSV export stays plaintext

It's for spreadsheets. Its row in Preferences says "Not encrypted".

## Consequences

- **Tests:** backup and opener tests run against real encrypted temp files. The rest of the suite keeps using
  unencrypted in-memory SQLite.
- **Platform setup:** Android's `allowBackup` is off, so Auto Backup can't restore the encrypted database to a
  new phone without its Keystore key. On Android, then, a SpendSense backup is the only way to move to a new
  phone (unlike iOS, decision 1). Signed Mac Catalyst builds use `Entitlements.Keychain.plist` when
  `CodesignProvision` is set.
- **Not verified here:** iOS and Mac Catalyst can't be built on the machine this was written on (no Xcode),
  and the Windows store compiles only on Windows. Each needs a run on the device before release.
- **Later:** an optional app lock (biometrics or PIN on open, independent of the device key), automatic
  backups to a folder picked once, and key rotation (`PRAGMA rekey` makes it possible).
