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
for path in glob.glob('apps/api/src/Api/Observability/**/*.cs', recursive=True):
    src = io.open(path, encoding='utf-8').read()
    for match in re.finditer(r'name:\s*"([^"]+)"\s*,\s*\n\s*unit:\s*"([^"]+)"', src):
        declared[match.group(1).replace('.', '_')] = (match.group(2), 'CreateCounter' in src[max(0, match.start()-120):match.start()])

# suffissi che Prometheus aggiunge DOPO l'unita': counter -> _total, histogram -> _count/_sum/_bucket
SUFFIXES = ('_total', '_count', '_sum', '_bucket', '')

# --- composizione del nome, replicata da PrometheusMetric.cs (OpenTelemetry.Exporter.Prometheus
# --- 1.13.1-beta.1, la versione pinnata in Api.csproj). Una regola semplificata qui produce FALSI
# --- POSITIVI, e su un gate bloccante sono peggio di un controllo assente: insegnano a ignorarlo.
# --- Ne sono stati misurati 5 il 2026-08-26, tutti dovuti ai tre passaggi qui sotto.
UNIT_ABBREVIATIONS = {
    'd': 'days', 'h': 'hours', 'min': 'minutes', 's': 'seconds', 'ms': 'milliseconds',
    'us': 'microseconds', 'ns': 'nanoseconds',
    'By': 'bytes', 'KiBy': 'kibibytes', 'MiBy': 'mebibytes', 'GiBy': 'gibibytes',
    'TiBy': 'tibibytes', 'KBy': 'kilobytes', 'MBy': 'megabytes', 'GBy': 'gigabytes',
    'TBy': 'terabytes', 'B': 'bytes', 'KB': 'kilobytes', 'MB': 'megabytes',
    'GB': 'gigabytes', 'TB': 'terabytes',
    'm': 'meters', 'V': 'volts', 'A': 'amperes', 'J': 'joules', 'W': 'watts', 'g': 'grams',
    'Cel': 'celsius', 'Hz': 'hertz', '1': '', '%': 'percent', '$': 'dollars',
}

def _sanitize_unit(unit):
    return re.sub(r'[^A-Za-z0-9:]+', '_', unit).strip('_')

def exposed_unit(unit):
    """GetUnit(): annotazioni via, "a/b" -> a_per_b, abbreviazioni espanse, poi sanificazione."""
    # le porzioni fra graffe sono ANNOTAZIONI e non entrano nel nome: unit="{state}" -> nessun suffisso
    unit = re.sub(r'\{[^}]*\}', '', unit).strip()
    if not unit:
        return ''
    if '/' in unit and not unit.endswith('/'):
        num, den = unit.split('/', 1)
        return _sanitize_unit(UNIT_ABBREVIATIONS.get(num, num) + '_per_' + UNIT_ABBREVIATIONS.get(den, den))
    return _sanitize_unit(UNIT_ABBREVIATIONS.get(unit, unit))

def exposed_name(base, unit, is_counter):
    """Il nome classico prodotto dall'exporter, suffisso di tipo escluso."""
    name = base
    u = exposed_unit(unit)
    # L'unita' NON viene riappesa se il nome vi termina gia': "..._duration_seconds" con unit "s"
    # resta invariato, e "meepleai_quality_score" con unit "score" pure.
    if u and not name.endswith(u):
        name = name + '_' + u
    if is_counter and not name.endswith('_total'):
        name = name + '_total'
    return name


blind = []
for path in sorted(glob.glob('infra/prometheus/alerts/*.yml')) + ['infra/prometheus-rules.yml']:
    text = io.open(path, encoding='utf-8').read()
    # le righe di commento non sono espressioni: un nome citato li' non rende cieca una regola
    body = "\n".join(l for l in text.splitlines() if not l.lstrip().startswith('#'))
    for base, (unit, is_counter) in declared.items():
        exposed = exposed_name(base, unit, is_counter)
        if exposed == base:
            continue  # dichiarato ed esposto coincidono: non c'e' nulla da sbagliare
        for suffix in SUFFIXES:
            used = base + suffix
            if re.search(r'\b' + re.escape(used) + r'\b', body) and exposed not in body:
                # Il suffisso di tipo (_count/_sum/_bucket) segue l'unita', quindi va riapplicato
                # al nome esposto; per un counter il posto del `_total` lo decide exposed_name.
                real = exposed if is_counter else exposed + suffix
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
