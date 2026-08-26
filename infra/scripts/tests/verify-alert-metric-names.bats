#!/usr/bin/env bats
#
# #3821 — la composizione del nome Prometheus, caso per caso.
#
# PERCHE' QUESTA SUITE ESISTE
# ---------------------------
# verify-alert-metric-names.sh confronta le regole con le dichiarazioni C#, e per farlo deve
# sapere quale nome l'exporter produrra'. Una regola di composizione approssimata non rende il
# controllo inutile: lo rende DANNOSO, perche' segnala come cieche regole corrette. Estendendo la
# copertura dello script il 2026-08-26 ne sono emersi 5 in un colpo solo, tutti falsi.
#
# Su un gate bloccante un falso positivo e' peggio di un controllo assente: il primo insegna a
# ignorare il gate, il secondo almeno non mente.
#
# La logica e' replicata da PrometheusMetric.cs di OpenTelemetry.Exporter.Prometheus 1.13.1-beta.1,
# la versione pinnata in apps/api/src/Api/Api.csproj. I casi marcati "PROVATO" non vengono da una
# lettura del sorgente ma da uno scrape vero:
#   - apps/api/tests/Api.Tests/Observability/QualityMetricsPrometheusNamingTests.cs
#   - /metrics di staging dopo due richieste QA (#3817)
#
# Se un aggiornamento dell'exporter cambia la composizione, questa suite cade prima che le regole
# diventino cieche in silenzio.

setup() {
    SCRIPT="$BATS_TEST_DIRNAME/../verify-alert-metric-names.sh"
}

explain() {
    bash "$SCRIPT" --explain "$@"
}

# ── L'unita' NON viene riappesa quando il nome vi termina gia' ────────────────────────────────

@test "unit gia' in coda al nome: non viene duplicata (PROVATO su /metrics)" {
    run explain meepleai.quality.score score histogram
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_quality_score" ]
}

@test "unit gia' in coda nella forma ESPANSA: 's' vede 'seconds' e non appende" {
    run explain meepleai.shared_game_detail.render_duration_seconds s histogram
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_shared_game_detail_render_duration_seconds" ]
}

# ── Le abbreviazioni UCUM vengono espanse prima del confronto ─────────────────────────────────

@test "abbreviazione espansa e appesa quando manca: 's' -> '_seconds'" {
    run explain meepleai.gamebook.index_duration s histogram
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_gamebook_index_duration_seconds" ]
}

@test "abbreviazione dei millisecondi: 'ms' -> '_milliseconds'" {
    run explain meepleai.pdf.stage_latency ms histogram
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_pdf_stage_latency_milliseconds" ]
}

@test "abbreviazione dei byte: 'By' -> '_bytes'" {
    run explain meepleai.storage.object_size By histogram
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_storage_object_size_bytes" ]
}

# ── Annotazioni e unita' adimensionali non entrano nel nome ───────────────────────────────────

@test "annotazione UCUM fra graffe: rimossa, nessun suffisso" {
    run explain meepleai.llm.circuit_breaker_state "{state}" histogram
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_llm_circuit_breaker_state" ]
}

@test "unit adimensionale '1': nessun suffisso" {
    run explain meepleai.rag.hit_ratio 1 histogram
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_rag_hit_ratio" ]
}

@test "unit percentuale: '%' -> '_percent'" {
    run explain meepleai.quality.low_quality_rate % histogram
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_quality_low_quality_rate_percent" ]
}

# ── Unita' composte ──────────────────────────────────────────────────────────────────────────

@test "unit di rate 'a/b': diventa '_per_'" {
    run explain meepleai.embedding.throughput "requests/s" histogram
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_embedding_throughput_requests_per_seconds" ]
}

# ── Counter: il '_total' e' l'ULTIMO suffisso, dopo l'unita' ──────────────────────────────────

@test "counter con nome che finisce per .total: l'unita' si infila PRIMA di un secondo _total (PROVATO su /metrics)" {
    run explain meepleai.bgg.url.attempted_render.total attempts counter
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_bgg_url_attempted_render_total_attempts_total" ]
}

@test "counter: stesso trattamento anche quando l'unita' compare gia' nel mezzo del nome (PROVATO su /metrics)" {
    run explain meepleai.quality.low_quality_responses.total responses counter
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_quality_low_quality_responses_total_responses_total" ]
}

@test "counter senza .total nel nome: riceve comunque _total in coda" {
    run explain meepleai.auth.login_attempts attempts counter
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_auth_login_attempts_total" ]
}

@test "histogram: nessun _total, a differenza del counter" {
    run explain meepleai.auth.login_attempts attempts histogram
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_auth_login_attempts" ]
}

# ── Il sottocomando stesso ───────────────────────────────────────────────────────────────────

@test "il tipo di default e' histogram" {
    run explain meepleai.auth.login_attempts attempts
    [ "$status" -eq 0 ]
    [ "$output" = "meepleai_auth_login_attempts" ]
}

@test "un tipo non riconosciuto e' un errore, non un default silenzioso" {
    run explain meepleai.auth.login_attempts attempts gauge
    [ "$status" -eq 2 ]
    [[ "$output" == *"tipo non riconosciuto"* ]]
}

# ── Il controllo vero e proprio resta eseguibile ──────────────────────────────────────────────

@test "senza argomenti lo script controlla il repo e lo trova pulito" {
    # Se questo cade, o e' comparsa una regola cieca in repo, o il controllo si e' rotto: in
    # entrambi i casi va guardato, perche' e' la modalita' che gira nella CI (#3821).
    run bash "$SCRIPT"
    [ "$status" -eq 0 ]
    [[ "$output" == *"nessuna regola cieca"* ]]
}
