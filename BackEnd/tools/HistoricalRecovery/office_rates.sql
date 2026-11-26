BEGIN TRANSACTION ISOLATION LEVEL SERIALIZABLE;
SET LOCAL lock_timeout = '10s';
CREATE TEMP TABLE source_rates (
    sha text PRIMARY KEY, filename text NOT NULL, sheet text NOT NULL,
    address text NOT NULL, rate numeric NOT NULL CHECK (rate BETWEEN 0 AND 100)
) ON COMMIT DROP;
INSERT INTO source_rates
SELECT * FROM jsonb_to_recordset(convert_from(decode('__MANIFEST_BASE64__','base64'),'UTF8')::jsonb)
    AS s(sha text, filename text, sheet text, address text, rate numeric);

CREATE TEMP VIEW matched_rates AS
SELECT s.*, b."Id" batch_id, p."Id" project_id, p."OfficeSharePercentage" existing_rate
FROM source_rates s
JOIN historical_import_batches b ON lower(b."SourceSha256") = s.sha
    AND b."SourceFileName" = s.filename AND b."Status" = 'POSTED'
    AND b."MappingVersion" = 'universal-v1.1'
JOIN projects p ON p."Id" = b."ProjectId";

DO $$
BEGIN
    IF current_database() <> 'emg_restore' OR EXISTS (
        SELECT s.sha FROM source_rates s LEFT JOIN matched_rates m USING (sha)
        GROUP BY s.sha HAVING count(m.project_id) <> 1
    ) OR EXISTS (SELECT project_id FROM matched_rates GROUP BY project_id HAVING count(*) <> 1) THEN
        RAISE EXCEPTION 'Existing historical project identity mismatch';
    END IF;
    PERFORM 1 FROM projects WHERE "Id" IN (SELECT project_id FROM matched_rates) ORDER BY "Id" FOR UPDATE;
    IF EXISTS (SELECT 1 FROM matched_rates WHERE existing_rate <> 0 AND existing_rate <> rate) THEN
        RAISE EXCEPTION 'Existing operational percentage conflicts with the source';
    END IF;
END $$;

-- Keep the source cell and prior value for each one-time change. Later owner
-- edits (including an intentional change back to zero) survive a retry.
CREATE TABLE IF NOT EXISTS historical_office_rate_recoveries (
    project_id integer PRIMARY KEY REFERENCES projects("Id"),
    batch_id integer NOT NULL REFERENCES historical_import_batches("Id"),
    workbook_sha256 text NOT NULL, source_sheet text NOT NULL, source_cell text NOT NULL,
    previous_rate numeric NOT NULL, recovered_rate numeric NOT NULL,
    corrected_at timestamptz NOT NULL DEFAULT now(), database_role text NOT NULL DEFAULT current_user
);
WITH claimed AS (
    INSERT INTO historical_office_rate_recoveries
        (project_id, batch_id, workbook_sha256, source_sheet, source_cell, previous_rate, recovered_rate)
    SELECT project_id, batch_id, sha, sheet, address, existing_rate, rate FROM matched_rates
    WHERE existing_rate IS DISTINCT FROM rate
    ON CONFLICT (project_id) DO NOTHING
    RETURNING project_id, previous_rate, recovered_rate
)
UPDATE projects p SET "OfficeSharePercentage" = c.recovered_rate
FROM claimed c WHERE p."Id" = c.project_id AND p."OfficeSharePercentage" = c.previous_rate;
SELECT count(*) AS matched_projects,
       count(*) FILTER (WHERE existing_rate = rate) AS matching_rates
FROM matched_rates;
