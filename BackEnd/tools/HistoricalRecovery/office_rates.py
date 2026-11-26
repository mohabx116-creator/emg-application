"""Recover an imported project's office rate from its original XLSX and mapping.

Run on the local Docker host. The default invocation validates and rolls back.
--apply requires the path and verified SHA-256 of the pre-recovery database dump.
"""

import argparse
import base64
import csv
import hashlib
import json
from decimal import Decimal
from pathlib import Path
import subprocess

from openpyxl import load_workbook


def source_rates(package, mapping):
    with (package / "Index" / "workbooks.csv").open(encoding="utf-8-sig", newline="") as source:
        manifest = {row["sha256"]: row for row in csv.DictReader(source)
                    if row["kind"] == "project_candidate"}
    projects = json.loads(mapping.read_text(encoding="utf-8-sig"))
    if len(projects) != len(manifest) or {project["sha256"] for project in projects} != set(manifest):
        raise ValueError("The mapping and the project manifest differ")
    result = []
    for project in projects:
        entry = manifest[project["sha256"]]
        path = (package / entry["archive_path"]).resolve(strict=True)
        if not path.is_relative_to((package / "Originals").resolve()) or path.name != project["workbook"]:
            raise ValueError(f"Original path mismatch for {project['id']}")
        if hashlib.sha256(path.read_bytes()).hexdigest() != project["sha256"]:
            raise ValueError(f"Original hash mismatch for {project['id']}")
        book = load_workbook(path, read_only=True, data_only=True)
        try:
            sheet, = [s for s in book if "نهائ" in s.title]
            address = project["office_percent_cell"]
            if not address:
                continue
            cell = sheet[address]
            if cell.value is None and not project["office_percent"]:
                continue
            if cell.data_type != "n" or cell.value is None or "%" not in cell.number_format:
                raise ValueError(f"Unverified percentage at {project['id']} {sheet.title}!{address}")
            rate = Decimal(str(cell.value)) * 100
            if rate != Decimal(project["office_percent"]) or not 0 <= rate <= 100:
                raise ValueError(f"Conflicting rate at {project['id']} {address}")
            result.append(dict(sha=project["sha256"], filename=path.name, sheet=sheet.title,
                               address=address, rate=str(rate)))
        finally:
            book.close()
    return result


def local_docker(*args, data=None):
    # Use the host's local named pipe even if the shell has a remote Docker context.
    command = ["docker", "--host", "npipe:////./pipe/dockerDesktopLinuxEngine", *args]
    return subprocess.run(command, input=data, stdout=subprocess.PIPE, check=True).stdout


def run_recovery(source_reader, sql_name, description):
    parser = argparse.ArgumentParser(description=description)
    parser.add_argument("package", type=Path)
    parser.add_argument("mapping", type=Path)
    parser.add_argument("--container", required=True)
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--backup", type=Path)
    parser.add_argument("--backup-sha256")
    args = parser.parse_args()
    records = source_reader(args.package, args.mapping)
    if args.apply:
        if not args.backup or not args.backup_sha256:
            parser.error("--apply requires a verified backup")
        with args.backup.open("rb") as dump:
            valid = dump.read(5) == b"PGDMP"
            dump.seek(0)
            if not valid or hashlib.file_digest(dump, "sha256").hexdigest() != args.backup_sha256:
                parser.error("Backup differs from the verified database dump")
    if local_docker("port", args.container, "5432/tcp").strip() != b"127.0.0.1:55464":
        parser.error("Unexpected PostgreSQL port binding")
    role = local_docker("exec", args.container, "printenv", "POSTGRES_USER").decode().strip()
    payload = base64.b64encode(json.dumps(records, ensure_ascii=False).encode()).decode("ascii")
    sql = Path(__file__).with_name(sql_name).read_text(encoding="utf-8")
    output = local_docker("exec", "-i", args.container, "psql", "-X", "-v", "ON_ERROR_STOP=1",
                          "-U", role, "-d", "emg_restore", "-P", "pager=off",
                          data=(sql.replace("__MANIFEST_BASE64__", payload)
                                + ("\nCOMMIT;\n" if args.apply else "\nROLLBACK;\n")).encode())
    print(output.decode("utf-8"))


if __name__ == "__main__":
    run_recovery(source_rates, "office_rates.sql", __doc__)
