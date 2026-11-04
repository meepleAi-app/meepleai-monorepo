#!/usr/bin/env bats
# Suite per l'installazione degli hook git — #4316.
#
# Tre cose, nell'ordine in cui si rompono:
#   1. lo script `prepare` di apps/web/package.json, eseguito così com'è scritto;
#   2. apps/web/scripts/install-git-hooks.mjs, che `prepare` chiama;
#   3. i permessi degli hook tracciati in .husky/.
#
# Perché esiste. `prepare` lanciava husky, che a ogni `pnpm install` eseguiva
# `git config core.hooksPath .husky/_` e generava quella cartella. La cartella è ignorata da git ed
# esiste solo nel checkout in cui husky è girato; la configurazione invece sta nel .git/config del
# repository, condiviso da tutti i worktree. Dopo un'installazione, ogni altro worktree cercava gli
# hook in una cartella che non aveva e non ne eseguiva nessuno, senza dirlo.
#
# Qui git è quello vero: i test creano repository e worktree di prova e ci fanno commit. Uno stub
# proverebbe meno, e su Windows node non lo troverebbe nemmeno (cerca git.exe, non uno script).

setup() {
    ROOT="$(cd "$BATS_TEST_DIRNAME/../../.." && pwd)"
    SCRIPT="$ROOT/apps/web/scripts/install-git-hooks.mjs"
    TMP="$(mktemp -d)"
}

teardown() {
    rm -rf "$TMP"
}

# Isola git dalla macchina che lancia la suite: niente configurazione globale o di sistema (un
# core.hooksPath globale falserebbe ogni lettura), messaggi in inglese, nessun repository cercato
# sopra $TMP. E niente HUSKY ereditato: chi lancia la suite può averlo a 0 nell'ambiente, e
# l'installatore lo rispetta.
isolate() {
    unset HUSKY GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE
    : > "$TMP/gitconfig"
    export GIT_CONFIG_GLOBAL="$TMP/gitconfig"
    export GIT_CONFIG_NOSYSTEM=1
    export GIT_CEILING_DIRECTORIES="$TMP"
    export LC_ALL=C
    export GIT_AUTHOR_NAME=suite GIT_AUTHOR_EMAIL=suite@example.invalid
    export GIT_COMMITTER_NAME=suite GIT_COMMITTER_EMAIL=suite@example.invalid
}

# Scrive in <checkout>/.husky/commit-msg un hook che, quando gira, lascia il proprio nome in
# $TMP/ran: è così che un test sa QUALE copia ha girato.
write_hook() {
    printf '#!/bin/sh\nprintf "%%s" "%s" > "%s"\n' "$2" "$TMP/ran" > "$1/.husky/commit-msg"
    chmod +x "$1/.husky/commit-msg"
}

# Un checkout di prova con la forma di questo repository: l'installatore sotto apps/web/scripts/,
# un hook in .husky/, un commit di base.
new_checkout() {
    mkdir -p "$1/apps/web/scripts" "$1/.husky"
    cp "$SCRIPT" "$1/apps/web/scripts/install-git-hooks.mjs"
    write_hook "$1" "$2"
    git -C "$1" init -q -b main
    git -C "$1" add -A
    git -C "$1" commit -q -m "base"
}

run_installer() {
    (cd "$1/apps/web" && node scripts/install-git-hooks.mjs)
}

# Lo script `prepare` vero, letto da package.json e passato a sh come fa pnpm su Linux.
prepare_string() {
    (cd "$ROOT/apps/web" && node -p "require('./package.json').scripts.prepare")
}

run_prepare() {
    (cd "$1" && sh -c "$(prepare_string)")
}

hooks_path() {
    git -C "$1" config --get core.hooksPath
}

# ─── lo script `prepare` ─────────────────────────────────────────────────────────────────────────

@test "prepare chiama l'installatore, non husky" {
    # `husky init` riscrive `prepare` a «husky»: è il modo in cui il difetto tornerebbe.
    prepare="$(prepare_string)"
    [[ "$prepare" == *"install-git-hooks.mjs"* ]]
    [[ "$prepare" != *husky* ]]
}

@test "prepare, eseguito com'è scritto, imposta core.hooksPath a .husky" {
    isolate
    new_checkout "$TMP/repo" principale

    run run_prepare "$TMP/repo/apps/web"

    [ "$status" -eq 0 ]
    [ "$(hooks_path "$TMP/repo")" = ".husky" ]
}

@test "prepare, senza l'installatore accanto, esce 0 e non stampa nulla" {
    # I Dockerfile di apps/web copiano package.json e lockfile PRIMA di `pnpm install`, e il resto
    # dopo: in quel momento lo script non c'è. Un `prepare` che fallisse lì romperebbe la build
    # delle immagini; uno che stampasse un errore lascerebbe una falsa pista in ogni log.
    isolate
    mkdir -p "$TMP/app"
    cp "$ROOT/apps/web/package.json" "$TMP/app/"

    run run_prepare "$TMP/app"

    [ "$status" -eq 0 ]
    [ -z "$output" ]
}

# ─── l'installatore ──────────────────────────────────────────────────────────────────────────────

@test "sostituisce il valore lasciato da husky (.husky/_)" {
    isolate
    new_checkout "$TMP/repo" principale
    git -C "$TMP/repo" config core.hooksPath .husky/_

    run run_installer "$TMP/repo"

    [ "$status" -eq 0 ]
    [[ "$output" == *"core.hooksPath = .husky"* ]]
    [ "$(hooks_path "$TMP/repo")" = ".husky" ]
}

@test "sostituisce un percorso assoluto verso un altro checkout" {
    # Il rimedio a mano che si usava: tutti i worktree eseguivano gli hook di QUEL checkout, al
    # commit su cui era fermo.
    isolate
    new_checkout "$TMP/repo" principale
    git -C "$TMP/repo" config core.hooksPath "$TMP/altrove/.husky"

    run run_installer "$TMP/repo"

    [ "$status" -eq 0 ]
    [ "$(hooks_path "$TMP/repo")" = ".husky" ]
}

@test "dopo l'installazione ogni checkout esegue la PROPRIA copia dell'hook" {
    isolate
    new_checkout "$TMP/repo" principale
    git -C "$TMP/repo" worktree add -q -b altro "$TMP/wt"
    write_hook "$TMP/wt" worktree
    run_installer "$TMP/repo"

    git -C "$TMP/wt" commit -q --allow-empty -m "dal worktree"
    [ "$(cat "$TMP/ran")" = "worktree" ]

    git -C "$TMP/repo" commit -q --allow-empty -m "dal principale"
    [ "$(cat "$TMP/ran")" = "principale" ]
}

@test "installare da un worktree vale per tutti i checkout" {
    # `pnpm install` gira nel checkout in cui si lavora, che di rado è quello principale.
    isolate
    new_checkout "$TMP/repo" principale
    git -C "$TMP/repo" worktree add -q -b altro "$TMP/wt"
    write_hook "$TMP/wt" worktree

    run_installer "$TMP/wt"

    [ "$(hooks_path "$TMP/repo")" = ".husky" ]
    git -C "$TMP/repo" commit -q --allow-empty -m "dal principale"
    [ "$(cat "$TMP/ran")" = "principale" ]
    git -C "$TMP/wt" commit -q --allow-empty -m "dal worktree"
    [ "$(cat "$TMP/ran")" = "worktree" ]
}

@test "HUSKY=0: non tocca la configurazione, e lo dice" {
    isolate
    new_checkout "$TMP/repo" principale
    git -C "$TMP/repo" config core.hooksPath .husky/_
    export HUSKY=0

    run run_installer "$TMP/repo"

    [ "$status" -eq 0 ]
    [[ "$output" == *"HUSKY=0"* ]]
    [ "$(hooks_path "$TMP/repo")" = ".husky/_" ]
}

@test "senza un .git alla radice non configura il repository che lo contiene" {
    # Una copia dell'albero dentro un altro repository (o un'immagine con tutto tranne .git): git
    # risalirebbe fino al repository esterno e gli toglierebbe i suoi hook.
    isolate
    git init -q -b main "$TMP/esterno"
    mkdir -p "$TMP/esterno/copia/apps/web/scripts"
    cp "$SCRIPT" "$TMP/esterno/copia/apps/web/scripts/install-git-hooks.mjs"

    run run_installer "$TMP/esterno/copia"

    [ "$status" -eq 0 ]
    [[ "$output" == *"niente da installare"* ]]
    run hooks_path "$TMP/esterno"
    [ "$status" -ne 0 ]
    [ -z "$output" ]
}

@test "se git non riesce a scrivere la configurazione, non fallisce e dice perché" {
    isolate
    new_checkout "$TMP/repo" principale
    : > "$TMP/repo/.git/config.lock"       # un altro git sta scrivendo la configurazione

    run run_installer "$TMP/repo"

    [ "$status" -eq 0 ]
    [[ "$output" == *"NON installati"* ]]
    [[ "$output" == *"could not lock config file"* ]]
    [[ "$output" == *"git config core.hooksPath .husky"* ]]    # come rimediare
}

@test "senza git nel PATH non fallisce, e lo dice" {
    # Un contenitore di sviluppo che monta il repository ma non ha git: `pnpm install` deve finire.
    isolate
    new_checkout "$TMP/repo" principale
    node_bin="$(command -v node)"
    mkdir "$TMP/vuoto"

    run env PATH="$TMP/vuoto" "$node_bin" "$TMP/repo/apps/web/scripts/install-git-hooks.mjs"

    [ "$status" -eq 0 ]
    [[ "$output" == *"NON installati"* ]]
}

# ─── gli hook tracciati ──────────────────────────────────────────────────────────────────────────

# Con core.hooksPath = .husky git lancia gli hook direttamente, senza l'involucro di husky che li
# passava a `sh`. Su Unix un hook non eseguibile viene ignorato, con un avviso che nessuno legge; e
# da Windows un file nuovo entra nell'indice come 100644, perché lì il bit non esiste.

@test "ogni hook tracciato in .husky/ è eseguibile nell'indice" {
    listing="$(git -C "$ROOT" ls-files -s -- .husky)"
    # I tre che il flusso di lavoro usa ci devono essere: su un elenco vuoto il controllo sotto
    # passerebbe senza aver guardato nulla.
    for hook in pre-commit commit-msg pre-push; do
        [[ "$listing" == *$'\t'".husky/$hook"* ]]
    done

    not_executable="$(printf '%s\n' "$listing" | grep -v '^100755 ' || true)"
    if [ -n "$not_executable" ]; then
        echo "non eseguibili nell'indice (git update-index --chmod=+x <file>):"
        echo "$not_executable"
        false
    fi
}

@test "ogni hook tracciato comincia con uno shebang, senza ritorno a capo Windows" {
    checked=0
    while IFS= read -r path; do
        # I byte della prima riga come li stampa od, non la riga stessa: sotto Git Bash una
        # sostituzione di comando toglie i CR, e il controllo sul CR passerebbe senza guardare.
        first="$(head -n 1 "$ROOT/$path" | od -An -c | tr -d ' \n')"
        if [[ "$first" != '#!'* ]]; then
            echo "$path: manca lo shebang"
            false
        fi
        if [[ "$first" == *'\r'* ]]; then
            echo "$path: lo shebang finisce con CR, su Unix l'interprete non si trova"
            false
        fi
        checked=$((checked + 1))
    done < <(git -C "$ROOT" ls-files -- .husky)
    [ "$checked" -ge 1 ]
}
