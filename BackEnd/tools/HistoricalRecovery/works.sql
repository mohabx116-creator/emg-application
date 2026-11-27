BEGIN TRANSACTION ISOLATION LEVEL SERIALIZABLE;
SET LOCAL lock_timeout = '10s';
CREATE TEMP TABLE work_source ON COMMIT DROP AS
SELECT * FROM jsonb_to_recordset(convert_from(decode('__MANIFEST_BASE64__','base64'),'UTF8')::jsonb)
    AS s(sha text, filename text, sheet text, cell text, row integer, family text,
         category text, basis text, amount numeric, currency text, date date, description text,
         unit text, quantity numeric, rate numeric, progress numeric, party text, source jsonb);
ALTER TABLE work_source ADD PRIMARY KEY (sha,sheet,cell);

CREATE TEMP TABLE work_batches ON COMMIT DROP AS
SELECT b."Id" batch_id, b."ProjectId" project_id, lower(b."SourceSha256") sha, b."SourceFileName" filename
FROM historical_import_batches b
WHERE b."Status"='POSTED' AND b."MappingVersion"='universal-v1.1'
  AND EXISTS (SELECT FROM work_source s WHERE s.sha=lower(b."SourceSha256") AND s.filename=b."SourceFileName");
ALTER TABLE work_batches ADD PRIMARY KEY (sha);
DO $$ BEGIN
    IF current_database() <> 'emg_restore' OR NOT EXISTS (SELECT FROM work_source)
      OR EXISTS (SELECT sha,filename FROM work_source EXCEPT SELECT sha,filename FROM work_batches) THEN
        RAISE EXCEPTION 'Work batch identity mismatch';
    END IF;
    PERFORM 1 FROM projects WHERE "Id" IN (SELECT project_id FROM work_batches) ORDER BY "Id" FOR UPDATE;
END $$;

CREATE TEMP TABLE prior_work ON COMMIT DROP AS
SELECT b.batch_id,r."SheetName" sheet,r."SourceCellsJson"::jsonb->'Fields'->>'AmountCell' cell,
       p."ProvenanceJson"::jsonb->>'Target' target,(p."ProvenanceJson"::jsonb->>'TargetId')::int target_id,
       p."Amount" amount,p."Currency" currency,b.project_id
FROM historical_posted_entries p JOIN work_batches b ON b.batch_id=p."BatchId"
JOIN historical_import_records r ON r."Id"=p."HistoricalRecordId" AND r."BatchId"=b.batch_id
WHERE p."LedgerStream" IN ('operational:SiteMovement','operational:FinishingRecord',
    'operational:ExcavationRecord','operational:SteelRecord','operational:ConcreteRecord');
ALTER TABLE prior_work ADD PRIMARY KEY (batch_id,sheet,cell);
ALTER TABLE prior_work ADD UNIQUE (target,target_id);

DO $$ BEGIN
    IF EXISTS (SELECT FROM prior_work p JOIN work_batches b USING(batch_id)
        LEFT JOIN work_source s ON (s.sha,s.sheet,s.cell)=(b.sha,p.sheet,p.cell)
        WHERE s.cell IS NULL OR p.amount <> round(s.amount,2) OR p.currency <> s.currency
        OR NOT CASE p.target
            WHEN 'SiteMovement' THEN EXISTS (SELECT FROM site_movements o WHERE (o."Id",o."ProjectId",o."TotalAmount",o."Currency")=(p.target_id,p.project_id,p.amount,p.currency))
            WHEN 'FinishingRecord' THEN EXISTS (SELECT FROM finishing_records o WHERE (o."Id",o."ProjectId",o."TotalAmount")=(p.target_id,p.project_id,p.amount))
            ELSE false END) THEN
        RAISE EXCEPTION 'Existing operational work differs from its source or has an unsupported target';
    END IF;
END $$;

CREATE TEMP TABLE expected_work ON COMMIT DROP AS
SELECT b.batch_id AS "BatchId",s.sheet AS "Sheet",s.cell AS "Cell",s.row AS "SourceRow",
    s.family AS "Family",s.category AS "Category",s.basis AS "Basis",s.amount AS "Amount",s.currency AS "Currency",
    s.date::timestamp AT TIME ZONE 'UTC' AS "WorkDate",s.description AS "Description",s.unit AS "Unit",s.quantity AS "Quantity",
    s.rate AS "Rate",s.progress AS "Progress",s.party AS "Party",s.source::text AS "SourceJson",
    p.target AS "OperationalTable",p.target_id AS "OperationalId"
FROM work_source s JOIN work_batches b USING(sha,filename)
LEFT JOIN prior_work p USING(batch_id,sheet,cell);

INSERT INTO historical_work_lines
    ("BatchId","Sheet","Cell","SourceRow","Family","Category","Basis","Amount","Currency","WorkDate",
     "Description","Unit","Quantity","Rate","Progress","Party","SourceJson","OperationalTable","OperationalId")
SELECT e.* FROM expected_work e
WHERE NOT EXISTS (SELECT FROM historical_work_lines l WHERE (l."BatchId",l."Sheet",l."Cell")=(e."BatchId",e."Sheet",e."Cell"));

DO $$ BEGIN
    IF EXISTS (SELECT FROM (SELECT l.* FROM historical_work_lines l JOIN work_batches b ON b.batch_id=l."BatchId") l
        FULL JOIN expected_work e USING ("BatchId","Sheet","Cell")
        WHERE (to_jsonb(l) - ARRAY['Id','SupplierId','ContractorId']) IS DISTINCT FROM to_jsonb(e)) THEN
        RAISE EXCEPTION 'Stored work lines differ from the verified workbook evidence';
    END IF;
END $$;
SELECT count(*) AS source_lines,count("OperationalId") AS existing_operational_links,
    count(DISTINCT "BatchId") AS represented_batches FROM expected_work;
