BEGIN TRANSACTION ISOLATION LEVEL SERIALIZABLE;
SET LOCAL lock_timeout = '10s';
CREATE TEMP TABLE funding_source ON COMMIT DROP AS
SELECT * FROM jsonb_to_recordset(convert_from(decode('__MANIFEST_BASE64__','base64'),'UTF8')::jsonb)
    AS s(sha text, filename text, sheet text, cell text, row integer, stream text, basis text,
         category text, amount numeric, currency text, date date, narrative text, source jsonb);
ALTER TABLE funding_source ADD PRIMARY KEY (sha,sheet,cell,stream);

CREATE TEMP VIEW funding AS
SELECT s.*, b."Id" batch_id, b."ProjectId" project_id
FROM funding_source s JOIN historical_import_batches b ON lower(b."SourceSha256") = s.sha
    AND b."SourceFileName" = s.filename AND b."Status" = 'POSTED' AND b."MappingVersion" = 'universal-v1.1';
DO $$
BEGIN
    IF current_database() <> 'emg_restore' OR EXISTS (
        SELECT sha FROM funding_source GROUP BY sha EXCEPT SELECT sha FROM funding GROUP BY sha
    ) OR (SELECT count(*) FROM funding) <> (SELECT count(*) FROM funding_source) THEN
        RAISE EXCEPTION 'Historical batch identity mismatch';
    END IF;
    PERFORM 1 FROM projects WHERE "Id" IN (SELECT project_id FROM funding) ORDER BY "Id" FOR UPDATE;
END $$;

INSERT INTO historical_funding_lines
    ("BatchId","Sheet","Cell","SourceRow","Stream","Basis","Category","Amount","Currency","SourceDate","Narrative","SourceJson")
SELECT batch_id,sheet,cell,row,stream,basis,category,amount,currency,date AT TIME ZONE 'UTC',narrative,source::text
FROM funding
ON CONFLICT ("BatchId","Sheet","Cell","Stream") DO NOTHING;

-- Preserve the actual pre-repair receipt. A new row has a null before-image.
CREATE TABLE IF NOT EXISTS historical_receipt_recoveries (
    line_id bigint PRIMARY KEY REFERENCES historical_funding_lines("Id"),
    advance_id integer NOT NULL UNIQUE, previous_row jsonb, corrected_at timestamptz NOT NULL DEFAULT now()
);
CREATE TEMP VIEW receipt_sources AS
SELECT f.*, l."Id" line_id,
       CASE WHEN f.narrative LIKE '%من المالك%' THEN 'المالك'
            WHEN f.narrative LIKE '%من العميل%' THEN 'العميل' ELSE '' END payer,
       CASE WHEN f.narrative LIKE '%شيك%' AND f.narrative NOT LIKE '%نقد%' THEN 'شيك'
            WHEN f.narrative LIKE '%نقد%' AND f.narrative NOT LIKE '%شيك%' THEN 'نقدي'
            WHEN f.narrative LIKE '%حوال%' OR f.narrative LIKE '%تحويل%' THEN 'تحويل'
            ELSE 'غير محدد' END method
FROM funding f JOIN historical_funding_lines l ON (l."BatchId",l."Sheet",l."Cell",l."Stream")
    = (f.batch_id,f.sheet,f.cell,f.stream)
WHERE f.stream IN ('main_schedule','unallocated_schedule')
    AND NOT EXISTS (SELECT FROM historical_receipt_recoveries h WHERE h.line_id=l."Id");

CREATE TEMP TABLE prior_receipts ON COMMIT DROP AS
SELECT s.line_id,a."Id" advance_id,to_jsonb(a) previous_row
FROM receipt_sources s JOIN advance_payments a ON a."ProjectId"=s.project_id
    AND a."Reference" ~ '^histadv:[0-9a-f]{24}$'
    AND starts_with(a."Notes", 'استلام تاريخي من ' || s.filename || '، ' || s.sheet || '!' || s.cell || '.')
    AND a."Amount"=s.amount AND a."Currency"=s.currency AND a."Status"='مرحّل';
ALTER TABLE prior_receipts ADD PRIMARY KEY (line_id);
ALTER TABLE prior_receipts ADD UNIQUE (advance_id);
DO $$
BEGIN
    IF EXISTS (SELECT FROM advance_payments a WHERE a."Reference" ~ '^histadv:[0-9a-f]{24}$'
        AND a."ProjectId" IN (SELECT project_id FROM funding)
        AND NOT EXISTS (SELECT FROM prior_receipts p WHERE p.advance_id=a."Id")
        AND NOT EXISTS (SELECT FROM historical_receipt_recoveries h WHERE h.advance_id=a."Id")) THEN
        RAISE EXCEPTION 'An existing backfill receipt cannot be matched exactly';
    END IF;
    IF EXISTS (SELECT FROM prior_receipts p WHERE
        EXISTS (SELECT FROM advance_allocations a WHERE a."AdvancePaymentId"=p.advance_id)
        OR EXISTS (SELECT FROM security_audit_logs a WHERE a."EntityType"='advance-payment' AND a."EntityId"=p.advance_id)
        OR EXISTS (SELECT FROM attachments a WHERE a."EntityType"='advance-payment' AND a."EntityId"=p.advance_id)
        OR EXISTS (SELECT FROM workflow_actions a WHERE a."EntityType"='advance-payment' AND a."EntityId"=p.advance_id)) THEN
        RAISE EXCEPTION 'A historical receipt has subsequent operational dependencies';
    END IF;
    IF EXISTS (SELECT FROM receipt_sources s WHERE s.basis='main_schedule_entry'
        AND NOT EXISTS (SELECT FROM prior_receipts p WHERE p.line_id=s.line_id)
        AND (EXISTS (SELECT FROM advance_payments a WHERE a."ProjectId"=s.project_id AND a."Amount"=s.amount
            AND a."Currency"=s.currency AND coalesce(a."Reference",'') !~ '^histadv:[0-9a-f]{24}$'
            AND NOT EXISTS (SELECT FROM historical_receipt_recoveries h WHERE h.advance_id=a."Id"))
        OR EXISTS (SELECT FROM site_movements a WHERE a."ProjectId"=s.project_id AND a."MovementType"='وارد' AND a."TotalAmount"=s.amount)
        OR EXISTS (SELECT FROM supplier_payments a WHERE a."ProjectId"=s.project_id AND a."PaymentSource"='مالك مباشر' AND a."Amount"=s.amount)
        OR EXISTS (SELECT FROM contractor_payments a WHERE a."ProjectId"=s.project_id AND a."PaymentSource"='مالك مباشر' AND a."Amount"=s.amount))) THEN
        RAISE EXCEPTION 'A new receipt has a possible operational counterpart';
    END IF;
END $$;
UPDATE advance_payments a SET "PaymentDate"=s.date AT TIME ZONE 'UTC',"Notes"=s.narrative,
    "PayerName"=s.payer,"PaymentMethod"=s.method,
    "Status"=CASE WHEN s.basis='main_schedule_entry' THEN 'مرحّل' ELSE 'مرجع تاريخي' END
FROM receipt_sources s JOIN prior_receipts p USING(line_id)
WHERE a."Id"=p.advance_id;
INSERT INTO historical_receipt_recoveries SELECT line_id,advance_id,previous_row,now() FROM prior_receipts;

WITH inserted AS (
    INSERT INTO advance_payments
        ("ProjectId","ClientName","PayerName","Amount","Currency","PaymentDate","PaymentMethod","Reference","Status","Notes","CreatedAtUtc")
    SELECT s.project_id,coalesce(p."ClientName",''),s.payer,s.amount,s.currency,s.date AT TIME ZONE 'UTC',s.method,
           'historical-funding:' || s.line_id,'مرحّل',s.narrative,now()
    FROM receipt_sources s JOIN projects p ON p."Id"=s.project_id
    WHERE s.basis='main_schedule_entry'
    RETURNING "Id","Reference"
)
INSERT INTO historical_receipt_recoveries (line_id,advance_id)
SELECT split_part("Reference",':',2)::bigint,"Id" FROM inserted;

SELECT "Basis","Currency",count(*) AS components,sum("Amount") AS source_amount
FROM historical_funding_lines WHERE "BatchId" IN (SELECT batch_id FROM funding)
GROUP BY "Basis","Currency" ORDER BY 1,2;
