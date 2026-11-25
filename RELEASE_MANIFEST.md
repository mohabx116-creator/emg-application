# EMG Sanitized Monorepo Release Manifest

Status: candidate only; no commit or publication performed.

- Final staged file count: 1,522

## Provenance

- Canonical local source HEAD: `4d17f111c75fc43c8f5cee56109e3370e667e3d5`
- Backend source reference: `9f2008e365c63173ccc759b9801a24eaf38b4da9`
- Frontend source reference: `0580de9710f7729a233da664d614fa3e0dcf9585`
- Documentation source reference: `dfbe0d29e9544a2446281ef379569cad4747d3da`
- Approved Backend image: `emg-dep03b-api:20261124-forwarded`
- Approved Frontend image: `emg-dep04-frontend:20261124-csrf-fix`
- PostgreSQL restore compatibility: PostgreSQL 16.16
- EF migration count: 24

These references document provenance. They do not claim byte-identical image reproduction.

## Included

- Backend and Frontend application source
- Database migrations and tests
- Certified Excel report templates only
- Production Dockerfiles, Compose and host-Caddy configuration
- Backup/recovery scripts and systemd timer units
- Approved non-sensitive technical documentation
- Non-secret configuration examples

## Dependency policy

Production installation is npm-based through `package-lock.json` and `npm ci`. Yarn is not a supported build path in this candidate; `yarn.lock` and `.yarnrc.yml` are excluded, and the package-manager metadata is npm-only. No dependency versions were changed.

Quill remains excluded from production dependencies and production bundles.

## Acceptance evidence

- Public Go-Live: accepted
- Disaster recovery: accepted using PostgreSQL 16.16
- Universal Import preview and Crosswalk: accepted
- Backend safe validation: 140 passed, 10 skipped. Nine skips require PostgreSQL fixtures; one is the private original-template parity check. None was converted to a pass.
- Frontend validation: `npm ci`, TypeScript, production build and same-origin API contract passed.
- DEP-07.2 package-policy recheck: `package-lock.json` unchanged (SHA-256 `BA598E2F9AE4689654085FF26A03B91D0F2A59723B9666E4928B04987D0B534D`); a fresh registry reinstall was stopped after an environment-side npm metadata/extraction hang, not a dependency or source error. The previously accepted clean `npm ci` and production build remain the authoritative build evidence.
- Dependency audit: full install reported 10 non-production findings (1 low, 4 moderate, 5 high); `npm audit --omit=dev` reported 0 vulnerabilities.
- Current candidate has not been committed or published

## Remaining limitations

- No repository has been created for the proposed monorepo.
- No Git remote is configured locally.
- Historical root Git history is intentionally excluded.
- Original business workbooks and audit evidence are intentionally excluded.
- The candidate contains no original business workbooks, generated audit-output reports, or the unused empty PowerPoint asset.
