#!/usr/bin/env python3
"""Classify a CSV/Hotfix file as a DBC MIRROR or a real POLYFILL.

Hotfixes are an *override* channel. The client only needs rows that differ from
its own baked-in DB2, or that do not exist in it at all. A file whose every row
is identical to client data is dead weight: it costs startup parse time, memory
and (if advertised) a login round trip, and changes nothing.

This compares a file in HermesProxy/CSV/Hotfix against the client's own data for
a given build, fetched from wago.tools.

    MIRROR    every row identical to the client's -> the proxy has no reason to carry it
    POLYFILL  rows differ or do not exist client-side -> genuinely ours, keep it

Usage
-----
    # one table, downloading the client data automatically
    python scripts/compare-hotfix-csv.py AreaTrigger3.csv --build 3.4.3.54261

    # every *3.csv in the hotfix directory
    python scripts/compare-hotfix-csv.py --all 3 --build 3.4.3.54261

    # compare against a CSV you already have
    python scripts/compare-hotfix-csv.py SkillLine3.csv --client path/to/SkillLine.csv

Downloads are cached under .cache/db2/<build>/ so repeat runs are offline.

The signedness trap
-------------------
DB2 columns are frequently signed client-side and unsigned in our CSV, so the
*same bits* print differently:

    RaceMask    ours=18446744073709551615   client=-1      (uint64 vs int64)
    CategoryID  ours=255                    client=-1      (uint8  vs int8)

A naive string compare reports those as differences and will label a pure mirror
as a polyfill. This script compares numerically and treats values as equal when
they share the same bit pattern at any common width. Floats are compared with a
tolerance. Do not "simplify" that back to a string compare.

Build numbers
-------------
The suffix on a hotfix CSV is the expansion version, not the build:

    *1.csv -> V1_14 (Classic Era)   e.g. --build 1.14.2.42597
    *2.csv -> V2_5  (TBC Classic)   e.g. --build 2.5.3.42598
    *3.csv -> V3_4_3 (WotLK)        e.g. --build 3.4.3.54261
"""

from __future__ import annotations

import argparse
import csv
import sys
import urllib.request
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
HOTFIX_DIR = REPO_ROOT / "HermesProxy" / "CSV" / "Hotfix"
CACHE_DIR = REPO_ROOT / ".cache" / "db2"
WAGO = "https://wago.tools/db2/{table}/csv?build={build}"


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def values_equal(a: str | None, b: str | None) -> bool:
    """True when two CSV cells mean the same thing.

    Handles the signed/unsigned split described in the module docstring, and
    float formatting differences.
    """
    a = (a or "").strip()
    b = (b or "").strip()
    if a == b:
        return True

    try:
        ia, ib = int(a), int(b)
    except ValueError:
        try:
            return abs(float(a) - float(b)) < 1e-6
        except ValueError:
            return False

    if ia == ib:
        return True

    for bits in (8, 16, 32, 64):
        mask = (1 << bits) - 1
        if abs(ia) <= mask and abs(ib) <= mask and (ia & mask) == (ib & mask):
            return True
    return False


def fetch_client_csv(table: str, build: str) -> Path:
    cached = CACHE_DIR / build / f"{table}.csv"
    if cached.exists() and cached.stat().st_size > 0:
        return cached
    cached.parent.mkdir(parents=True, exist_ok=True)
    url = WAGO.format(table=table, build=build)
    # wago.tools answers 403 to urllib's default User-Agent.
    request = urllib.request.Request(url, headers={"User-Agent": "HermesProxy-hotfix-audit/1.0"})
    with urllib.request.urlopen(request, timeout=300) as response:
        cached.write_bytes(response.read())
    if cached.stat().st_size == 0:
        cached.unlink(missing_ok=True)
        raise RuntimeError(f"wago.tools returned nothing for {table} @ {build}")
    return cached


def compare(ours_path: Path, client_path: Path, id_column: str | None) -> tuple[str, str]:
    ours = read_csv(ours_path)
    if not ours:
        return "EMPTY", f"{ours_path.name}: no rows"

    theirs = read_csv(client_path)
    id_column = id_column or ("ID" if "ID" in ours[0] else next(iter(ours[0])))
    if id_column not in ours[0]:
        return "ERROR", f"{ours_path.name}: no '{id_column}' column"

    client_cols = set(theirs[0].keys()) if theirs else set()
    by_id = {row.get(id_column): row for row in theirs}
    shared = [c for c in ours[0] if c in client_cols]

    identical = differing = new = 0
    examples: list[str] = []
    for row in ours:
        match = by_id.get(row.get(id_column))
        if match is None:
            new += 1
            if len(examples) < 3:
                examples.append(f"row {row.get(id_column)} absent client-side")
            continue
        bad = [c for c in shared if not values_equal(row[c], match[c])]
        if bad:
            differing += 1
            if len(examples) < 3:
                col = bad[0]
                examples.append(
                    f"row {row.get(id_column)} {col}: ours={row[col]!r} client={match[col]!r}"
                )
        else:
            identical += 1

    verdict = "MIRROR" if (differing == 0 and new == 0) else "POLYFILL"
    summary = (
        f"{ours_path.name:<34} rows={len(ours):<7} identical={identical:<7} "
        f"differ={differing:<6} new={new:<6} {verdict}"
    )
    for example in examples:
        summary += f"\n      {example}"
    if not shared:
        summary += "\n      WARNING: no overlapping columns — check the id column / table name"
    return verdict, summary


def table_name_for(csv_name: str) -> str:
    """AreaTrigger3.csv -> AreaTrigger (strip the expansion-version suffix)."""
    stem = Path(csv_name).stem
    return stem[:-1] if stem and stem[-1].isdigit() else stem


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("file", nargs="?", help="CSV under HermesProxy/CSV/Hotfix, e.g. AreaTrigger3.csv")
    parser.add_argument("--all", metavar="SUFFIX", help="compare every *<SUFFIX>.csv, e.g. --all 3")
    parser.add_argument("--build", default="3.4.3.54261", help="client build for wago.tools (default: %(default)s)")
    parser.add_argument("--client", help="path to client CSV instead of downloading")
    parser.add_argument("--id-column", help="id column name (default: ID, else first column)")
    args = parser.parse_args()

    if args.all:
        targets = sorted(HOTFIX_DIR.glob(f"*{args.all}.csv"), key=lambda p: -p.stat().st_size)
    elif args.file:
        candidate = Path(args.file)
        targets = [candidate if candidate.exists() else HOTFIX_DIR / args.file]
    else:
        parser.error("give a file, or --all <suffix>")

    mirrors: list[str] = []
    failures = 0
    for target in targets:
        if not target.exists():
            print(f"{target.name:<34} NOT FOUND")
            failures += 1
            continue
        try:
            client = Path(args.client) if args.client else fetch_client_csv(table_name_for(target.name), args.build)
        except Exception as exc:  # noqa: BLE001 - report and continue over the batch
            print(f"{target.name:<34} SKIPPED ({exc})")
            failures += 1
            continue
        verdict, summary = compare(target, client, args.id_column)
        print(summary)
        if verdict == "MIRROR":
            mirrors.append(target.name)

    if len(targets) > 1:
        reclaimable = sum((HOTFIX_DIR / name).stat().st_size for name in mirrors) / 1024 / 1024
        print(f"\n{len(mirrors)}/{len(targets)} are mirrors — {reclaimable:.1f} MB of client data we do not need to carry")
        if failures:
            print(f"{failures} could not be checked")
    return 0


if __name__ == "__main__":
    sys.exit(main())
