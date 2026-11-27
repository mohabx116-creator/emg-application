"""Recover historical work components from their original tables and control dependencies."""

from collections import defaultdict
import csv
from decimal import Decimal
import hashlib
import json
import re

from openpyxl import load_workbook
from openpyxl.utils.cell import range_boundaries

from funding import category_for, control_cells, row_date
from office_rates import run_recovery


def amount(value):
    if not isinstance(value, (int, float, Decimal)) or isinstance(value, bool):
        return None
    return str(Decimal(str(value)).quantize(Decimal("0.000001")))


def wording(value):
    return value.strip() if isinstance(value, str) else ""


def norm(value):
    return wording(value).translate(str.maketrans("أإآىة", "ااايه")).replace(" ", "")


def work_category(label):
    account = re.sub(r"^(?:دفعات|دفات|دفعه)\s+(?:تحت\s+)?(?:ال)?حساب\s+", "", label.strip())
    if account.startswith("لل"):
        account = "ال" + account[2:]
    return category_for(account)


def indexed_rows(path):
    result = defaultdict(list)
    with path.open(encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            result[row["id"]].append(row)
    return result


def work_lines(package, mapping, account_reader=None):
    directory = mapping.parent
    candidates = indexed_rows(directory / "08_work_component_candidates.csv")
    checks = indexed_rows(directory / "09_work_control_checks.csv")
    projects = json.loads(mapping.read_text(encoding="utf-8-sig"))
    with (package / "Index" / "workbooks.csv").open(encoding="utf-8-sig", newline="") as stream:
        manifest = {r["sha256"]: r["archive_path"] for r in csv.DictReader(stream) if r["kind"] == "project_candidate"}
    ids = {p["id"] for p in projects}
    if (len(ids) != len(projects) or set(candidates) - ids or set(checks) != ids
        or len(projects) != len(manifest) or {p["sha256"] for p in projects} != set(manifest)):
        raise ValueError("Project mapping does not cover the manifest")
    results = []
    for project in projects:
        path = (package / manifest[project["sha256"]]).resolve(strict=True)
        if path.name != project["workbook"] or not path.is_relative_to((package / "Originals").resolve()) or hashlib.sha256(path.read_bytes()).hexdigest() != project["sha256"]:
            raise ValueError("Original workbook identity mismatch")
        book = load_workbook(path, data_only=True)
        expressions = load_workbook(path, data_only=False)
        try:
            project_start = len(results)
            inputs = {}
            boundaries = defaultdict(list)
            for check in checks[project["id"]]:
                sheet, address = check["sheet"], check["source_control"]
                actual = book[sheet][address].value
                cached = check["cached"]
                if cached and (amount(actual) is None or abs(Decimal(str(actual))-Decimal(cached)) > Decimal("0.000001")):
                    raise ValueError(f"Source control mismatch at {sheet}!{address}")
                boundaries[sheet, address].append({k: check[k] for k in
                    ("payment_sheet_control", "payment_sheet_formula", "formula", "cached", "difference", "status")})
            for r in candidates[project["id"]]:
                if r["workbook"] != path.name:
                    raise ValueError("Work candidate workbook mismatch")
                key = (r["sheet"], r["amount_cell"])
                if key not in inputs:
                    inputs[key] = (work_category(r["category"]), r["control_cell"], r["formula"], Decimal(r["amount"]))
            # Find real detail tables even when the payment control refers to
            # a structured table directly, bypasses a sheet, or has a broken link.
            table_headers = {}
            for sheet in book:
                if sheet.title in (project["payment_sheet"], "تقارير", "مرفقات") or "نهائ" in sheet.title:
                    continue
                def add_detail(cell, category, control):
                    if amount(cell.value) is not None and cell.value:
                        inputs.setdefault((sheet.title, cell.coordinate),
                            (category, control, "", Decimal(str(cell.value))))
                # Quote-format excavation sheets sometimes carry a different trade
                # in the account heading. Follow the amount and transport subtotals.
                if "حفر" in sheet.title and norm(sheet["G7"].value) == "تكلفهالنقل":
                    for line in expressions[sheet.title].iter_rows(min_col=13, max_col=13):
                        total = line[0]
                        if not re.fullmatch(r"=SUM\(N\d+:N\d+\)\+G\d+", str(total.value), re.I):
                            continue
                        block = "الطوب" if "الطوب" in wording(sheet["O2"].value) else "العرب"
                        for ref in sorted(control_cells(total.value)):
                            nested = expressions[sheet.title][ref].value
                            leaves = control_cells(nested) if ref.startswith("G") else None
                            for addr in sorted(leaves or {ref}):
                                add_detail(sheet[addr], block, total.coordinate)
                for table in sheet.tables.values():
                    first, top, last, bottom = range_boundaries(table.ref)
                    headers = {norm(sheet.cell(top, col).value): col for col in range(first, last + 1)}
                    money = next((col for label, col in headers.items() if "اجماليتكلفهالعمل" in label), None)
                    if money is None and {"البيان", "الكميه", "السعر"} <= headers.keys():
                        money = headers.get("الاجمالي")
                    if money is None:
                        continue
                    for n in range(top + 1, bottom - (table.totalsRowCount or 0) + 1):
                        cell = sheet.cell(n, money)
                        if amount(cell.value) is None or not cell.value:
                            continue
                        key = (sheet.title, cell.coordinate)
                        table_headers[key] = headers
                        if key not in inputs:
                            if "حفر" in sheet.title:
                                cat = "العرب"
                            elif "بنا" in sheet.title or "مباني" in sheet.title:
                                cat = "المباني"
                            else:
                                cat = sheet.title.strip()
                            inputs[key] = (cat, table.ref, "", Decimal(str(cell.value)))

            for (sheet_name, address), (category, control, formula, expected) in inputs.items():
                sheet = book[sheet_name]
                cell = sheet[address]
                value = amount(cell.value)
                if value is None or abs(Decimal(value) - expected) > Decimal("0.000001"):
                    raise ValueError(f"Original work value differs at {project['id']} {sheet_name}!{address}")
                n, col = cell.row, cell.column
                def at(c):
                    return sheet.cell(n, c).value if c and c > 0 else None
                headers = table_headers.get((sheet_name, address), {})
                if not headers and n > 9:
                    standard = {norm(c.value): c.column for c in sheet[9] if wording(c.value)}
                    if standard.get("اجماليتكلفهالعملالاعمال") == col:
                        headers = standard
                def field(*keys):
                    return next((at(headers[k]) for k in keys if k in headers), None)
                narrative = party = unit = role = ""
                work_date = quantity = rate = progress = None
                basis = "work_detail"
                if headers:
                    narrative = wording(field("نوعالاعمال", "البيان"))
                    unit = wording(field("الوحده", "الوحد"))
                    quantity = amount(field("مجموعمقدارلاعمال", "مجموعمقدارالاعمال", "الكميه"))
                    rate = amount(field("السعربالجنيه", "السعر"))
                    work_date = field("التاريخ")
                    party = wording(field("اسمالمورد"))
                    family = ("site" if "الموقع" in sheet_name else "excavation" if "حفر" in sheet_name
                              else "building" if any(t in norm(sheet_name) for t in ("البنا", "مباني")) else "finishing")
                    if family == "building" or family == "finishing" and any(t in norm(sheet_name) for t in ("عزل","محار","سباك","كهرب")):
                        role = "contractor_candidate"
                    else:
                        role = "supplier_candidate"
                elif "حديد" in sheet_name:
                    family = "steel"
                    quantity, rate, unit = amount(at(3)), amount(at(5)), "طن"
                    work_date, narrative = at(2), wording(at(7))
                    title = wording(sheet["A2"].value)
                    party = re.split("الحديد", title)[-1].strip(" :-،") if "الحديد" in title else ""
                    role = "supplier"
                elif "خرسان" in sheet_name and category == "المحطة":
                    family = "concrete"
                    # The net amount, narrative and station columns shift together.
                    date_col = next((i for i in range(1, col) if norm(sheet.cell(7,i).value) == "التاريخ"), None)
                    type_col = next((i for i in range(1, col) if norm(sheet.cell(7,i).value) == "النوع"), None)
                    narrative = " — ".join(filter(None, (wording(at(type_col)), wording(at(col + 1)))))
                    party, role = wording(at(col + 2)), "supplier"
                    rate, unit, work_date = amount(at(col - 1)), "م3", at(date_col)
                    qcol = next((i for i in range(1, col) if "مورد" in norm(sheet.cell(8, i).value)), None)
                    quantity = amount(at(qcol))
                elif "خرسان" in sheet_name and category == "المقاول":
                    family, role = "workmanship", "contractor"
                    # Bound the contractor statement independently of the station table.
                    anchor = max((i for i in range(1, col) if norm(sheet.cell(7,i).value) in ("بيانبالاعمال","بيانالاعمال")
                                  or "مصنعيهالمقاول" in norm(sheet.cell(7,i).value)), default=0)
                    if anchor:
                        narrative = wording(at(anchor))
                        unit_col = next((i for i in range(anchor,col) if "الوحد" in norm(sheet.cell(7,i).value)), None)
                        unit = wording(at(unit_col))
                        for i in range(anchor, col):
                            h7, h8 = norm(sheet.cell(7,i).value), norm(sheet.cell(8,i).value)
                            if h7 == "النسبه": progress = amount(at(i))
                            if h7 == "الفئه" or h8 == "السعر": rate = amount(at(i))
                            if h8 in ("الاجمالي","الكميه"): quantity = amount(at(i))
                    for i in range(max(1,anchor-1), col + 1):
                        if "كشفحسابالمقاول" in norm(sheet.cell(5,i).value):
                            label = wording(sheet.cell(5,i).value)
                            party = (label.split(":", 1)[1].strip() if ":" in label else "") or wording(sheet.cell(6,i).value)
                        if "تاريخاصدار" in norm(sheet.cell(5,i).value):
                            work_date = sheet.cell(6,i).value
                        match = re.search(r"مصنعي[ةه] المقاول\s*[:：]\s*(.+)", wording(sheet.cell(7,i).value))
                        if match: party = match[1].strip()
                elif "حفر" in sheet_name:
                    family = "building" if category == "الطوب" else "excavation"
                    narrative, quantity, rate = wording(at(6)), amount(at(12)), amount(at(13))
                    if "م3" in wording(sheet.cell(7,12).value): unit = "م3"
                    if quantity is None and rate is None: basis = "source_lump_sum"
                else:
                    raise ValueError(f"Unknown work block {sheet_name}!{address}")
                date_value = row_date([work_date], wording(work_date))
                details = {c.coordinate: str(c.value) for c in sheet[n] if c.value is not None}
                formulas = {c.coordinate: c.value for c in expressions[sheet_name][n] if c.data_type == "f" and isinstance(c.value, str)}
                actual_formula = formulas.get(address, "").lstrip("=")
                if formula and formula != actual_formula:
                    raise ValueError(f"Work formula mismatch at {sheet_name}!{address}")
                results.append(dict(sha=project["sha256"], filename=path.name, sheet=sheet_name, cell=address, row=n,
                                    family=family,category=category,basis=basis,amount=value,date=date_value,currency="EGP",
                                    description=narrative,unit=unit,quantity=quantity,rate=rate,progress=progress,party=party,role=role,
                                    source=dict(control=control, checks=boundaries[sheet_name, control], role=role,
                                                formula=actual_formula, formulas=formulas, values=details)))
            if account_reader:
                results[project_start:] = account_reader(project, book, expressions, results[project_start:])
        finally:
            book.close()
            expressions.close()
    return results


if __name__ == "__main__":
    run_recovery(work_lines, "works.sql", __doc__)
