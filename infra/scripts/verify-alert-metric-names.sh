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
#
# PERCHE' IL CONTROLLO VA DALLE REGOLE AL CODICE, E NON VICEVERSA (#3814)
# ----------------------------------------------------------------------
# La prima versione andava dalle dichiarazioni alle regole: per ogni metrica C# cercava nel corpo il
# nome grezzo `base + suffisso` e segnalava solo se lo trovava senza l'esposto accanto. Bastava un
# TERZO nome — ne' il grezzo ne' l'esposto — per renderla muta.
#
# E' successo. fa33b2235 (#3798/#3812) riscrisse i nomi applicando «base + unit + _total» e lasciando
# cadere il `.total` gia' presente nel nome C#, producendo 13 identificatori che nessuno espone. Il
# gate era gia' in repo dal commit precedente (6a00fda75) e usciva 0 su tutti. Non ha fallito: e'
# stato aggirato, perche' guardava nella direzione in cui un nome nuovo non puo' comparire.
#
# Ora il verso e' opposto: ogni identificatore `meepleai_*` citato da una regola deve appartenere
# all'insieme dei nomi che l'exporter puo' produrre. L'insieme non cresce quando si scrive una
# regola — cresce solo quando si dichiara una metrica in C#, che e' il gesto che deve autorizzarla.
#
# Copre anche le `input_series` dei `*.test.yml`: dichiarare li' il nome sbagliato e' cio' che
# teneva `promtool test rules` verde mentre le regole erano cieche.
set -uo pipefail
cd "$(dirname "$0")/../.." || exit 1

python3 - "$@" <<'PY'
import glob, io, pathlib, re, sys

# Dichiarazioni: nome puntato -> (unita', tipo).
#
# Legge l'INTERA chiamata `Meter.Create*(...)`, non due righe adiacenti. La versione precedente
# pretendeva `unit:` sulla riga subito dopo `name:` e perdeva percio' ogni strumento osservabile,
# dove in mezzo c'e' `observeValue:`/`observeValues:` — 19 metriche invisibili, e con esse la
# differenza fra «il gate non l'ha vista» e «non esiste».
#
# Il tipo si legge dal nome del metodo, non indovinandolo da una finestra di testo intorno: e' la
# distinzione che decide il suffisso (`_total` per un counter monotono, `_count`/`_sum`/`_bucket`
# per un istogramma, nulla per un gauge).
CREATE_CALL = re.compile(
    r'Create(Counter|UpDownCounter|Histogram|ObservableCounter|ObservableUpDownCounter|ObservableGauge)'
    r'\s*(?:<[^>]*>)?\s*\((?P<args>.*?)\)\s*;',
    re.DOTALL)

KIND_OF = {
    'Counter': 'counter',
    'ObservableCounter': 'counter',
    'UpDownCounter': 'gauge',
    'ObservableUpDownCounter': 'gauge',
    'ObservableGauge': 'gauge',
    'Histogram': 'histogram',
}

declared = {}
kinds = {}
for path in glob.glob('apps/api/src/Api/Observability/**/*.cs', recursive=True):
    src = io.open(path, encoding='utf-8').read()
    for call in CREATE_CALL.finditer(src):
        args = call.group('args')
        name = re.search(r'name:\s*"([^"]+)"', args)
        if not name:
            continue
        unit = re.search(r'unit:\s*"([^"]+)"', args)
        base = name.group(1).replace('.', '_')
        kind = KIND_OF[call.group(1)]
        kinds[base] = kind
        declared[base] = (unit.group(1) if unit else '', kind == 'counter')

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


# --- sottocomando diagnostico -------------------------------------------------------------
# `verify-alert-metric-names.sh --explain <nome.puntato> <unit> [counter|histogram]` stampa il
# nome che l'exporter produrra'. Serve a due cose: rispondere alla domanda «come si chiamera'
# questa metrica?» prima di scrivere la regola, e dare ai test un aggancio sulla funzione pura
# (infra/scripts/tests/verify-alert-metric-names.bats) senza dover allestire un finto repo.
if len(sys.argv) >= 4 and sys.argv[1] == '--explain':
    _name, _unit = sys.argv[2], sys.argv[3]
    _kind = sys.argv[4] if len(sys.argv) > 4 else 'histogram'
    if _kind not in ('counter', 'histogram'):
        print(f"tipo non riconosciuto: {_kind} (usa 'counter' o 'histogram')", file=sys.stderr)
        sys.exit(2)
    print(exposed_name(_name.replace('.', '_'), _unit, _kind == 'counter'))
    sys.exit(0)


# ---------------------------------------------------------------------------------------------
# Autocontrollo di copertura. Se lo scraper non vede una dichiarazione, «metrica non vista» e
# «metrica inesistente» diventano indistinguibili — ed e' l'errore che in una misura precedente
# ha prodotto 16 identificatori «ignoti», quattro dei quali erano difetti veri.
declared_names = set()
for _path in glob.glob('apps/api/src/Api/Observability/**/*.cs', recursive=True):
    for _m in re.finditer(r'name:\s*"(meepleai\.[^"]+)"', io.open(_path, encoding='utf-8').read()):
        declared_names.add(_m.group(1).replace('.', '_'))
_missing = sorted(n for n in declared_names if n not in declared)
if _missing:
    print('SCRAPER INCOMPLETO: dichiarazioni che il gate non ha classificato\n')
    for n in _missing:
        print('  ' + n)
    print('\nSenza classificazione il controllo non distingue un nome sbagliato da una metrica che')
    print('non esiste. Correggi l\'estrazione prima di fidarti dell\'esito.')
    sys.exit(2)

# ---------------------------------------------------------------------------------------------
# Nomi ammessi: quelli che l'exporter puo' davvero produrre.
allowed = set()
for _base, (_unit, _ctr) in declared.items():
    _e = exposed_name(_base, _unit, _ctr)
    _kind = kinds.get(_base, 'counter' if _ctr else 'histogram')
    if _kind == 'histogram':
        # un istogramma espone SOLO i suffissi di tipo, mai il nome nudo: una regola che usa il
        # nome nudo interroga una serie che non esiste
        for _suf in ('_count', '_sum', '_bucket'):
            allowed.add(_e + _suf)
    else:
        # counter (col suo `_total`) e gauge (nome nudo)
        allowed.add(_e)

# ---------------------------------------------------------------------------------------------
# Estrazione via parser YAML, non per righe: le espressioni sono blocchi multi-riga (`expr: |`),
# e un estrattore che legge le sole righe `expr:` vede una frazione della superficie (misurato:
# 16 identificatori su 47).
try:
    import yaml
except ImportError:
    print('PyYAML non disponibile: `pip install pyyaml` (serve per leggere le regole)')
    sys.exit(2)

LABEL_ARGS = re.compile(r'\b(?:by|without|on|ignoring|group_left|group_right)\s*\([^)]*\)')

def metric_ids(expr):
    """Identificatori meepleai_* in un'espressione PromQL, esclusi i nomi di label."""
    e = re.sub(r'"[^"]*"', ' ', str(expr))
    e = re.sub(r"'[^']*'", ' ', e)
    e = LABEL_ARGS.sub(' ', e)
    e = re.sub(r'\{[^}]*\}', ' ', e)
    return set(re.findall(r'\b(meepleai_[A-Za-z0-9_:]+)', e))

rule_files = [f for f in sorted(glob.glob('infra/prometheus/alerts/*.yml'))
              if not f.endswith('.test.yml')] + ['infra/prometheus-rules.yml']
test_files = sorted(glob.glob('infra/prometheus/alerts/*.test.yml'))

cited = {}
recorded = set()
n_expr = 0

def _note(ident, path, ctx):
    cited.setdefault(ident, {}).setdefault(pathlib.Path(path).name, set()).add(ctx)

for _path in rule_files:
    _doc = yaml.safe_load(io.open(_path, encoding='utf-8')) or {}
    for _g in _doc.get('groups') or []:
        for _r in _g.get('rules') or []:
            if _r.get('record'):
                recorded.add(_r['record'])
    for _g in _doc.get('groups') or []:
        for _r in _g.get('rules') or []:
            _expr = _r.get('expr')
            if not _expr:
                continue
            n_expr += 1
            _label = _r.get('alert') or _r.get('record') or '?'
            for _i in metric_ids(_expr):
                _note(_i, _path, _label)

# Le `input_series` di un test DICHIARANO il nome: se e' sbagliato il test resta verde e la regola
# resta cieca. E' il punto in cui il difetto si nascondeva.
for _path in test_files:
    _doc = yaml.safe_load(io.open(_path, encoding='utf-8')) or {}
    for _t in _doc.get('tests') or []:
        for _s in _t.get('input_series') or []:
            for _i in metric_ids(_s.get('series', '')):
                _note(_i, _path, 'input_series')

# gli output delle recording rule esistono a runtime pur non essendo dichiarati in C#
allowed |= recorded

_baseline_path = 'infra/prometheus/alerts/.blind-metric-baseline'
baseline = set()
if pathlib.Path(_baseline_path).exists():
    for _line in io.open(_baseline_path, encoding='utf-8'):
        _line = _line.split('#', 1)[0].strip()
        if _line:
            baseline.add(_line)

blind = sorted((i, s) for i, s in cited.items() if i not in allowed and i not in baseline)

# Una baseline puo' solo restringersi: una voce che non compare piu' nelle regole e' debito gia'
# pagato, e lasciarla nel file la trasformerebbe in un parcheggio dove nascondere il prossimo caso.
stale = sorted(n for n in baseline if n not in cited or n in allowed)

# ---------------------------------------------------------------------------------------------
# Un gate che non ha esaminato nulla e' indistinguibile da un gate verde.
print('dichiarazioni C# con unit: %d  |  nomi ammessi: %d (di cui %d da record:)'
      % (len(declared), len(allowed), len(recorded)))
print('file: %d di regole + %d di test  |  espressioni: %d  |  identificatori citati: %d'
      % (len(rule_files), len(test_files), n_expr, len(cited)))
for _label, _value in (('dichiarazioni', len(declared)), ('espressioni', n_expr),
                       ('identificatori', len(cited))):
    if _value == 0:
        print('\nNIENTE DA ESAMINARE: %s = 0. Un verde cosi non significa nulla.' % _label)
        sys.exit(2)

if stale:
    print('\nBASELINE OBSOLETA: voci che le regole non citano piu\' (o che ora esistono)\n')
    for _n in stale:
        print('  ' + _n)
    print('\nToglile da %s: una baseline che non si restringe smette di misurare qualcosa.'
          % _baseline_path)
    sys.exit(1)

if not blind:
    if baseline:
        print('\nOK — ogni identificatore citato esiste, salvo %d in baseline dichiarata'
              % len(baseline))
    else:
        print('\nOK — ogni identificatore citato esiste col nome che l\'exporter produce')
    sys.exit(0)

print('\nIDENTIFICATORI CHE NESSUNO ESPONE: %d\n' % len(blind))
for _ident, _sites in blind:
    print('  ' + _ident)
    for _fname, _ctxs in sorted(_sites.items()):
        print('      %s: %s' % (_fname, ', '.join(sorted(_ctxs))))
    _stem = _ident.rsplit('_total', 1)[0]
    _near = sorted(a for a in allowed if a.startswith(_stem[:max(18, len(_stem) - 12)]))
    if _near:
        print('      forse: ' + _near[0])
print('\nUn alert che interroga una serie assente e\' caricato e non puo\' scattare, che e\'')
print('indistinguibile da un sistema sano. Il nome giusto lo da\':')
print('  bash infra/scripts/verify-alert-metric-names.sh --explain <nome.puntato> <unit> counter|histogram')
sys.exit(1)

PY
