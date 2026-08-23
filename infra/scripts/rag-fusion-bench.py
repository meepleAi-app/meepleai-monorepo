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

USO — STADIO PER-GIOCO (#3768)
------------------------------
Il modo `--per-game` valuta lo stadio a MONTE: quali candidati ogni gioco manda alla fusione, e
quindi anche quelli che scarta. Consuma `per-game.log`, cioe' le righe `[RAG-TUNE-GAME]`:

    docker logs meepleai-api --since <T> 2>&1 | grep -F '[RAG-TUNE-GAME]' > <dir>/per-game.log
    python infra/scripts/rag-fusion-bench.py --per-game <dir> \
           [--doc-names names.json] [--game-titles titles.json]

Le due mappe opzionali si producono con una query sola ciascuna (su staging, dal container
postgres):

    names.json   SELECT json_object_agg("Id", "FileName") FROM pdf_documents;
    titles.json  SELECT json_object_agg("Id", "Name") FROM shared_games;   -- o la tabella dei giochi

Con `--game-titles` lo script valuta anche la VARIANTE che esclude i token del nome del gioco dai
termini di heading-match, e stampa il conteggio semantico prima e dopo.

SE `FuseGlobally` O `HybridFusionCore.Fuse` CAMBIANO, QUESTO FILE VA CAMBIATO CON ESSI. Entrambe le
validazioni lo scoprono — la globale sui top-3, la per-gioco su OGNI punteggio ricostruito — ma lo
scoprono solo quando qualcuno le esegue.
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


# ----------------------------------------------------------------------------------------------
# STADIO PER-GIOCO (#3768)
#
# Il dump [RAG-TUNE] mostra i candidati che ARRIVANO alla fusione globale, mai quelli che lo stadio
# per-gioco ha RIFIUTATO. Su staging, per `catan-setup-it`, il chunk con le regole di setup e' rango
# 1 del braccio vettoriale dentro Catan (cosine 0.80544) e non arriva: al suo posto arriva il
# colophon `catan.com` (0.77997, rango 14), perche' la sua heading contiene il nome del gioco e
# `headingBoost` vale 0.15 contro un rrfSum che satura a 1/61 = 0.0164.
#
# [RAG-TUNE-GAME] (#3768) emette la scomposizione del punteggio di ogni candidato fuso, e questa
# sezione la rimette nella formula di HybridFusionCore.Fuse.
#
# LA VALIDAZIONE E' PIU' FORTE DI QUELLA GLOBALE: il payload porta il punteggio REALE (`s`), quindi
# la replica si confronta candidato per candidato — migliaia di controlli indipendenti — invece che
# sui soli top-3. Se un singolo punteggio non torna, lo script si ferma.
# ----------------------------------------------------------------------------------------------

# Devono corrispondere a FusionSignals / HybridFusionCore.
RRF_K = 60
HEADING_MATCH_BOOST = 0.15
SCORE_TOLERANCE = 2e-6


def load_per_game(directory: Path):
    """Le righe [RAG-TUNE-GAME] di per-game.log, una per (query, gioco)."""
    path = directory / "per-game.log"
    rows = []
    for line in io.open(path, encoding="utf-8"):
        marker = line.find("[RAG-TUNE-GAME] {")
        if marker < 0:
            continue
        payload, _ = DECODER.raw_decode(line[marker + len("[RAG-TUNE-GAME] "):])
        rows.append(payload)
    if not rows:
        raise SystemExit(f"{path}: nessuna riga [RAG-TUNE-GAME]")
    return rows


def per_game_score(candidate, heading_boost):
    """HybridScore = rrfSum * (1-legend) * (1-numberNoise) + roleBoost + headingBoost."""
    vector_rrf = VECTOR_WEIGHT / (RRF_K + candidate["vr"]) if candidate.get("vr") else 0.0
    keyword_rrf = KEYWORD_WEIGHT / (RRF_K + candidate["kr"]) if candidate.get("kr") else 0.0
    rrf_sum = vector_rrf + keyword_rrf
    return (rrf_sum
            * (1.0 - candidate.get("lg", 0.0))
            * (1.0 - candidate.get("nn", 0.0))
            + candidate.get("rb", 0.0)
            + heading_boost)


def fusion_key(candidate):
    """La chiave di fusione, `{PdfDocumentId}_{ChunkIndex}`: e' anche il tiebreak ordinale."""
    return f"{candidate['d']}_{candidate['i']}"


def validate_per_game(rows):
    """Ricostruisce ogni punteggio dai fattori emessi e lo confronta con quello reale."""
    checked, mismatches = 0, []
    for row in rows:
        for candidate in row["c"]:
            checked += 1
            rebuilt = per_game_score(candidate, candidate.get("hb", 0.0))
            if abs(rebuilt - candidate["s"]) > SCORE_TOLERANCE:
                mismatches.append((row.get("g"), candidate["i"], candidate["s"], rebuilt))
    return checked, mismatches


def title_tokens(title):
    """Stessa normalizzazione dei termini di heading: minuscolo, spezzato, lunghezza >= 3."""
    if not title:
        return set()
    out, token = set(), []
    for ch in title.lower():
        if ch.isalnum():
            token.append(ch)
        else:
            if len(token) >= 3:
                out.add("".join(token))
            token = []
    if len(token) >= 3:
        out.add("".join(token))
    return out


def heading_boost(terms, heading, drop=frozenset()):
    """Replica di FusionSignals.ComputeHeadingMatchBoost, con esclusione opzionale di termini.

    `drop` e' la variante in valutazione: i token del NOME DEL GIOCO. Dentro un gioco gia' filtrato
    per GameId quel nome ha IDF nullo, e le heading che lo contengono sono proprio quelle senza
    contenuto (colophon, pie' di pagina, copertina).
    """
    if not terms or not heading:
        return 0.0
    lowered = heading.lower()
    for term in terms:
        if len(term) >= 3 and term not in drop and term in lowered:
            return HEADING_MATCH_BOOST
    return 0.0


def per_game_selection(row, per_game_limit, drop=frozenset()):
    """I candidati che questo gioco manda alla fusione globale, nella forma che `fuse` consuma."""
    terms = row.get("t") or []
    scored = [
        (per_game_score(c, heading_boost(terms, c.get("h"), drop)), fusion_key(c), c)
        for c in row["c"]
    ]
    # HybridFusionCore ordina per punteggio desc con tiebreak ordinale sulla chiave; il successivo
    # OrderByDescending di HybridSearchService e' stabile, quindi il tiebreak sopravvive.
    scored.sort(key=lambda t: (-t[0], t[1]))
    return [
        {"d": c["d"], "i": c["i"], "g": row["g"], "v": c.get("v"), "k": c.get("k"), "l": c.get("l")}
        for _, _, c in scored[:per_game_limit]
    ]


def aggregate_from_per_game(rows, per_game_limit, drop_by_game=None):
    """Ricostruisce, per query, l'aggregato che arriverebbe a FuseGlobally."""
    drop_by_game = drop_by_game or {}
    by_query = defaultdict(list)
    for row in rows:
        drop = drop_by_game.get(row["g"], frozenset())
        by_query[row["q"]].extend(per_game_selection(row, per_game_limit, drop))
    return [{"q": query, "n": len(candidates), "c": candidates} for query, candidates in by_query.items()]


def run_per_game(args, query_ids, expected_docs, top_k):
    rows = load_per_game(args.per_game)
    games = {row["g"] for row in rows}
    print(f"per-gioco: {len(rows)} righe, {len(games)} giochi, "
          f"{len({row['q'] for row in rows})} query")

    checked, mismatches = validate_per_game(rows)
    print(f"validazione: {checked} punteggi ricostruiti, divergenze: {len(mismatches)}")
    if mismatches:
        for game, chunk, real, rebuilt in mismatches[:8]:
            print(f"   gioco {game} chunk {chunk}: reale {real:.8f} != ricostruito {rebuilt:.8f}")
        print("\nLa replica non riproduce il punteggio per-gioco: i numeri sotto sarebbero infondati.")
        return 1

    names = {}
    if args.doc_names:
        names = json.load(io.open(args.doc_names, encoding="utf-8"))

    drop_by_game = {}
    if args.game_titles:
        titles = json.load(io.open(args.game_titles, encoding="utf-8"))
        drop_by_game = {game: title_tokens(title) for game, title in titles.items()}
        covered = len(games & set(drop_by_game))
        print(f"variante: esclusione dei token del titolo su {covered}/{len(games)} giochi")
        if covered < len(games):
            print(f"   ATTENZIONE: giochi senza titolo, lasciati invariati: {len(games) - covered}")

    variants = [("com'e' oggi", {})]
    if drop_by_game:
        variants.append(("senza il nome del gioco nei termini di heading", drop_by_game))

    for label, drop in variants:
        aggregate = aggregate_from_per_game(rows, args.per_game_limit, drop)
        hits, misses = semantic_count(aggregate, query_ids, expected_docs, names, top_k,
                                      language_correction=True)
        print(f"\n{label}: semantico {hits}/{len(aggregate)}")
        for query_id, want, got in misses:
            print(f"    {query_id:<26} atteso {want:<28} -> {got}")

    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--reference", type=Path,
                        help="artifact rag-fusion-tuning di una run in cui il gate e' PASSATO")
    parser.add_argument("--compare", type=Path, action="append", default=[],
                        help="altri artifact da valutare con la stessa replica (ripetibile)")
    parser.add_argument("--per-game", type=Path,
                        help="directory con per-game.log: valuta lo stadio PER-GIOCO (#3768)")
    parser.add_argument("--per-game-limit", type=int, default=3,
                        help="candidati che ogni gioco manda alla fusione globale "
                             "(MultiGameHybridSearchService: min(max(limit,1),50); default 3)")
    parser.add_argument("--doc-names", type=Path,
                        help="JSON pdfDocumentId -> fileName. Senza, i documenti restano GUID")
    parser.add_argument("--game-titles", type=Path,
                        help="JSON gameId -> titolo: valuta la variante che esclude i token del "
                             "nome del gioco dai termini di heading-match")
    parser.add_argument("--fixtures", type=Path, default=Path("infra/fixtures"))
    args = parser.parse_args()

    if not args.reference and not args.per_game:
        parser.error("serve --reference (stadio globale) oppure --per-game (stadio per-gioco)")

    fixtures = json.load(io.open(args.fixtures / "rag-canonical-queries.json", encoding="utf-8"))
    top_k = fixtures.get("topK", 3)
    query_ids = {q["query"]: q["queryId"] for q in fixtures["queries"]}
    expected_docs = {q["queryId"]: q["expectedDocument"] for q in fixtures["queries"]}

    if args.per_game:
        return run_per_game(args, query_ids, expected_docs, top_k)

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
