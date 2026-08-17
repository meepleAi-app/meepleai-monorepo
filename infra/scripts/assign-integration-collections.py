#!/usr/bin/env python3
"""Riassegna [Collection("Integration-Group{A..D}")] per hash dell'FQN.

Issue #3633. Le collection erano assegnate per bounded context, cioè sullo stesso asse dei filtri di
shard di dev-async.yml: ogni shard finiva per contenere solo un paio di gruppi e girava con meno
thread di quelli concessi. Qui l'assegnazione diventa ortogonale al dominio.

Questo script si esegue UNA VOLTA. L'autorità permanente è il test C#
Api.Tests.Architecture.IntegrationCollectionBalanceArchitectureTests, che verifica la stessa regola
a ogni build: se le due implementazioni divergono, il test fallisce — ed è il comportamento voluto.

Uso:
    python infra/scripts/assign-integration-collections.py           # report, non scrive
    python infra/scripts/assign-integration-collections.py --apply   # riscrive i file
"""

import argparse
import collections
import hashlib
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2] / "apps" / "api" / "tests" / "Api.Tests"
GROUPS = [
    "Integration-GroupA",
    "Integration-GroupB",
    "Integration-GroupC",
    "Integration-GroupD",
]

ATTR = re.compile(r'\[Collection\("(Integration-Group[A-D])"\)\]')
NAMESPACE = re.compile(r"^\s*namespace\s+([\w\.]+)", re.M)
CLASS = re.compile(
    r"\b(?:public\s+|internal\s+|sealed\s+|abstract\s+|partial\s+|static\s+)*class\s+(\w+)"
)
SKIP_PARTS = {"obj", "bin", "TestResults"}


def group_for(fqn: str) -> str:
    """SHA-256(UTF-8(fqn)) -> primi 4 byte big-endian -> mod 4.

    Deve restare identica a IntegrationCollectionBalanceArchitectureTests.GroupFor.
    """
    digest = hashlib.sha256(fqn.encode("utf-8")).digest()
    return GROUPS[int.from_bytes(digest[:4], "big") % len(GROUPS)]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="riscrive i file invece di elencare")
    args = parser.parse_args()

    counts = collections.Counter()
    moved = 0
    scanned = 0

    for path in sorted(ROOT.rglob("*.cs")):
        if SKIP_PARTS & set(path.parts):
            continue

        # newline="" preserva i terminatori di riga esistenti: senza, su Windows l'intero file
        # verrebbe riscritto e il diff sarebbe illeggibile invece che di una riga per classe.
        with open(path, encoding="utf-8", newline="") as handle:
            text = handle.read()

        matches = list(ATTR.finditer(text))
        if not matches:
            continue

        namespace = NAMESPACE.search(text)
        if not namespace:
            print(f"ERRORE: namespace non risolto in {path}", file=sys.stderr)
            return 2

        # Si applica dal fondo verso l'inizio, così gli offset dei match precedenti restano validi.
        edits = []
        for match in matches:
            declaration = CLASS.search(text, match.end())
            if not declaration:
                print(f"ERRORE: classe non risolta dopo {match.group(0)} in {path}", file=sys.stderr)
                return 2

            fqn = f"{namespace.group(1)}.{declaration.group(1)}"
            want = group_for(fqn)
            counts[want] += 1
            scanned += 1
            if match.group(1) != want:
                moved += 1
                edits.append((match.start(), match.end(), f'[Collection("{want}")]'))

        if edits and args.apply:
            for start, end, replacement in reversed(edits):
                text = text[:start] + replacement + text[end:]
            with open(path, "w", encoding="utf-8", newline="") as handle:
                handle.write(text)

    print(f"classi con collection di integrazione: {scanned}")
    for group in GROUPS:
        share = counts[group] / scanned * 100 if scanned else 0
        print(f"  {group}: {counts[group]:>4}  ({share:.1f}%)")
    print(f"da spostare: {moved}")
    print("APPLICATO" if args.apply else "(report soltanto: rilanciare con --apply)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
