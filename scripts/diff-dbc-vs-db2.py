#!/usr/bin/env python3
"""Find what the legacy 3.3.5a server can reference but the modern client cannot resolve.

The proxy translates packets from a 3.3.5a server to a 3.4.3 client. When the server
sends a record id the client's own DB2 does not contain, the client silently does
nothing — it cannot look the row up. Those ids, and only those, are what belongs in
HermesProxy/CSV/Hotfix as a polyfill.

    ids in the 3.3.5a DBC  -  ids in the 3.4.3 DB2  =  polyfill candidates

This is how the Dark Portal fix was found: AreaTrigger 4354 / 3646 / 3647 exist in
3.3.5a and are absent from the 3.4.3 client, so the client could never fire them until
the proxy shipped them as hotfix rows.

The mirror image of this question — "is a file we already ship just a copy of client
data?" — is answered by scripts/compare-hotfix-csv.py.

Usage
-----
    python scripts/diff-dbc-vs-db2.py AreaTrigger
    python scripts/diff-dbc-vs-db2.py SkillLine --build 3.4.3.54261
    python scripts/diff-dbc-vs-db2.py --all                # every DBC with a DB2 counterpart
    python scripts/diff-dbc-vs-db2.py AreaTrigger --dbc-dir "D:/wow335a/dbc"

The 3.3.5a DBC directory is found via, in order:
    1. --dbc-dir
    2. the HERMES_TOOLS_DBC_335A environment variable
Extract it from the 3.3.5a client with any MPQ tool; the files are the loose .dbc set.

DB2 downloads are cached under .cache/db2/<build>/ and shared with compare-hotfix-csv.py.

WDBC format (3.3.5a)
--------------------
    magic            char[4]   'WDBC'
    record_count     uint32
    field_count      uint32
    record_size      uint32    == field_count * 4 for every 3.3.5a table
    string_size      uint32
    records          record_count * record_size
    string block     string_size

Every field is 4 bytes. Field 0 is the row id in effectively all 3.3.5a tables; pass
--id-field if you hit one where it is not.
"""

from __future__ import annotations

import argparse
import csv
import os
import struct
import sys
import urllib.request
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
CACHE_DIR = REPO_ROOT / ".cache" / "db2"
WAGO = "https://wago.tools/db2/{table}/csv?build={build}"
DBC_ENV = "HERMES_TOOLS_DBC_335A"


def resolve_dbc_dir(explicit: str | None) -> Path:
    candidate = explicit or os.environ.get(DBC_ENV)
    if not candidate:
        sys.exit(
            f"No 3.3.5a DBC directory. Pass --dbc-dir, or set {DBC_ENV}:\n"
            f"  PowerShell:  [Environment]::SetEnvironmentVariable('{DBC_ENV}', '<path>', 'User')"
        )
    path = Path(candidate)
    if not path.is_dir():
        sys.exit(f"{DBC_ENV}/--dbc-dir points at '{path}', which is not a directory")
    return path


def read_dbc_ids(path: Path, id_field: int = 0) -> set[int]:
    """Return the set of row ids in a WDBC file."""
    data = path.read_bytes()
    if data[:4] != b"WDBC":
        raise ValueError(f"{path.name}: not a WDBC file (magic {data[:4]!r})")
    record_count, field_count, record_size, _string_size = struct.unpack_from("<4I", data, 4)
    if field_count and record_size != field_count * 4:
        raise ValueError(f"{path.name}: record_size {record_size} != field_count {field_count} * 4")
    if id_field >= field_count:
        raise ValueError(f"{path.name}: id field {id_field} out of range (field_count={field_count})")

    ids: set[int] = set()
    base = 20 + id_field * 4
    for i in range(record_count):
        (value,) = struct.unpack_from("<I", data, base + i * record_size)
        ids.add(value)
    return ids


def fetch_db2_ids(table: str, build: str) -> set[int] | None:
    cached = CACHE_DIR / build / f"{table}.csv"
    if not (cached.exists() and cached.stat().st_size > 0):
        cached.parent.mkdir(parents=True, exist_ok=True)
        url = WAGO.format(table=table, build=build)
        # wago.tools answers 403 to urllib's default User-Agent.
        request = urllib.request.Request(url, headers={"User-Agent": "HermesProxy-hotfix-audit/1.0"})
        try:
            with urllib.request.urlopen(request, timeout=300) as response:
                cached.write_bytes(response.read())
        except Exception:
            cached.unlink(missing_ok=True)
            return None
    if cached.stat().st_size == 0:
        cached.unlink(missing_ok=True)
        return None

    with cached.open(encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    if not rows:
        return None
    id_col = "ID" if "ID" in rows[0] else next(iter(rows[0]))
    ids: set[int] = set()
    for row in rows:
        try:
            ids.add(int(row[id_col]))
        except (TypeError, ValueError):
            pass
    return ids


def report(table: str, dbc_dir: Path, build: str, id_field: int, quiet_when_empty: bool) -> bool:
    dbc_path = dbc_dir / f"{table}.dbc"
    if not dbc_path.exists():
        if not quiet_when_empty:
            print(f"{table:<28} no {table}.dbc in {dbc_dir}")
        return False

    try:
        legacy = read_dbc_ids(dbc_path, id_field)
    except ValueError as exc:
        print(f"{table:<28} SKIPPED ({exc})")
        return False

    modern = fetch_db2_ids(table, build)
    if modern is None:
        print(f"{table:<28} SKIPPED (no 3.4.3 DB2 for this table)")
        return False

    missing = sorted(legacy - modern)
    if not missing and quiet_when_empty:
        return False

    verdict = "client covers everything" if not missing else f"{len(missing)} ids need polyfill"
    print(f"{table:<28} 3.3.5a={len(legacy):<7} 3.4.3={len(modern):<7} {verdict}")
    if missing:
        preview = ", ".join(str(i) for i in missing[:12])
        print(f"    only in 3.3.5a: {preview}{' ...' if len(missing) > 12 else ''}")
    return bool(missing)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("table", nargs="?", help="table name without extension, e.g. AreaTrigger")
    parser.add_argument("--all", action="store_true", help="every .dbc that has a 3.4.3 DB2 counterpart")
    parser.add_argument("--build", default="3.4.3.54261", help="modern client build (default: %(default)s)")
    parser.add_argument("--dbc-dir", help=f"3.3.5a DBC directory (default: ${DBC_ENV})")
    parser.add_argument("--id-field", type=int, default=0, help="0-based index of the id field (default: 0)")
    args = parser.parse_args()

    dbc_dir = resolve_dbc_dir(args.dbc_dir)

    if args.all:
        tables = sorted(p.stem for p in dbc_dir.glob("*.dbc"))
        print(f"Scanning {len(tables)} tables from {dbc_dir} against {args.build}\n")
        hits = sum(report(t, dbc_dir, args.build, args.id_field, quiet_when_empty=True) for t in tables)
        print(f"\n{hits} table(s) have ids the 3.4.3 client cannot resolve")
    elif args.table:
        report(args.table, dbc_dir, args.build, args.id_field, quiet_when_empty=False)
    else:
        parser.error("give a table name, or --all")
    return 0


if __name__ == "__main__":
    sys.exit(main())
