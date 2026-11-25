# EMG DEP-06 Backup and Recovery

The recovery set contains an encrypted PostgreSQL custom-format dump, the uploads archive, ASP.NET Data Protection keys, and deployment/release metadata. A set is complete only when `COMPLETE` exists and `SHA256SUMS` validates.

Backups use an age public recipient. The matching recovery identity is owner-controlled and must not be stored only on the VPS or beside the encrypted archives. The VPS stores only the recipient, never the identity.

The backup service takes a consistent PostgreSQL snapshot and records file inventories before and after archiving. If uploads or Data Protection keys change during capture, the set fails instead of claiming consistency. A deployment that needs stricter cross-store atomicity must schedule a short write-quiescence window approved by the owner.

Scheduled backups are intentionally limited to:

- Weekly: Friday at 03:00 Africa/Cairo; retain the latest 4 successful sets.
- Monthly: first day at 03:00 Africa/Cairo; retain the latest 12 successful sets.

There is no daily, six-hourly, or hourly timer. Retention cleanup starts in dry-run mode and must not remove the last known-good set.

Recovery validation must use disposable PostgreSQL 16 and file volumes. It must verify checksums, decryption, `pg_restore`, schema/migration state, protected identity fingerprints, uploads, Data Protection keys, API health, and that the restored configuration cannot target live production.
