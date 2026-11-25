# Sanitized Publication Exclusions

The following are excluded from this candidate:

- Canonical `.git` directory and all Git objects/refs
- Entire `docs/اعمال المواقع/` business-workbook directory
- `docs/اعمال المواقع.rar`
- Entire `docs/recovery/audit/` tree
- Entire `docs/recovery/archive/` tree
- `docs/recovery/.git-history/` bundles
- `FrontEnd/scratch/debug_page.png`
- `FrontEnd/PHASE31_*.md` audit reports and their visual-audit helper scripts; these are internal review artifacts, not runtime source
- 15 generated `BackEnd/tools/ImportPlanAudit/audit-output*.txt` and `BackEnd/tools/WorkbookCatalog/audit-output*.txt` files; the audit tool source remains included
- `FrontEnd/src/assets/images/New Microsoft PowerPoint Presentation.pptx`; empty zero-byte file, no application references
- `FrontEnd/yarn.lock` and `FrontEnd/.yarnrc.yml`; npm/package-lock.json is the approved production authority and no supported build requires Yarn
- All production `.env` files and local environment overrides
- Database dumps, encrypted backup archives and recovery identities
- Private SSH keys, Data Protection keys and runtime uploads
- Docker image export tar files
- `bin`, `obj`, `node_modules`, `dist`, `.vite`, `.yarn` and test/runtime caches
- Local PostgreSQL storage and disposable QA artifacts
- User profile images and historical business attachments

The only Excel/ZIP assets included are:

- `BackEnd/src/EMG.WebApi/Modules/Reports/Templates/ProjectReportTemplate.xlsx`
- `BackEnd/src/EMG.WebApi/Modules/ReportBuilder/Templates/ConsolidatedReportLayout.zip`
