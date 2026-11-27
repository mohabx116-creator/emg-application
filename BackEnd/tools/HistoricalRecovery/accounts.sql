BEGIN TRANSACTION ISOLATION LEVEL SERIALIZABLE;
SET LOCAL lock_timeout='10s';
CREATE TEMP TABLE account_source ON COMMIT DROP AS
SELECT s.*,b."Id" batch_id FROM jsonb_to_recordset(convert_from(decode('__MANIFEST_BASE64__','base64'),'UTF8')::jsonb)
    AS s(sha text,filename text,sheet text,cell text,kind text,category text,amount numeric,currency text,
        party text,key text,role text,invalid boolean,date date,narrative text,source jsonb)
LEFT JOIN historical_import_batches b ON lower(b."SourceSha256")=s.sha AND b."SourceFileName"=s.filename
    AND b."Status"='POSTED' AND b."MappingVersion"='universal-v1.1';
ALTER TABLE account_source ADD PRIMARY KEY(batch_id,sheet,cell,kind);
LOCK TABLE suppliers,contractors IN SHARE ROW EXCLUSIVE MODE;
CREATE FUNCTION pg_temp.name_key(text) RETURNS text LANGUAGE sql IMMUTABLE AS
$$ SELECT replace(translate(trim($1),'أإآىة','ااايه'),' ','') $$;
DO $$ BEGIN
    IF current_database()<>'emg_restore' OR NOT EXISTS (SELECT FROM account_source)
      OR EXISTS (SELECT FROM account_source s LEFT JOIN historical_work_lines w
        ON (w."BatchId",w."Sheet",w."Cell")=(s.batch_id,s.sheet,s.cell)
        WHERE s.kind='work' AND (w."Amount",w."Currency") IS DISTINCT FROM (s.amount,s.currency))
      OR EXISTS (SELECT FROM historical_work_lines w WHERE NOT EXISTS (SELECT FROM account_source s
        WHERE (s.batch_id,s.sheet,s.cell,s.kind)=(w."BatchId",w."Sheet",w."Cell",'work'))) THEN
        RAISE EXCEPTION 'Account evidence differs from work ledger';
    END IF;
END $$;

-- Only deactivate demonstrably redundant, unused legacy imports. Preserve every source identity.
CREATE TEMP TABLE unused_imports ON COMMIT DROP AS
SELECT s."Id" id,pg_temp.name_key(s."Name") key FROM suppliers s
WHERE s."IsActive" AND s."SupplierType"='مورد تاريخي' AND s."Notes" LIKE 'حساب تاريخي مستخرج من %'
    AND coalesce(s."Phone",'')='' AND coalesce(s."TaxNumber",'')='';
DO $$ DECLARE fk record; BEGIN
    FOR fk IN SELECT c.conrelid::regclass rel,a.attname col FROM pg_constraint c
        JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=ANY(c.conkey)
        WHERE c.contype='f' AND c.confrelid='suppliers'::regclass LOOP
        EXECUTE format('DELETE FROM unused_imports u WHERE EXISTS (SELECT FROM %s d WHERE d.%I=u.id)',fk.rel,fk.col);
    END LOOP;
END $$;
UPDATE suppliers s SET "IsActive"=false FROM unused_imports u WHERE s."Id"=u.id AND
    (EXISTS (SELECT FROM account_source a WHERE a.invalid AND a.key=u.key) OR
     EXISTS (SELECT FROM unused_imports other WHERE other.key=u.key AND other.id<u.id
        AND EXISTS (SELECT FROM account_source a WHERE a.key=u.key AND a.role='supplier')));

CREATE TEMP VIEW named_accounts AS
SELECT role,key,min(party) party FROM account_source WHERE role IN ('supplier','contractor') GROUP BY role,key;
INSERT INTO suppliers ("Name","SupplierType","Notes","IsActive","CreatedAtUtc")
SELECT party,'مورد تاريخي','حساب أعمال موثق',true,now() FROM named_accounts n WHERE role='supplier'
    AND NOT EXISTS (SELECT FROM suppliers s WHERE pg_temp.name_key(s."Name")=n.key);
INSERT INTO contractors ("Name","Notes","IsActive","CreatedAtUtc")
SELECT party,'حساب أعمال موثق',true,now() FROM named_accounts n WHERE role='contractor'
    AND NOT EXISTS (SELECT FROM contractors c WHERE pg_temp.name_key(c."Name")=n.key);
CREATE TEMP TABLE resolved_accounts ON COMMIT DROP AS
SELECT s.*,v.id supplier_id,c.id contractor_id FROM account_source s
LEFT JOIN (SELECT pg_temp.name_key("Name") key,min("Id") id FROM suppliers WHERE "IsActive" GROUP BY 1 HAVING count(*)=1) v
    ON s.role='supplier' AND v.key=s.key
LEFT JOIN (SELECT pg_temp.name_key("Name") key,min("Id") id FROM contractors WHERE "IsActive" GROUP BY 1 HAVING count(*)=1) c
    ON s.role='contractor' AND c.key=s.key;
DO $$ BEGIN
    IF EXISTS (SELECT FROM historical_work_lines w JOIN resolved_accounts a
        ON (a.batch_id,a.sheet,a.cell,a.kind)=(w."BatchId",w."Sheet",w."Cell",'work')
        WHERE (w."SupplierId" IS NOT NULL AND w."SupplierId" IS DISTINCT FROM a.supplier_id)
           OR (w."ContractorId" IS NOT NULL AND w."ContractorId" IS DISTINCT FROM a.contractor_id)) THEN
        RAISE EXCEPTION 'Existing work account link changed';
    END IF;
END $$;
UPDATE historical_work_lines w SET "SupplierId"=a.supplier_id,"ContractorId"=a.contractor_id FROM resolved_accounts a
WHERE (w."BatchId",w."Sheet",w."Cell",'work')=(a.batch_id,a.sheet,a.cell,a.kind)
    AND (w."SupplierId",w."ContractorId") IS DISTINCT FROM (a.supplier_id,a.contractor_id);
CREATE TEMP TABLE expected_settlements ON COMMIT DROP AS
SELECT batch_id AS "BatchId",sheet AS "Sheet",cell AS "Cell",category AS "Category",amount AS "Amount",currency AS "Currency",
    date::timestamp AT TIME ZONE 'UTC' AS "SettlementDate",party AS "Party",narrative AS "Narrative",source::text AS "SourceJson",
    supplier_id AS "SupplierId",contractor_id AS "ContractorId" FROM resolved_accounts WHERE kind='settlement';
-- The source remains immutable. Accept a subsequent beneficiary only when the
-- existing settlement and its authentic correction event agree on every key.
UPDATE expected_settlements e
SET "SupplierId" = s."SupplierId", "ContractorId" = s."ContractorId"
FROM historical_settlements s
JOIN historical_import_batches b ON b."Id" = s."BatchId"
WHERE (e."BatchId", e."Sheet", e."Cell") = (s."BatchId", s."Sheet", s."Cell")
  AND e."SupplierId" IS NULL AND e."ContractorId" IS NULL
  AND num_nonnulls(s."SupplierId", s."ContractorId") = 1
  AND EXISTS (
      SELECT FROM historical_import_audit_events a
      WHERE a."BatchId" = s."BatchId" AND a."Action" = 'HISTORICAL_SETTLEMENT_BENEFICIARY_LINKED'
        AND a."DetailsJson"::jsonb #>> '{preview,ProposalSha256}' ~ '^[0-9A-F]{64}$'
        AND a."DetailsJson"::jsonb ->> 'BackupSha256' ~ '^[0-9A-F]{64}$'
        AND a."DetailsJson"::jsonb #> '{preview,Change}' @> jsonb_build_object(
            'BatchId', s."BatchId", 'ProjectId', b."ProjectId", 'SettlementId', s."Id",
            'WorkbookSha256', b."SourceSha256", 'Sheet', s."Sheet", 'Cell', s."Cell",
            'Amount', s."Amount", 'Currency', s."Currency",
            'Role', CASE WHEN s."SupplierId" IS NULL THEN 'contractor' ELSE 'supplier' END,
            'AccountId', coalesce(s."SupplierId", s."ContractorId")));
INSERT INTO historical_settlements ("BatchId","Sheet","Cell","Category","Amount","Currency","SettlementDate","Party","Narrative","SourceJson","SupplierId","ContractorId")
SELECT e.* FROM expected_settlements e WHERE NOT EXISTS (SELECT FROM historical_settlements s
    WHERE (s."BatchId",s."Sheet",s."Cell")=(e."BatchId",e."Sheet",e."Cell"));
DO $$ BEGIN
    IF EXISTS (SELECT FROM expected_settlements e FULL JOIN historical_settlements s USING ("BatchId","Sheet","Cell")
        WHERE to_jsonb(e) IS DISTINCT FROM to_jsonb(s)-'Id') THEN RAISE EXCEPTION 'Settlement evidence differs'; END IF;
END $$;
SELECT kind,count(*) AS components,count(supplier_id) AS supplier_links,count(contractor_id) AS contractor_links
FROM resolved_accounts GROUP BY kind;
