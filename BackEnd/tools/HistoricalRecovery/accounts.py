"""Recover named work accounts and independent payment columns from original workbooks."""

from collections import defaultdict
import re

from openpyxl.utils.cell import range_boundaries

from funding import control_cells, row_date
from office_rates import run_recovery
from works import amount, norm, wording, work_lines


# These labels in the supplier column describe a document, expense, trade or role.
# Keep the source wording on the work line without opening a personal account.
NON_NAMES = {norm(s) for s in (
    "فاتوره", "فواتير", "بالفاتوره", "فاتوره كيماويات بناء", "اكراميه", "مصروف", "باليوميات",
    "توريدات", "غفره", "غفير", "الغفير", "رمل", "نقل", "نقل تربه", "نقل ردم", "ماده",
    "طوب بلوك", "رش مياه", "هليكوبتر", "عزل", "مساحه", "حفار", "دكاك", "خرسانه",
    "دفاع مدني", "العميل", "مقاول", "حداد", "البنا", "تبع البنا", "المالك", "الكهربائى",
    "كيماويات بناء", "المحاره", "العرب", "عرب", "المقاول", "المكتب", "البناء", "السباكه", "دفعه من تحت الحساب",
)}


def is_name(value):
    return (bool(re.search(r"[\u0621-\u064aA-Za-z]", value)) and norm(value) not in NON_NAMES
            and not value.startswith(("نقل من", "فاتور", "من ")))


def account_heading(sheet):
    for cells in sheet.iter_rows(min_row=2, max_row=6):
        for cell in cells:
            label = wording(cell.value)
            match = re.match(r"(?:كشف\s+)?حساب\s+(?:مقاول\s+[^:：]+|العرب(?:\s*\(الحفر\))?|الحفر|المباني)\s*[:：]\s*(.+)$", label)
            if match and is_name(match[1].strip()):
                return match[1].strip()
    return ""


def payment_cells(sheet, formulas, header):
    """Restrict payments to the source table or the payment column's SUM boundary."""
    col = header.column
    ranges = set()
    for table in sheet.tables.values():
        left, top, right, bottom = range_boundaries(table.ref)
        if top == header.row and left <= col <= right:
            ranges.add((top + 1, bottom - (table.totalsRowCount or 0)))
    numbers = set()
    for row in formulas.iter_rows(min_row=header.row+1,min_col=col,max_col=col+1):
        for end in row:
            expr = end.value
            if not isinstance(expr, str) or not expr.startswith("="):
                continue
            for ref in re.findall(r"SUM\((\$?[A-Z]+\$?\d+:\$?[A-Z]+\$?\d+)\)", expr.upper()):
                first, top, last, bottom = range_boundaries(ref)
                if first == col and bottom > header.row and end.row > bottom:
                    ranges.add((max(top,header.row+1), bottom))
            if end.column == col and "+" in expr:
                for ref in control_cells(expr) or ():
                    term = sheet[ref]
                    if term.column == col and header.row < term.row < end.row:
                        numbers.add(term.row)
            # A literal paid-to-date total can stand alone in a second statement block.
            prev = row[0]
            if end.column == col+1 and re.fullmatch(r"=[A-Z]+\d+-"+prev.coordinate,expr) and isinstance(prev.value,(int,float)):
                if not ranges and not numbers:
                    numbers.add(prev.row)
    numbers.update(n for top,bottom in ranges for n in range(top,bottom+1))
    return [sheet.cell(n,col) for n in sorted(numbers)
            if amount(sheet.cell(n,col).value) is not None and sheet.cell(n,col).value]


def account_rows(project, book, formulas, works):
    result = []
    blocks = defaultdict(list)
    headings = {s.title: account_heading(s) for s in book}
    for w in works:
        sheet = book[w["sheet"]]
        col = sheet[w["cell"]].column
        party, role = w["party"], w["role"].removesuffix("_candidate")
        if w["family"] == "steel" and not party:
            party = sheet.title.split("الحديد")[-1].strip()
        if w["family"] in ("building","finishing","excavation") and headings[sheet.title]:
            # A named account identifies the payee for the statement as a whole.
            # The row's supplier text may refer to somebody else. Preserve it separately.
            party, role = headings[sheet.title], "contractor"
        valid = is_name(party)
        result.append(dict(sha=w["sha"], filename=w["filename"], sheet=w["sheet"], cell=w["cell"],
                           kind="work", party=party, role=role if valid else "", key=norm(party),
                           invalid=bool(party and not valid and norm(party) in NON_NAMES),
                           category=w["category"], amount=w["amount"], currency=w["currency"]))
        blocks[sheet.title].append((col, w["family"], w["category"], party if valid else "", role))
    for title, columns in blocks.items():
        sheet = book[title]
        for cells in sheet.iter_rows(min_row=6, max_row=9):
            for header in cells:
                if norm(header.value) not in ("دفعات","الدفعات","الدفعه"):
                    continue
                earlier = [c for c in columns if c[0] < header.column]
                chosen = max(earlier, key=lambda c:c[0]) if earlier else columns[0]
                col,family,category,_,role = chosen
                if "خرسان" in title and header.column < min((c[0] for c in columns if c[1]=="workmanship"), default=1000):
                    family,category,role = "concrete","المحطة","supplier"
                names = {c[3] for c in columns if c[0]==col and c[1]==family and c[3]}
                party = headings[title]
                if family in ("steel","workmanship") and len(names)==1:
                    party = next(iter(names))
                if party and headings[title]: role = "contractor"
                for cell in payment_cells(sheet, formulas[title], header):
                    adjacent = sheet.cell(cell.row,cell.column+1)
                    # Only the independent payment schedule's adjacent date/narrative belongs to it.
                    neighbor_header = norm(sheet.cell(header.row,header.column+1).value)
                    note = adjacent.value if neighbor_header in ("البيان","التاريخ","تاريخ") else None
                    expr = formulas[title][cell.coordinate]
                    recognized = party and (expr.data_type != "f" or re.fullmatch(r"=[\d.+*/() -]+",str(expr.value)))
                    result.append(dict(sha=project["sha256"], filename=project["workbook"], sheet=title,
                        cell=cell.coordinate, kind="settlement", amount=amount(cell.value), category=category,
                        currency="EGP", party=party, key=norm(party), role=role if recognized else "",
                        date=row_date([note],wording(note)), narrative=wording(note),
                        source=dict(header=header.coordinate, raw_note=str(note or ""),
                                    formula=expr.value if expr.data_type=="f" else "")))
    return result


def accounts(package, mapping):
    return work_lines(package, mapping, account_rows)


if __name__ == "__main__":
    run_recovery(accounts, "accounts.sql", __doc__)
