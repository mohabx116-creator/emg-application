"""Recover the source funding schedules, date evidence and project allocations."""

from collections import defaultdict
import csv
from datetime import date, datetime
from decimal import Decimal
import json
import re

from openpyxl import load_workbook
from openpyxl.utils.cell import coordinate_from_string

from office_rates import run_recovery, source_rates


def control_cells(formula):
    """Recognize only the explicit additive formulas used as payment boundaries."""
    if not isinstance(formula, str) or not formula.startswith("="):
        return None
    formula = formula[1:].upper().replace("$", "").replace(" ", "")
    result = set()
    for term in formula.split("+"):
        if re.fullmatch(r"[A-Z]+[1-9][0-9]*", term):
            result.add(term)
            continue
        match = re.fullmatch(r"SUM\(([A-Z]+)(\d+):([A-Z]+)(\d+)\)", term)
        if not match or match[1] != match[3]:
            return None
        result.update(f"{match[1]}{i}" for i in range(int(match[2]), int(match[4]) + 1))
    return result


def category_for(header):
    label = header.strip().replace("ى", "ي")
    categories = {"الموقع": "الموقع", "العرب": "العرب", "المقاول": "المقاول",
                  "المحطه": "المحطة", "المحطة": "المحطة", "الحديد": "الحديد",
                  "المباني": "المباني", "المحاره": "المحارة", "المحارة": "المحارة",
                  "العزل": "العزل", "المكتب": "المكتب", "الكهرباء": "الكهرباء",
                  "السباكه": "السباكة"}
    for ending, category in categories.items():
        if label.endswith(ending):
            return category
    return header.strip()


def row_date(cells, narrative):
    typed = {value.date() if isinstance(value, datetime) else value for value in cells
             if isinstance(value, date)}
    if len(typed) == 1:
        return next(iter(typed)).isoformat()
    if typed:
        return None
    if "شيك" in narrative or "مستحق" in narrative or "استحقاق" in narrative:
        received = re.search(r"(?:معاد استلام|تاريخ استلام|بتاريخ)\s*[:：]?\s*(\d{1,2}[/\-]\d{1,2}[/\-]\d{4})", narrative)
        if not received:
            return None
        narrative = received[1]
    dates = set()
    for d, m, y in re.findall(r"(?<!\d)(\d{1,2})[/\-](\d{1,2})[/\-](\d{4})(?!\d)", narrative):
        try:
            dates.add(date(int(y), int(m), int(d)))
        except ValueError:
            return None
    return next(iter(dates)).isoformat() if len(dates) == 1 else None


def sheet_cells(sheet):
    return {cell.coordinate: cell.value for row in sheet.rows for cell in row if cell.value is not None}


def funding_lines(package, mapping):
    source_rates(package, mapping)  # Verify every workbook identity and SHA before using the map.
    with (package / "Index" / "workbooks.csv").open(encoding="utf-8-sig", newline="") as source:
        manifest = {r["sha256"]: r["archive_path"] for r in csv.DictReader(source)}
    with mapping.with_name("02_payment_candidates.csv").open(encoding="utf-8-sig", newline="") as source:
        by_project = defaultdict(list)
        for row in csv.DictReader(source):
            by_project[row["id"]].append(row)
    lines = []
    for project in json.loads(mapping.read_text(encoding="utf-8-sig")):
        path = package / manifest[project["sha256"]]
        book = load_workbook(path, read_only=True, data_only=True)
        formulas = load_workbook(path, read_only=True, data_only=False)
        try:
            sheet = project["payment_sheet"]
            values = sheet_cells(book[sheet])
            expressions = sheet_cells(formulas[sheet])
            for row in by_project.pop(project["id"], []):
                cell = row["amount_cell"]
                column, number = coordinate_from_string(cell)
                stream = row["stream"]
                amount = Decimal(row["amount"])
                if row["workbook"] != path.name or row["sheet"] != sheet or abs(Decimal(str(values[cell])) - amount) > Decimal("0.000001"):
                    raise ValueError(f"Conflicting payment evidence at {path.name} {sheet}!{cell}")
                context = json.loads(row["context_json"])
                if stream == "allocation":
                    if values.get(column + "3", "").strip() != row["category"].strip():
                        raise ValueError(f"Allocation heading mismatch at {sheet}!{cell}")
                    own_values = [values.get(f"A{number}")]
                    narrative = row["description_raw"].strip()
                    control = column + str(project["allocation_control_row"])
                else:
                    # Each horizontal schedule has its own date/description columns.
                    own_values = [values.get(key) for key in context if key != cell]
                    narrative = " | ".join(x.strip() for x in own_values if isinstance(x, str) and x.strip())
                    control = project["main_control_cell"] if stream == "main_schedule" else None
                membership = control_cells(expressions.get(control))
                if stream == "unallocated_schedule":
                    basis = "transfer_breakdown"
                elif membership is None:
                    basis = "unresolved_control"
                elif cell not in membership:
                    basis = "excluded_by_source_control"
                elif stream == "allocation":
                    basis = "project_allocation"
                elif amount < 0:
                    basis = "signed_schedule_adjustment"
                elif isinstance(expressions[cell], str) and expressions[cell].startswith("="):
                    basis = "formula_settlement_review"
                elif re.search(r"مطلوب مراج[ع]?ه|لحين توضيح", narrative):
                    basis = "unresolved_source_note"
                elif re.search(r"تسوي[ةه]|تقفيل حساب|تخليص حساب", narrative) and not re.search(r"من (?:المالك|العميل)", narrative):
                    basis = "settlement_funding_unconfirmed"
                else:
                    basis = "main_schedule_entry"
                lines.append(dict(sha=project["sha256"], filename=path.name, sheet=sheet, cell=cell,
                                  row=number, stream=stream, basis=basis, category=category_for(row["category"]),
                                  amount=str(amount.quantize(Decimal("0.01"))), currency=row["currency"],
                                  date=row_date(own_values, narrative), narrative=narrative,
                                  source=dict(context=context, control=control, formula=row["formula"],
                                              heading=row["category"], raw_date=row["date_raw"])))
        finally:
            book.close()
            formulas.close()
    if by_project:
        raise ValueError("Payment candidates reference an unknown workbook")
    return lines


if __name__ == "__main__":
    run_recovery(funding_lines, "funding.sql", __doc__)
