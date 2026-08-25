#!/usr/bin/env bash
# Verifica che ogni metrica citata dalle regole Prometheus esista con il nome che l'exporter
# produce davvero (#3798).
#
# IL DIFETTO CHE PREVIENE
# -----------------------
# L'exporter Prometheus di OpenTelemetry INFILA L'UNITA' NEL NOME:
#
#     name: "meepleai.bgg.url.attempted_render.total", unit: "attempts"
#     ->   meepleai_bgg_url_attempted_render_total_attempts_total
#
# Una regola scritta copiando il nome dal codice C# e' quindi CARICATA MA CIECA: /api/v1/rules la
# conta, /api/v1/alerts mostra zero alert, e non puo' mai scattare. Un alert che non scatta e'
# indistinguibile da un sistema sano, quindi ne' «regole caricate» ne' «zero alert attivi» se ne
# accorgono.
#
# Al 2026-08-25 erano cieche 22 espressioni in 10 file, incluso l'SLO=0 P1 del ban ToS BGG. Il
# difetto si era propagato per copia: #3082 documento' «unit is NOT appended» citando come prova
# una metrica MAI ESPOSTA, e #3112 / #3248 copiarono quell'assunzione dal commento.
#
# PERCHE' I TEST PROMTOOL NON BASTANO
# -----------------------------------
# Le `input_series` di un test promtool DICHIARANO il nome: validano la logica della regola e mai
# l'esistenza della serie. Le regole di #3793 superarono 4 scenari verdi mentre erano cieche.
#
# Questo script confronta invece le regole con le DICHIARAZIONI C#, quindi funziona anche per i
# counter mai incrementati — che su /metrics non compaiono affatto.
set -uo pipefail
cd "$(dirname "$0")/../.." || exit 1

python3 - "$@" <<'PY'
import glob, io, pathlib, re, sys

# (nome puntato -> unita') da ogni CreateCounter/CreateHistogram con `unit:` dichiarata
declared = {}
for path in glob.glob('apps/api/src/Api/Observability/Metrics/*.cs'):
    src = io.open(path, encoding='utf-8').read()
    for match in re.finditer(r'name:\s*"([^"]+)"\s*,\s*\n\s*unit:\s*"([^"]+)"', src):
        declared[match.group(1).replace('.', '_')] = (match.group(2), 'CreateCounter' in src[max(0, match.start()-120):match.start()])

# suffissi che Prometheus aggiunge DOPO l'unita': counter -> _total, histogram -> _count/_sum/_bucket
SUFFIXES = ('_total', '_count', '_sum', '_bucket', '')

blind = []
for path in sorted(glob.glob('infra/prometheus/alerts/*.yml')):
    text = io.open(path, encoding='utf-8').read()
    # le righe di commento non sono espressioni: un nome citato li' non rende cieca una regola
    body = "\n".join(l for l in text.splitlines() if not l.lstrip().startswith('#'))
    for base, (unit, is_counter) in declared.items():
        with_unit = f"{base}_{unit}"
        for suffix in SUFFIXES:
            used = base + suffix
            if re.search(r'\b' + re.escape(used) + r'\b', body) and with_unit not in body:
                # Un counter riceve SEMPRE `_total` finale, anche quando il nome dichiarato
                # finisce gia' per `.total`: meepleai.bgg.url.attempted_render.total e' esposto
                # come ..._render_total_attempts_total. Suggerire il nome senza quel suffisso
                # manderebbe chi legge a scrivere un SECONDO nome sbagliato.
                real = with_unit + ('_total' if is_counter else suffix)
                blind.append((pathlib.Path(path).name, used, real, unit))
                break

if not blind:
    print(f"OK — nessuna regola cieca ({len(declared)} metriche con unit dichiarata)")
    sys.exit(0)

print(f"REGOLE CIECHE: {len(blind)}\n")
for filename, used, real, unit in blind:
    print(f"  {filename}")
    print(f"    usa   : {used}")
    print(f"    reale : {real}   (unit='{unit}')")
print("\nUna regola con il nome sbagliato e' caricata ma non puo' scattare.")
print("Conferma sul campo, quando la metrica e' stata incrementata almeno una volta:")
print("  docker exec meepleai-api curl -s localhost:8080/metrics | grep <prefisso>")
sys.exit(1)
PY
