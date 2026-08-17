#!/usr/bin/env python3
"""Riassegna [Collection("Integration-Group{A..D}")] per hash dell'FQN.

Issue #3633. Le collection erano assegnate per bounded context, cioè sullo stesso asse dei filtri di
shard di dev-async.yml: ogni shard finiva per contenere solo un paio di gruppi e girava con meno
thread di quelli concessi. Qui l'assegnazione diventa ortogonale al dominio.

Questo script si esegue UNA VOLTA. L'autorità permanente è il test C#
Api.Tests.Architecture.IntegrationCollectionBalanceArchitectureTests, che verifica la stessa regola
a ogni build: se le due implementazioni divergono, il test fallisce — ed è il comportamento voluto.

Le classi annidate sono ESCLUSE per scelta, non per svista. Il test C# le esclude a sua volta
(`.Where(t => !t.IsNested)`): la riflessione costruisce il loro FullName come `Outer+Inner`, non
`Namespace.Inner`, e questo script — testuale, senza un vero parser C# — non può ricostruire in modo
affidabile la catena dei tipi contenitori (region, classi parziali, annidamento multiplo). Muoverle
comunque, con un FQN sintetico che nessuna riflessione produrrebbe, è esattamente il tipo di deriva
silenziosa che questo lavoro chiude altrove: un calcolo che nessuna autorità verifica. Le occorrenze
annidate trovate vengono quindi saltate e stampate in coda, non ignorate in silenzio.

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

# Euristiche testuali, non un parser: valgono per lo stato attuale della suite, verificato a mano
# (nessun `class` in commento/stringa prima dell'attributo, un solo blocco `namespace` per file). Se
# lo script viene rilanciato dopo una ristrutturazione dei file di test, riverificare l'assunzione —
# non c'è nulla che impedisca di rilanciarlo nonostante il nome "una volta sola".
ATTR = re.compile(r'\[Collection\("(Integration-Group[A-D])"\)\]')
NAMESPACE = re.compile(r"^\s*namespace\s+([\w\.]+)", re.M)
CLASS = re.compile(
    r"\b(?:public\s+|internal\s+|sealed\s+|abstract\s+|partial\s+|static\s+)*class\s+(\w+)"
)
SKIP_PARTS = {"obj", "bin", "TestResults"}


def is_nested(text: str, namespace_end: int, position: int) -> bool:
    """True se `position` cade dentro il corpo di un tipo che lo racchiude.

    Conta le graffe fra la fine della dichiarazione di namespace e `position`: un saldo positivo
    (più `{` che `}`) significa che l'attributo sta dentro un corpo di classe già aperto, quindi la
    classe a cui appartiene è annidata. Per le ~370 classi di questo sweep l'attributo di una classe
    top-level cade sempre prima della sua stessa graffa di apertura (saldo 0); solo l'unica classe
    annidata nota (PostgresFullTextSearchTests, dentro SharedGameRagIntegrationTests) ha saldo > 0,
    perché il suo attributo segue la graffa di apertura della classe esterna. Stessa euristica
    testuale di ATTR/CLASS/NAMESPACE: non distingue graffe di codice da graffe dentro stringhe o
    commenti, ma nessun caso simile è presente nella zona d'interesse di questo sweep.
    """
    segment = text[namespace_end:position]
    return segment.count("{") - segment.count("}") > 0


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
    skipped_nested = []

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

            if is_nested(text, namespace.end(), match.start()):
                # Fuori dalla regola per scelta: vedi docstring del modulo e is_nested(). Non entra
                # né nel conteggio né nella ripartizione — la stessa popolazione che vede il guard.
                skipped_nested.append((path, declaration.group(1)))
                continue

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
    if skipped_nested:
        print(f"classi annidate escluse dalla regola ({len(skipped_nested)}, non riassegnate):")
        for path, class_name in skipped_nested:
            print(f"  {path}: {class_name}")
    print("APPLICATO" if args.apply else "(report soltanto: rilanciare con --apply)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
