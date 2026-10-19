#!/usr/bin/env bats
# La ripartizione in shard dei test di integrazione — #3742.
#
# I test Category=Integration sono divisi fra piu' job da un filtro per nome: ogni shard seleziona i
# test il cui nome completo contiene uno dei suoi token, e `Core` prende cio' che resta escludendo
# quei token uno per uno. Niente tiene insieme le due meta' della regola tranne chi scrive la
# matrice: togliere un token da uno shard lasciandolo escluso da `Core` fa sparire quei test da ogni
# shard, e nessun job cambia colore — sono test che non girano, non test che falliscono.
#
# Questi test leggono le matrici dai due workflow e ne verificano la forma. Costano secondi; la
# stessa svista, scoperta in dev-async, costa un'ora per run e si vede solo cercando un test nel
# .trx.
#
# Un token puo' contenerne un altro (#4484): `Integration.Authentication` seleziona un sottoinsieme
# di cio' che seleziona `Authentication`, perche' ogni nome che contiene il primo contiene anche il
# secondo. Uno shard che prende quel sottoinsieme RAFFINA la ripartizione senza cambiare l'unione,
# e `Core` resta coperto escludendo il token piu' largo. Per questo i confronti fra insiemi di token
# si fanno sui token massimali (vedi `maximal`), non sull'elenco grezzo.
#
# Il confronto per sottostringa vale anche nel verso opposto (#4488): un test il cui nome contiene i
# token di due shard lo selezionano tutti e due, e il nome comprende il METODO. Un'esclusione decide
# quale dei due lo tiene, e ogni esclusione e' una precedenza: lo shard che esclude cede il test a
# quello che seleziona. Le regole qui sotto valgono per ogni coppia di shard, non per coppie nominate:
# niente doppi, nessun token annullato dal proprio shard, e nessun ciclo di precedenze, che farebbe
# sparire da ogni shard i nomi che contengono i token del ciclo.

setup() {
    ROOT="$BATS_TEST_DIRNAME/../../.."
    DEV_ASYNC="$ROOT/.github/workflows/dev-async.yml"
    CI="$ROOT/.github/workflows/ci.yml"
    BASELINE="$ROOT/infra/fixtures/dev-async-shard-baseline.json"
}

# "nome<TAB>filtro" per ogni voce della matrice. Nei due workflow `filter_extra` compare solo li',
# e segue sempre la riga `- name: "..."` del suo shard.
shards() {
    awk '
      /^[[:space:]]*- name:[[:space:]]*"/        { name = $0; sub(/^[^"]*"/, "", name); sub(/".*$/, "", name); next }
      /^[[:space:]]*filter_extra:[[:space:]]*"/  { f = $0; sub(/^[^"]*"/, "", f); sub(/"[^"]*$/, "", f); print name "\t" f }
    ' "$1"
}

filter_of() { shards "$1" | awk -F'\t' -v n="$2" '$1 == n { print $2 }'; }
names_of() { shards "$1" | cut -f1 | LC_ALL=C sort; }

# I token che un filtro SELEZIONA (FullyQualifiedName~X) e quelli che ESCLUDE (FullyQualifiedName!~X).
# Il punto fa parte del token: senza, `Integration.Authentication` si leggerebbe `Integration`.
selected() {
    printf '%s\n' "$1" | sed 's/FullyQualifiedName!~/ESCLUSO:/g' \
        | grep -oE 'FullyQualifiedName~[A-Za-z0-9_.]+' | sed 's/^FullyQualifiedName~//' | LC_ALL=C sort -u
}
excluded() {
    printf '%s\n' "$1" | grep -oE 'FullyQualifiedName!~[A-Za-z0-9_.]+' | sed 's/^FullyQualifiedName!~//' | LC_ALL=C sort -u
}

# L'unione dei token selezionati dagli shard diversi da Core.
selected_outside_core() {
    shards "$1" | awk -F'\t' '$1 != "Core" { print $2 }' | while IFS= read -r f; do selected "$f"; done | LC_ALL=C sort -u
}

# Da un elenco di token (uno per riga, su stdin), quelli che non contengono un altro token
# dell'elenco. Un token che ne contiene un altro seleziona un sottoinsieme dei suoi test, quindi non
# aggiunge niente all'unione.
maximal() {
    awk '{ t[NR] = $0 } END {
        for (i = 1; i <= NR; i++) {
            keep = 1
            for (j = 1; j <= NR; j++) if (i != j && t[j] != t[i] && index(t[i], t[j]) > 0) keep = 0
            if (keep) print t[i]
        }
    }' | LC_ALL=C sort -u
}

# "shard<TAB>sel|exc<TAB>token" per ogni token degli shard diversi da Core.
token_table() {
    shards "$1" | while IFS=$'\t' read -r name filter; do
        [ "$name" = "Core" ] && continue
        for t in $(selected "$filter"); do printf '%s\tsel\t%s\n' "$name" "$t"; done
        for t in $(excluded "$filter"); do printf '%s\texc\t%s\n' "$name" "$t"; done
    done
}

# Le coppie di token selezionati da due shard diversi che possono stare nello stesso nome. Un nome che
# contiene `a` e `b` contiene anche ogni token contenuto in uno dei due: se uno dei due shard esclude
# uno di quei token, il nome lo tiene al piu' l'altro. Se nessuno lo fa, il nome che li concatena lo
# selezionano entrambi. Una riga per coppia, vuoto se non ce ne sono.
double_selections() {
    token_table "$1" | awk -F'\t' '
      function blocks(shard, a, b,   k, e, i) {
          k = split(substr(ex[shard], 2), e, "\t")
          for (i = 1; i <= k; i++) if (index(a, e[i]) > 0 || index(b, e[i]) > 0) return 1
          return 0
      }
      $2 == "sel" { n++; sn[n] = $1; st[n] = $3 }
      $2 == "exc" { ex[$1] = ex[$1] "\t" $3 }
      END {
          for (i = 1; i <= n; i++) for (j = i + 1; j <= n; j++) {
              if (sn[i] == sn[j]) continue
              if (blocks(sn[i], st[i], st[j]) || blocks(sn[j], st[i], st[j])) continue
              print sn[i] "/" sn[j] ": " st[i] " + " st[j]
          }
      }'
}

# I token esclusi da uno shard che nessun ALTRO shard seleziona. Lo shard stesso non conta: un
# token che seleziona ed esclude non copre niente.
uncovered_exclusions() {
    token_table "$1" | awk -F'\t' '
      $2 == "sel" { by[$3] = by[$3] " " $1 }
      $2 == "exc" { m++; en[m] = $1; et[m] = $3 }
      END {
          for (j = 1; j <= m; j++) {
              ok = 0
              k = split(by[et[j]], s, " ")
              for (i = 1; i <= k; i++) if (s[i] != en[j]) ok = 1
              if (!ok) print en[j] " esclude " et[j] ", che nessun altro shard seleziona"
          }
      }'
}

# I token che il loro stesso shard annulla: lo shard esclude un token contenuto in essi, quindi ogni
# nome che li contiene e' escluso, e il token non seleziona niente.
dead_tokens() {
    token_table "$1" | awk -F'\t' '
      $2 == "sel" { n++; sn[n] = $1; st[n] = $3 }
      $2 == "exc" { m++; en[m] = $1; et[m] = $3 }
      END {
          for (i = 1; i <= n; i++) for (j = 1; j <= m; j++)
              if (sn[i] == en[j] && index(st[i], et[j]) > 0) print sn[i] ": " st[i] " annullato da !~" et[j]
      }'
}

# Gli shard che stanno in un ciclo di precedenze. A cede a B se A esclude un token che B seleziona.
# Si tolgono uno alla volta gli shard che non cedono a nessuno degli shard rimasti; quelli che non si
# riescono a togliere stanno in un ciclo, o cedono a uno shard che ci sta.
yield_cycle() {
    token_table "$1" | awk -F'\t' '
      { node[$1] = 1 }
      $2 == "sel" { n++; sn[n] = $1; st[n] = $3 }
      $2 == "exc" { m++; en[m] = $1; et[m] = $3 }
      END {
          for (j = 1; j <= m; j++) for (i = 1; i <= n; i++)
              if (et[j] == st[i] && en[j] != sn[i]) edge[en[j], sn[i]] = 1
          do {
              removed = 0
              split("", gone)
              for (a in node) {
                  out = 0
                  for (b in node) if ((a, b) in edge) out = 1
                  if (!out) gone[a] = 1
              }
              for (a in gone) { delete node[a]; removed = 1 }
          } while (removed)
          for (a in node) print a
      }' | LC_ALL=C sort
}

# Una matrice di prova in un file, per i controlli delle regole: righe "nome|filtro".
fake_matrix() {
    local f="$BATS_TEST_TMPDIR/matrix.yml"
    : > "$f"
    for row in "$@"; do
        printf '          - name: "%s"\n            filter_extra: "%s"\n' "${row%%|*}" "${row#*|}" >> "$f"
    done
    printf '%s\n' "$f"
}

# ─── la lettura delle matrici e' viva ────────────────────────────────────────────────────────────

@test "le due matrici si leggono: piu' shard, e uno si chiama Core" {
    # Controprova per tutti i test sotto: se il formato del YAML cambiasse e la lettura tornasse
    # vuota, i confronti fra insiemi vuoti passerebbero senza provare niente.
    for wf in "$DEV_ASYNC" "$CI"; do
        run names_of "$wf"
        [ "$status" -eq 0 ]
        [ "$(printf '%s\n' "$output" | wc -l)" -ge 3 ]
        [[ $'\n'"$output"$'\n' == *$'\n'"Core"$'\n'* ]]
        [ -n "$(selected_outside_core "$wf")" ]
    done
}

@test "maximal toglie solo i token che ne contengono un altro" {
    # Controprova per i confronti sotto: un `maximal` che restituisse niente renderebbe uguali due
    # insiemi qualsiasi; uno che restituisse tutto rifiuterebbe ogni raffinamento.
    run maximal <<< $'Authentication\nIntegration.Authentication\nKnowledgeBase\nKnowledgeBase'
    [ "$status" -eq 0 ]
    [ "$output" = $'Authentication\nKnowledgeBase' ]
    run maximal <<< $'GameManagement\nSharedGameCatalog'
    [ "$output" = $'GameManagement\nSharedGameCatalog' ]
}

@test "le regole fra shard vedono un doppio, un ciclo, un token annullato e un'esclusione scoperta" {
    # Controprova per le regole sotto: ognuna passa se il suo helper non stampa niente, quindi un
    # helper che non trovasse mai una violazione le farebbe passare su qualsiasi matrice.
    m="$(fake_matrix 'A|&(FullyQualifiedName~Foo)' 'B|&(FullyQualifiedName~Bar)')"
    run double_selections "$m"
    [ "$output" = "A/B: Foo + Bar" ]

    m="$(fake_matrix 'A|&(FullyQualifiedName~Foo)' 'B|&(FullyQualifiedName~Bar)&FullyQualifiedName!~Foo')"
    run double_selections "$m"
    [ "$status" -eq 0 ]
    [ -z "$output" ]
    run yield_cycle "$m"
    [ "$status" -eq 0 ]
    [ -z "$output" ]

    # Ognuno cede all'altro: nessun doppio, ma un nome con Foo e Bar non lo tiene nessuno.
    m="$(fake_matrix 'A|&(FullyQualifiedName~Foo)&FullyQualifiedName!~Bar' 'B|&(FullyQualifiedName~Bar)&FullyQualifiedName!~Foo')"
    run double_selections "$m"
    [ -z "$output" ]
    run yield_cycle "$m"
    [ "$output" = $'A\nB' ]

    m="$(fake_matrix 'A|&(FullyQualifiedName~Foo.Bar)&FullyQualifiedName!~Foo')"
    run dead_tokens "$m"
    [ "$output" = "A: Foo.Bar annullato da !~Foo" ]

    m="$(fake_matrix 'A|&(FullyQualifiedName~Foo)' 'B|&(FullyQualifiedName~Bar)&FullyQualifiedName!~Baz')"
    run uncovered_exclusions "$m"
    [ "$output" = "B esclude Baz, che nessun altro shard seleziona" ]
}

# ─── nessun test resta fuori da ogni shard ───────────────────────────────────────────────────────

@test "dev-async: Core esclude esattamente i token massimali selezionati dagli altri shard" {
    [ "$(excluded "$(filter_of "$DEV_ASYNC" Core)")" = "$(selected_outside_core "$DEV_ASYNC" | maximal)" ]
}

@test "ci.yml: Core esclude esattamente i token massimali selezionati dagli altri shard" {
    [ "$(excluded "$(filter_of "$CI" Core)")" = "$(selected_outside_core "$CI" | maximal)" ]
}

@test "un token escluso da uno shard e' selezionato da un altro" {
    # Uno shard puo' escludere un token per non eseguire due volte i test di un altro shard. Ma se
    # nessun altro lo seleziona, quei test non li prende nemmeno Core, che esclude i token degli altri.
    for wf in "$DEV_ASYNC" "$CI"; do
        run uncovered_exclusions "$wf"
        [ "$status" -eq 0 ]
        [ -z "$output" ] || { echo "$(basename "$wf"):"; echo "$output"; return 1; }
    done
}

@test "le esclusioni non formano un ciclo di precedenze" {
    # Se A cede a B e B cede ad A, un nome che contiene i token di entrambi non lo tiene nessuno dei
    # due, e Core lo esclude: sparisce da ogni shard. La regola sui doppi non lo vede, perche' quel
    # nome non gira due volte: non gira affatto. Insieme alla regola sopra basta: seguendo le
    # esclusioni da uno shard a quello che seleziona il token escluso, senza cicli si arriva a uno
    # shard che tiene il nome.
    for wf in "$DEV_ASYNC" "$CI"; do
        run yield_cycle "$wf"
        [ "$status" -eq 0 ]
        [ -z "$output" ] || { echo "$(basename "$wf"): shard in un ciclo di esclusioni, o che cedono a uno di essi:"; echo "$output"; return 1; }
    done
}

# ─── i due gate misurano lo stesso insieme ───────────────────────────────────────────────────────

@test "dev-async e ci.yml selezionano gli stessi test: stessi token, stesso Core" {
    # dev-async puo' avere una ripartizione piu' fine di ci.yml (#3742 divide Games in due, #4484
    # stacca AuthIntegration da KnowledgeBase), ma l'unione dev'essere la stessa: un test che un solo
    # gate esegue e' il difetto di #3622. Le esclusioni possono differire fra i due: decidono quale
    # shard tiene un test, non se qualcuno lo tiene, e quello lo verificano le regole qui sopra.
    dev="$(selected_outside_core "$DEV_ASYNC" | maximal)"
    [ -n "$dev" ]
    [ "$dev" = "$(selected_outside_core "$CI" | maximal)" ]
    [ "$(filter_of "$DEV_ASYNC" Core)" = "$(filter_of "$CI" Core)" ]
}

# ─── nessun test gira in due shard (#4488) ───────────────────────────────────────────────────────

@test "nessuna coppia di shard seleziona lo stesso test" {
    # Il filtro confronta anche il nome del metodo: `MergeKnowledgeBase_...`, in una classe di
    # SharedGameCatalog, conteneva i token di KnowledgeBase e di Catalog e girava in tutti e due. Vale
    # per ogni coppia di shard tranne Core, che esclude i token massimali, quindi ogni nome che un
    # altro shard seleziona (lo verificano i test sopra).
    for wf in "$DEV_ASYNC" "$CI"; do
        run double_selections "$wf"
        [ "$status" -eq 0 ]
        [ -z "$output" ] || {
            echo "$(basename "$wf"): un nome che contiene entrambi i token lo selezionano due shard:"
            echo "$output"
            return 1
        }
    done
}

@test "nessuno shard annulla un token che seleziona" {
    # Un token che il suo stesso shard esclude non seleziona niente: la matrice dichiara una
    # ripartizione che non fa. E' anche il modo in cui un token fuori dal dominio del suo shard
    # passerebbe le altre regole: `SharedGameCatalog.Foo` in AuthIntegration, che esclude
    # SharedGameCatalog, non fa doppi con Catalog, ma non prende nessun test.
    for wf in "$DEV_ASYNC" "$CI"; do
        run dead_tokens "$wf"
        [ "$status" -eq 0 ]
        [ -z "$output" ] || { echo "$(basename "$wf"):"; echo "$output"; return 1; }
    done
}

# ─── la baseline segue la matrice ────────────────────────────────────────────────────────────────

@test "la baseline ha una voce per ogni shard di dev-async, e nessuna in piu'" {
    # Uno shard senza voce fa uscire 1 il comparatore al primo run; una voce senza shard e' una
    # dichiarazione che nessuno confronta piu'.
    [ "$(jq -r '.shards | keys[]' "$BASELINE" | tr -d '\r' | LC_ALL=C sort)" = "$(names_of "$DEV_ASYNC")" ]
    for shard in $(names_of "$DEV_ASYNC"); do
        run jq -e --arg s "$shard" '.shards[$s].failed != null and .shards[$s].skipped != null' "$BASELINE"
        [ "$status" -eq 0 ]
    done
}
