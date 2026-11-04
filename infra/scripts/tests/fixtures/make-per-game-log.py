#!/usr/bin/env python3
"""Genera un per-game.log sintetico per i test di rag-fusion-bench.py --per-game (#3768).

Riproduce lo scenario misurato su staging per `catan-setup-it`:

  - dentro Catan il chunk con le regole di setup e' rango 1 del braccio vettoriale (cosine 0.80544)
    e la sua heading ("I") non contiene termini della query;
  - il colophon `catan.com` e' rango 14 (cosine 0.77997) ma la sua heading contiene il nome del
    gioco, quindi prende headingBoost = 0.15 e vince l'arena per-gioco;
  - tre giochi concorrenti hanno un candidato con cosine FRA i due valori.

Conseguenza: con perGameLimit = 1, oggi Catan manda il colophon a 0.77997 e finisce quarto nella
fusione globale (fuori dal top-3); escludendo il nome del gioco dai termini di heading manda le
regole a 0.80544 e rientra. E' l'inversione che il banco deve saper misurare.

I punteggi `s` sono calcolati QUI con la stessa formula che il banco ricostruisce: e' cio' che
rende significativo il test sulla guardia di validazione, che corrompe `s` e si aspetta l'arresto.
"""
import io
import json
import pathlib
import sys

RRF_K = 60
VECTOR_WEIGHT = 0.7
KEYWORD_WEIGHT = 0.3
HEADING_MATCH_BOOST = 0.15

QUERY = "Come si prepara il tabellone in Catan?"

CATAN_PDF = "2c3c74de-7dcb-458b-8ceb-857f9d5e5f5d"
CATAN_GAME = "g-catan"


def heading_terms(query):
    seen, terms, token = set(), [], []
    for ch in query + " ":
        if ch.isalnum():
            token.append(ch.lower())
            continue
        term = "".join(token)
        if len(term) >= 3 and term not in seen:
            seen.add(term)
            terms.append(term)
        token = []
    return terms


TERMS = heading_terms(QUERY)


def candidate(pdf_id, chunk_index, vector_rank, cosine, heading):
    boost = HEADING_MATCH_BOOST if any(t in (heading or "").lower() for t in TERMS) else 0.0
    rrf_sum = VECTOR_WEIGHT / (RRF_K + vector_rank)
    return {
        "d": pdf_id, "i": chunk_index, "vr": vector_rank, "v": cosine,
        "lg": 0.0, "nn": 0.0, "rb": 0.0, "hb": boost, "h": heading, "l": "en",
        "s": rrf_sum + boost,
    }


def main():
    out = pathlib.Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)

    names = {CATAN_PDF: "catan_en_rulebook.pdf"}
    titles = {CATAN_GAME: "CATAN"}

    rows = [{
        "q": QUERY, "g": CATAN_GAME, "f": "english", "t": TERMS, "n": 3,
        "c": [
            candidate(CATAN_PDF, 388, 1, 0.80544, "I"),
            candidate(CATAN_PDF, 387, 2, 0.80544, "I"),
            candidate(CATAN_PDF, 410, 14, 0.77997, "catan.com"),
        ],
    }]

    # Tre concorrenti con cosine STRETTAMENTE fra 0.77997 e 0.80544: bastano a spingere Catan fuori
    # dal top-3 globale quando manda il colophon, e non bastano piu' quando manda le regole.
    for index, cosine in enumerate((0.79694, 0.79367, 0.79162), start=1):
        pdf_id = f"aaaaaaaa-0000-0000-0000-00000000000{index}"
        game_id = f"g-other-{index}"
        names[pdf_id] = f"other-{index}_rulebook.pdf"
        titles[game_id] = f"Other {index}"
        rows.append({
            "q": QUERY, "g": game_id, "f": "english", "t": TERMS, "n": 1,
            "c": [candidate(pdf_id, 5, 1, cosine, "Scoring")],
        })

    with io.open(out / "per-game.log", "w", encoding="utf-8") as handle:
        for row in rows:
            handle.write("[2026-08-23 18:25:19.659 +00:00] [DBG] [] [RAG-TUNE-GAME] "
                         + json.dumps(row, ensure_ascii=False) + "\n")

    json.dump(names, io.open(out / "names.json", "w", encoding="utf-8"))
    json.dump(titles, io.open(out / "titles.json", "w", encoding="utf-8"))


if __name__ == "__main__":
    main()
