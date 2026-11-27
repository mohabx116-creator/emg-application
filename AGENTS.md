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
- Permanent UI Requirement: Preserve the original Berry floating `Customization` gear component (`FrontEnd/src/layout/Customization`) wherever it is mounted (`MainLayout`, `MinimalLayout`, `Landing`), keeping all original functionality intact (light/dark modes, theme colors, layout customization, font customization, and existing options) across desktop, tablet, and mobile. Never remove, disable, or replace it during UI optimization or template cleanup.
- Permanent Visual Governance (Owner-Approved EMG Visual Identity):
  - Authoritative approved visual references:
    - Financial summary cards (`FrontEnd/src/Modules/EMG/FinancialSummary.tsx`, interim technical reference `50719fa`; final visual approval remains subject to owner live inspection): Blue gradient for Works (`إجمالي الأعمال`), Purple gradient for Payments (`إجمالي الدفعات`), Green for positive Cash Balance, Red for negative Cash Balance, and Neutral Slate for zero/unknown Cash Balance, supporting both light and dark modes with Berry theme tokens.
    - Advances & historical funding cards (`FrontEnd/src/Modules/EMG/Advances/index.tsx`, owner-approved 2026-11-27): `FinancialSummary` at top with allocation subtitle and currency separation, unified recognized receipts (`قائمة الدفعات المقبوضة`) and separate project allocations (`التوزيعات على بنود المشروع وسجل المصدر`) rendered with semantic top-border accent cards, icon badges, highlighted amount boxes, English/Western `tabular-nums` digits, and Excel source reference footers.
    - Transaction registration modal (`FrontEnd/src/Modules/EMG/SiteMovements/NewTransactionModalForm.tsx`): Owner-approved responsive desktop/tablet/mobile layout with counterparty cards, itemized/direct entry modes, payment summary, and attachment/camera upload.
    - Application identity & PWA assets (`FrontEnd/public/favicon-32x32.png`, `apple-touch-icon.png`, `icon-192.png`, `icon-512.png`, `icon-maskable-512.png`, `emg-logo.png`, `emg-logo-3d.png`, `manifest.webmanifest`, `sw.js`): Must serve valid PNG binaries and keep `/api/*` and `/auth/*` strictly excluded from service-worker caching.
    - Responsive header & navigation (`FrontEnd/src/layout/MainLayout/Header/*`, `MobileBottomNav.tsx`): Must remain unclipped and fully clickable across 360px, 390px, 430px, 768px, and 1440px in Arabic RTL, light/dark modes.
  - Functional, accounting, or backend agents must NOT redesign approved screens, replace approved palettes/typography/spacing/cards, or introduce competing design systems without explicit owner authorization. Integrate functional and accounting data into existing approved visual components.
