#!/usr/bin/env python3
"""Replica offline di MultiGameHybridSearchService.FuseGlobally, per tarare la fusione in secondi.

PERCHE' ESISTE
--------------
Fra il 2026-08-17 e il 2026-08-22 quattro configurazioni della fusione sono state provate contro
il gate RAG smoke (10/11 -> 8/11 -> 5/11 -> 7/11). Ogni ipotesi e' costata ~45 minuti di CI e
nessuna era verificabile prima: dall'esterno si vede solo il top-3 finale, mai i segnali su cui la
fusione decide. Con il dump `[RAG-TUNE]` (#3748) e la lingua per candidato (#3740) quei segnali
sono osservabili, e questo script li rimette nella stessa formula che gira in produzione.

Ha gia' ripagato il costo di scriverlo: la correzione per lingua di #3743 e' stata valutata qui
prima di spendere una run, su due configurazioni reali gia' misurate.

LA VALIDAZIONE NON E' OPZIONALE
-------------------------------
Una replica che diverge dal codice reale produce numeri sbagliati con l'aria di essere autorevole
— ed e' esattamente il modo in cui questo lavoro ha sbagliato due volte. Lo script quindi
ricostruisce i top-3 della run di riferimento e li confronta con la golden baseline: se un GUID
finisse allineato a nomi diversi in query diverse, la replica non riproduce l'ordinamento reale e
lo script si ferma invece di stampare risultati.

USO
---
1. due run del gate, una per configurazione da confrontare:
       gh workflow run rag-smoke-dispatch.yml -f e5_query_prefix=false
       gh workflow run rag-smoke-dispatch.yml -f e5_query_prefix=true
2. scarica gli artifact:
       gh run download <run_id> -n rag-fusion-tuning-<run_id> -D <dir>
3.     python infra/scripts/rag-fusion-bench.py --reference <dir-run-verde> [--compare <altra-dir>]

La directory di riferimento deve venire da una run in cui il gate e' PASSATO: e' cio' che rende la
golden baseline un ground truth con cui validare la replica.

SE `FuseGlobally` CAMBIA, QUESTO FILE VA CAMBIATO CON ESSA. La validazione lo scopre (i top-3
ricostruiti smettono di combaciare), ma lo scopre solo quando qualcuno lo esegue.
"""
import argparse
import io
import json
import sys
from collections import defaultdict
from pathlib import Path

DECODER = json.JSONDecoder()

# Devono corrispondere a MultiGameHybridSearchService.
VECTOR_WEIGHT = 0.7
KEYWORD_WEIGHT = 0.3
MIN_LANGUAGE_GROUP_SIZE = 5


def load_dump(directory: Path):
    """Le righe [RAG-TUNE] di aggregate.log, una per query."""
    path = directory / "aggregate.log"
    rows = []
    for line in io.open(path, encoding="utf-8"):
        marker = line.find("[RAG-TUNE] {")
        if marker < 0:
            continue
        payload, _ = DECODER.raw_decode(line[marker + len("[RAG-TUNE] "):])
        rows.append(payload)
    if not rows:
        raise SystemExit(f"{path}: nessuna riga [RAG-TUNE]")
    return rows


def extent(values):
    present = [v for v in values if v is not None]
    return (min(present), max(present)) if present else (0.0, 0.0)


def normalise(value, low, high):
    # absent = 0 e' load-bearing (#3735): rende la fusione congiuntiva. Vedi il commento sul
    # metodo omonimo in MultiGameHybridSearchService prima di toccarlo.
    if value is None:
        return 0.0
    span = high - low
    return 1.0 if span <= 1e-7 else (value - low) / span


def language_offsets(candidates):
    """#3740: scostamento della media di ciascun gruppo linguistico dalla media globale."""
    with_vector = [c for c in candidates if c.get("v") is not None]
    if not with_vector:
        return {}
    global_mean = sum(c["v"] for c in with_vector) / len(with_vector)
    groups = defaultdict(list)
    for candidate in with_vector:
        groups[(candidate.get("l") or "?").lower()].append(candidate["v"])
    return {
        language: sum(scores) / len(scores) - global_mean
        for language, scores in groups.items()
        if len(scores) >= MIN_LANGUAGE_GROUP_SIZE
    }


def fuse(candidates, language_correction: bool):
    offsets = language_offsets(candidates) if language_correction else {}
    adjusted = [
        None if c.get("v") is None else c["v"] - offsets.get((c.get("l") or "?").lower(), 0.0)
        for c in candidates
    ]
    v_low, v_high = extent(adjusted)
    k_low, k_high = extent([c.get("k") for c in candidates])

    scored = [
        (
            VECTOR_WEIGHT * normalise(adj, v_low, v_high)
            + KEYWORD_WEIGHT * normalise(c.get("k"), k_low, k_high),
            c,
        )
        for c, adj in zip(candidates, adjusted)
    ]

    neg = float("-inf")
    scored.sort(
        key=lambda t: (
            -t[0],
            -(t[1]["v"] if t[1].get("v") is not None else neg),
            -(t[1]["k"] if t[1].get("k") is not None else neg),
            t[1]["i"],
            t[1]["d"],
        )
    )
    return [c for _, c in scored]


def top_documents(candidates, k, language_correction=True):
    """La correzione per lingua e' ATTIVA per default: dal 2026-08-22 (#3740) e' il comportamento
    di produzione, quindi e' quello che la replica deve riprodurre per essere fedele."""
    return [c["d"] for c in fuse(candidates, language_correction)[:k]]


def build_document_map(rows, baseline, query_ids, top_k):
    """GUID -> fileName, per contatto fra i top-3 ricostruiti e la golden baseline.

    I conflitti sono il segnale d'errore: se la replica non riproducesse l'ordinamento reale, lo
    stesso GUID finirebbe allineato a nomi diversi in query diverse.
    """
    mapping, conflicts = {}, []
    for row in rows:
        query_id = query_ids.get(row["q"])
        if query_id is None or query_id not in baseline:
            continue
        rebuilt = top_documents(row["c"], top_k)
        expected = baseline[query_id]
        if len(rebuilt) != len(expected):
            conflicts.append((query_id, "lunghezza diversa", rebuilt, expected))
            continue
        for guid, name in zip(rebuilt, expected):
            if guid in mapping and mapping[guid] != name:
                conflicts.append((query_id, guid, mapping[guid], name))
            mapping[guid] = name
    return mapping, conflicts


def semantic_count(rows, query_ids, expected_docs, mapping, top_k, language_correction):
    hits, misses = 0, []
    for row in rows:
        query_id = query_ids.get(row["q"])
        if query_id is None:
            continue
        want = expected_docs[query_id]
        got = [mapping.get(g, g) for g in top_documents(row["c"], top_k, language_correction)]
        if want in got:
            hits += 1
        else:
            misses.append((query_id, want, got))
    return hits, misses


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--reference", required=True, type=Path,
                        help="artifact rag-fusion-tuning di una run in cui il gate e' PASSATO")
    parser.add_argument("--compare", type=Path, action="append", default=[],
                        help="altri artifact da valutare con la stessa replica (ripetibile)")
    parser.add_argument("--fixtures", type=Path, default=Path("infra/fixtures"))
    args = parser.parse_args()

    fixtures = json.load(io.open(args.fixtures / "rag-canonical-queries.json", encoding="utf-8"))
    top_k = fixtures.get("topK", 3)
    query_ids = {q["query"]: q["queryId"] for q in fixtures["queries"]}
    expected_docs = {q["queryId"]: q["expectedDocument"] for q in fixtures["queries"]}

    golden = json.load(io.open(args.fixtures / "rag-golden-baseline.json", encoding="utf-8"))
    baseline = {qid: [hit["document"] for hit in hits] for qid, hits in golden["baseline"].items()}

    reference = load_dump(args.reference)
    mapping, conflicts = build_document_map(reference, baseline, query_ids, top_k)

    print(f"validazione: {len(mapping)} documenti mappati, {len(conflicts)} conflitti")
    if conflicts:
        for conflict in conflicts[:8]:
            print("   conflitto:", conflict)
        print("\nLa replica non riproduce l'ordinamento reale: i numeri sotto sarebbero infondati.")
        return 1

    datasets = [(args.reference, reference)] + [(d, load_dump(d)) for d in args.compare]
    for directory, rows in datasets:
        for correction in (True, False):
            label = (f"{directory.name} "
                     f"{'(come in produzione)' if correction else 'SENZA correzione lingua'}")
            hits, misses = semantic_count(rows, query_ids, expected_docs, mapping, top_k, correction)
            print(f"\n{label}: semantico {hits}/{len(rows)}")
            for query_id, want, got in misses:
                print(f"    {query_id:<26} atteso {want:<28} -> {got}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
