# Deployment and operations

This file grows as deployment is worked out. It starts with what G1b (Documents) made necessary:
uploaded files live outside the database, so a backup is no longer just the database.

## Backups: the database AND the documents folder, together

From G1b on, the system keeps data in **two places**:

| What | Where | Holds |
| --- | --- | --- |
| The database | SQL Server (`SchoolManagementDb`) | every record, including each document's metadata, status and `StorageKey` |
| The documents folder | `Documents:StorageRoot` (default `App_Data/documents` next to the API) | the uploaded files themselves, named by `StorageKey` |

**A backup must contain both, taken at the same time.** One without the other cannot be restored to a
consistent state:

- database restored, folder not: the documents list shows files that are not there (download answers
  "the file of this document is missing from storage");
- folder restored, database not: files that no row points to (taking space, and holding personal data
  nobody can see or delete from the application).

Recommended routine (until a hosting decision is made):

1. Back up the database (full backup, e.g. the same `BACKUP DATABASE` as
   `database/scripts/g1a-backup-before-migration.sql`, to a dated file).
2. Straight after, copy the whole documents folder (e.g. `robocopy <StorageRoot> <backup>\documents /MIR`)
   to the same dated backup location.
3. Keep both under the same date; restore them together.
4. Test a restore now and then: restore both to a test machine and open a few documents.

The files are already written once and never changed (a corrected document is a new upload with a new
file), so an incremental copy of the folder is safe and fast.

Deleted documents keep their file for `Documents:DeletedFileRetentionDays` (default 90). After that the
daily purge job removes the file (the row stays, marked `FilePurgedAt`). Backups older than that still
contain the file: keep backup retention in line with the school's data-retention policy.

## Documents settings (`appsettings.json`, section `Documents`)

| Setting | Default | Meaning |
| --- | --- | --- |
| `StorageRoot` | empty = `App_Data/documents` next to the API | Folder for uploaded files. Must be **outside** any web root, on a disk that is backed up. A relative path is relative to the API's folder. |
| `MaxFileSizeBytes` | `5242880` (5 MB) | Largest file accepted. The error message shows the file's exact size and this limit. Allowed range 1 … 104 857 600 (100 MB). |
| `DeletedFileRetentionDays` | `90` | Days a deleted document's file is kept before the purge job removes it. |
| `ExpiringSoonDays` | `60` | A passport / visa / Emirates ID expiring within this many days is flagged "Expiring soon". |
| `PurgeEnabled` | `true` | The daily purge job (the tests turn it off). |

Each can also be set by environment variable, e.g. `Documents__MaxFileSizeBytes=10485760`. A change needs
an API restart, no release. A wrong value (e.g. a negative size) stops the API at startup with the reason.

The account the API runs under needs **read and write** on `StorageRoot` and nothing else there. Nobody
else needs access to the folder: every download goes through the API, which checks the permissions
(identity and medical files also need `Students.ViewSensitive`) and records the download in the audit log.

If the API runs behind IIS or a reverse proxy, its request size limit must be at least
`MaxFileSizeBytes` + 1 MB, or large uploads are refused by the proxy before the API can explain why.
