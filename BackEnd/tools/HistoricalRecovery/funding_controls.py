"""Verify primary receipt and allocation-based funding controls without posting receipts."""

import csv
import json
import re
from collections import defaultdict
from decimal import Decimal

from openpyxl import load_workbook
from openpyxl.utils.cell import get_column_letter, range_boundaries

from funding import category_for, control_cells, sheet_cells
from office_rates import run_recovery, source_rates


def amount(value):
    return Decimal(str(value or 0))


def funding_controls(package, mapping):
    source_rates(package, mapping)
    with (package / "Index" / "workbooks.csv").open(encoding="utf-8-sig") as source:
        manifest = {r["sha256"]: r["archive_path"] for r in csv.DictReader(source)}
    with mapping.with_name("02_payment_candidates.csv").open(encoding="utf-8-sig") as source:
        signatures = defaultdict(list)
        for record in csv.DictReader(source):
            signatures[record["id"]].append(tuple(record[key] for key in
                ("amount_cell","stream","currency","amount","date_raw","description_raw","formula")))
    owners = defaultdict(list)
    result = []
    for project in json.loads(mapping.read_text(encoding="utf-8-sig")):
        path = package / manifest[project["sha256"]]
        sheet = project["payment_sheet"]
        wb = load_workbook(path, read_only=True, data_only=True)
        fb = load_workbook(path, read_only=True, data_only=False)
        try:
            values = sheet_cells(wb[sheet])
            formulas = sheet_cells(fb[sheet])
            def owner(label):
                return re.sub(r"[\W_]+", "", str(label).split(":")[-1].strip()).replace("أ","ا").replace("إ","ا")
            owner_label = values.get("A2","")
            site = next((s for s in wb if "باعمال الموقع" in s.title),None)
            site_owner = next((str(v) for cells in site.iter_rows(max_row=8,values_only=True) for v in cells
                               if isinstance(v,str) and "اسم العميل" in v and ":" in v),"") if site else ""
            if signatures[project["id"]] and owner(owner_label):
                key = (owner(owner_label),tuple(sorted(signatures[project["id"]])))
                owners[key].append((project["sha256"],owner(site_owner)==owner(owner_label),site_owner,owner_label))
            final = next(s for s in fb if "نهائ" in s.title)
            office_cell = project["office_paid_cell"]
            office_links = [c.coordinate for cells in final.iter_rows(max_row=18) for c in cells
                            if office_cell and c.value == f"='{sheet}'!{office_cell}"]
            final_values = sheet_cells(wb[final.title])
        finally:
            wb.close()
            fb.close()
        main = project["main_control_cell"]
        members = {}
        if main and project["main_candidate_count"]:
            # Existing recognition rules handle individual receipt and settlement components.
            continue
        if main:
            refs = control_cells(formulas.get(main))
            if not refs or values.get(main) != 0 or any(amount(values.get(c)) for c in refs):
                raise ValueError(f"Unverified empty main schedule in {project['id']}")
            if not project["allocation_count"]:
                result.append(dict(sha=project["sha256"], filename=path.name, sheet=sheet, cell=main,
                                   row=int(re.search(r"[0-9]+$", main)[0]), amount="0", basis="verified_zero",
                                   source=dict(formula=formulas[main], members={}, main=main)))
                continue
        row = project["allocation_control_row"]
        controls = [c for c,v in values.items() if v == "اجمالي الدفعات" and c.endswith(str(row-1))]
        if len(controls) != 1:
            continue
        col = re.match(r"[A-Z]+", controls[0])[0]
        cell = col + str(row)
        formula = str(formulas.get(cell, "")).upper().replace("$","")
        subtotal = re.fullmatch(r"=SUM\(([A-Z]+[0-9]+:[A-Z]+[0-9]+)\)", formula)
        if not subtotal:
            continue
        first, top, last, bottom = range_boundaries(subtotal[1])
        if top != row or bottom != row:
            continue
        columns = [get_column_letter(i) for i in range(first,last+1)]
        for column in columns:
            address = column + str(row)
            refs = control_cells(formulas.get(address))
            if not refs or any(not re.fullmatch(column+r"[0-9]+", ref) for ref in refs):
                raise ValueError(f"Unsupported allocation subtotal at {sheet}!{address}")
            details = {ref: str(amount(values[ref])) for ref in refs if amount(values.get(ref))}
            if sum(map(Decimal,details.values()),Decimal(0)) != amount(values.get(address)):
                raise ValueError(f"Allocation control differs at {sheet}!{address}")
            members.update(details)
        total = sum(map(Decimal,members.values()),Decimal(0))
        if cell not in values or total != amount(values[cell]):
            raise ValueError(f"Funding total differs at {sheet}!{cell}")
        # The source outstanding-work calculation must actually deduct this control or its exact range.
        proof = None
        for addr, expr in formulas.items():
            if not isinstance(expr,str):
                continue
            match = re.fullmatch(r"=([A-Z]+[0-9]+)-(.*)",expr.upper().replace("$","").replace(" ",""))
            if match and match[2] in {cell, "SUM("+subtotal[1]+")", "(SUM("+subtotal[1]+"))"}:
                if addr in values and match[1] in values and amount(values[addr]) == amount(values[match[1]]) - total:
                    proof = addr
                    break
        if not proof:
            continue
        result.append(dict(sha=project["sha256"], filename=path.name, sheet=sheet, cell=cell, row=row,
                           amount=str(total), basis="verified_allocation_total" if members else "verified_zero",
                           source=dict(formula=formula, members=members, net_cell=proof,
                                       net_formula=formulas[proof], net_value=str(values[proof]), main=main)))
        # Some source templates explicitly keep office payments outside the work-only control.
        # Require a separate final-account payment cross-reference and reconcile its signed detail.
        office = project["office_paid_cell"]
        if office and office not in {column+str(row) for column in columns} and values.get(office) and office_links:
            office_column = re.match(r"[A-Z]+",office)[0]
            refs = control_cells(formulas.get(office))
            if not refs or category_for(values[office_column+"3"]) != "المكتب":
                raise ValueError("Unsupported office funding reference")
            detail = {ref:str(amount(values[ref])) for ref in refs if amount(values.get(ref))}
            office_amount = sum(map(Decimal,detail.values()),Decimal(0))
            if office_amount != amount(values[office]) or any(amount(final_values[ref]) != office_amount for ref in office_links):
                raise ValueError("Office funding controls differ")
            result.append(dict(sha=project["sha256"],filename=path.name,sheet=sheet,cell=office,row=row,
                               amount=str(office_amount),basis="verified_office_supplement",
                               source=dict(formula=formulas[office],members=detail,final_cells=office_links,
                                           work_only_control=cell)))
    for peers in owners.values():
        if len(peers) < 2:
            continue
        confirmed = [sha for sha,matched,_,_ in peers if matched]
        for sha,matched,site_owner,payment_owner in peers:
            if matched and len(confirmed)==1:
                continue
            record = next((r for r in result if r["sha"]==sha and r["basis"]=="verified_allocation_total"),None)
            if record is None:
                raise ValueError("Duplicate payment schedule requires review before recognizing primary receipts")
            result.append(dict(record,basis="conflicting_ownership",source=dict(
                members=record["source"]["members"], peers=sorted(v[0] for v in peers if v[0]!=sha),
                site_owner=site_owner,payment_owner=payment_owner)))
    for record in result:
        record["stream"] = "funding_qualification" if record["basis"] in (
            "conflicting_ownership","verified_office_supplement") else "source_control"
    return result


if __name__ == "__main__":
    run_recovery(funding_controls, "funding_controls.sql", __doc__)
