# DEP-08.3 — Owner-Controlled Immutable Image Deployment

This runbook is prepared for review only. It is not an authorization to deploy.

## Preconditions

- Run only on the approved Hostinger VPS as the restricted deployment operator.
- Use the approved `docker-compose.prod.yml` and a secret `.env` outside Git.
- `DATABASE_APPLY_MIGRATIONS=false` and `SECURITY_BOOTSTRAP_ADMIN_ENABLED=false` are mandatory.
- `BACKUP_MARKER` and `BACKUP_MANIFEST` must identify a fresh, verified encrypted backup.
- `BACKUP_REQUIRED_FILES` must list the encrypted database dump, uploads archive, Data Protection keys and recovery configuration artifacts from that same set.
- `PUBLIC_ACCOUNT_URL` must identify the unauthenticated account endpoint; deployment acceptance requires HTTP 401 there so a shallow health response cannot mask database authentication failure.
- `AUTH_ACCEPTANCE_URL` and `AUTH_ACCEPTANCE_COOKIE_FILE` must provide an owner-authorized authenticated `/api/account/me` request; deployment and rollback cannot be marked successful from HTTP 401 alone.
- The deployment gate performs a password-authenticated TCP `select 1` against the existing PostgreSQL container using the protected production environment file before application mutation.
- `BACKEND_IMAGE` and `FRONTEND_IMAGE` must be full immutable GHCR digest references from the same source SHA.
- Confirm adequate disk and memory before execution.

## Deployment

```bash
export EXPECTED_SOURCE_SHA='approved-40-character-commit-sha'
export BACKEND_IMAGE='ghcr.io/mohabx116-creator/emg-application/backend@sha256:...'
export FRONTEND_IMAGE='ghcr.io/mohabx116-creator/emg-application/frontend@sha256:...'
export BACKUP_MARKER='/opt/emg/backups/manual/<verified-set>/COMPLETE'
export BACKUP_MANIFEST='/opt/emg/backups/manual/<verified-set>/SHA256SUMS'
export BACKUP_REQUIRED_FILES='/opt/emg/backups/manual/<verified-set>/<database-dump.enc>,/opt/emg/backups/manual/<verified-set>/<uploads.enc>,/opt/emg/backups/manual/<verified-set>/<keys.enc>,/opt/emg/backups/manual/<verified-set>/<config.enc>'
export PUBLIC_HEALTH_URL='https://emg.example.test/'
export PUBLIC_API_HEALTH_URL='https://emg.example.test/api/health'
export PUBLIC_ACCOUNT_URL='https://emg.example.test/api/account/me'
export AUTH_ACCEPTANCE_URL='https://emg.example.test/api/account/me'
export AUTH_ACCEPTANCE_COOKIE_FILE='/run/emg-deploy/auth.cookies'
export COMPOSE_FILE='/opt/emg/deployment/docker-compose.prod.yml'
export ENV_FILE='/opt/emg/deployment/.env'
sudo -u emg-deploy /opt/emg/deployment/deploy-approved-images.sh
```

The script uses an exclusive lock, verifies the backup evidence, validates both image digests and OCI revision labels, pulls both images before changing application containers, and checks Backend/Frontend health. PostgreSQL, uploads, Data Protection keys and Caddy are not restarted.

## Rollback

```bash
sudo -u emg-deploy /opt/emg/deployment/rollback-approved-images.sh
```

Rollback changes only the application image references recorded in the last successful deployment. It never restores or downgrades PostgreSQL and never runs migrations.

## Credential handling

- Use a dedicated least-privilege `emg-deploy` account.
- Permit Docker Compose operations only through a reviewed, root-owned deployment directory or narrowly scoped sudo rule.
- Keep `.env`, registry credentials and backup decryption material outside Git with mode `0600`.
- Do not place tokens in command arguments, logs, image labels or release manifests.

## Recovery and safety

The pre-deployment backup must be independently verified before the script is invoked. Database and upload restoration are separate owner-approved recovery operations; neither is automatic during application rollback.
