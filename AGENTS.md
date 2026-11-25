# EMG canonical workspace rules

The only authorized development workspace is `E:\mohabx_116\EMG-application`.

- Never create RC copies, external worktrees, or alternative development directories.
- Develop on canonical `main` only. Do not create additional branches or repositories.
- Verify each completed change, review its explicit staged paths for sensitive data and generated artifacts, and create a descriptive local commit.
- Synchronize verified changes to `main` in the applicable existing GitHub repository after verifying its identity, ancestry, and remote state. Use normal fast-forward pushes only and verify the remote result.
- If synchronization would expose sensitive history, overwrite remote work, or bypass access or branch protection, retain the local commit and report `SYNC_BLOCKED`. Never force-push, rewrite shared history, or delete remote branches.
- Original workbooks under `docs\Ø§Ø¹Ù…Ø§Ù„ Ø§Ù„Ù…ÙˆØ§Ù‚Ø¹` are read-only business evidence.
- Investigate original Excel evidence before inventing business or accounting rules.
- Real historical financial posting remains disabled until explicit owner approval.
- Never access or modify permanent PostgreSQL 5432 without authorization.
- Never delete valuable source or Git history as part of workspace cleanup.
- Never leave temporary runtime, QA, generated or disposable files inside the canonical repository. Keep only actual source code, required configuration, tests and approved permanent documentation.
- Maintain one canonical development workspace only; do not create redundant permanent directories, clones, worktrees or repositories.
- Use one authorized backup root, `E:\mohabx_116\EMG-backups`, for verified recovery backups; do not store backups in the repository.
- Remove task-created temporary directories, disposable containers, networks, databases and logs immediately after validation.
- Preserve unique business data, original workbooks and verified recovery backups; never delete them as part of routine cleanup.
