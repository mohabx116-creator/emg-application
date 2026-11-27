BEGIN TRANSACTION ISOLATION LEVEL SERIALIZABLE;
SET LOCAL lock_timeout = '10s';
CREATE TEMP TABLE controls ON COMMIT DROP AS
SELECT s.*, b."Id" batch_id FROM jsonb_to_recordset(convert_from(decode('__MANIFEST_BASE64__','base64'),'UTF8')::jsonb)
  AS s(sha text,filename text,sheet text,cell text,row int,amount numeric,basis text,source jsonb,stream text)
LEFT JOIN historical_import_batches b ON lower(b."SourceSha256") = s.sha AND b."SourceFileName" = s.filename
  AND b."Status" = 'POSTED' AND b."MappingVersion" = 'universal-v1.1';
ALTER TABLE controls ADD PRIMARY KEY (batch_id,stream,cell);
DO $$
BEGIN
    IF current_database() <> 'emg_restore' OR EXISTS (
        SELECT FROM controls c JOIN LATERAL jsonb_each_text(c.source->'members') m ON true
        WHERE NOT EXISTS (SELECT FROM historical_funding_lines f WHERE f."BatchId"=c.batch_id AND f."Sheet"=c.sheet
          AND f."Cell"=m.key AND f."Amount"=m.value::numeric AND f."Currency"='EGP' AND f."Basis"='project_allocation')
    ) OR EXISTS (
        SELECT FROM controls c JOIN historical_funding_lines f ON f."BatchId"=c.batch_id
        WHERE f."Stream"='main_schedule' AND f."Basis" <> 'excluded_by_source_control'
    ) THEN RAISE EXCEPTION 'Funding control lacks matching source components'; END IF;
END $$;
INSERT INTO historical_funding_lines
 ("BatchId","Sheet","Cell","SourceRow","Stream","Basis","Category","Amount","Currency","SourceDate","Narrative","SourceJson")
SELECT batch_id,sheet,cell,row,stream,basis,'',amount,'EGP',NULL,'',source::text FROM controls
ON CONFLICT ("BatchId","Sheet","Cell","Stream") DO NOTHING;
DO $$
BEGIN
    IF EXISTS (SELECT FROM controls c WHERE NOT EXISTS (SELECT FROM historical_funding_lines f
        WHERE f."BatchId"=c.batch_id AND f."Sheet"=c.sheet AND f."Cell"=c.cell AND f."Stream"=c.stream
        AND f."Basis"=c.basis AND f."Amount"=c.amount AND f."SourceJson"::jsonb=c.source))
    THEN RAISE EXCEPTION 'Stored funding proof differs from source'; END IF;
END $$;
SELECT basis,count(*) FROM controls GROUP BY basis ORDER BY basis;
