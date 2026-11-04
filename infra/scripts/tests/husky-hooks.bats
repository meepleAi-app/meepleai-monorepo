#!/usr/bin/env bats
# Suite per gli hook tracciati in .husky/: l'interprete che dichiarano, e ciò che fanno sotto
# quello — #4321.
#
# Gli hook dichiaravano `#!/bin/sh` ma sono scritti in bash (`[[ … ]]`, `=~`, `&>`). Su Windows non
# si vede, perché lì /bin/sh È bash. Dove /bin/sh è dash l'hook non si ferma: stampa
# «[[: not found» e prende il ramo sbagliato. Su main-dev, pre-commit saltava formattazione del
# backend e ricerca dei segreti e finiva con «checks passed».
#
# Due livelli:
#   - la prima riga di ogni hook tracciato: rossa anche su Windows, dove si sviluppa;
#   - gli hook veri, lanciati come li lancia git (dal file, con l'interprete che dichiara), in una
#     cartella di prova con npx, pnpm, dotnet, detect-secrets e semgrep finti che registrano le
#     chiamate. Questa parte distingue bash da dash solo dove /bin/sh è dash: nel job di CI, non su
#     Windows.

setup() {
    ROOT="$(cd "$BATS_TEST_DIRNAME/../../.." && pwd)"
    TMP="$(mktemp -d)"
    export CALLS="$TMP/calls.log"
    : > "$CALLS"

    mkdir -p "$TMP/bin"
    for tool in npx pnpm dotnet detect-secrets semgrep; do
        printf '#!/bin/sh\nprintf "%%s %%s\\n" "%s" "$*" >> "$CALLS"\n' "$tool" > "$TMP/bin/$tool"
        chmod +x "$TMP/bin/$tool"
    done
    export PATH="$TMP/bin:$PATH"

    # Git isolato dalla macchina che lancia la suite, e dalle variabili che git imposta quando la
    # suite gira dentro un hook.
    unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE
    : > "$TMP/gitconfig"
    export GIT_CONFIG_GLOBAL="$TMP/gitconfig"
    export GIT_CONFIG_NOSYSTEM=1
    export GIT_CEILING_DIRECTORIES="$TMP"
}

teardown() {
    rm -rf "$TMP"
}

# Un repository di prova sul ramo dato, con la forma che pre-commit si aspetta e un file C# in
# stage: è quello che fa scattare la formattazione del backend, sui rami che la prevedono.
new_repo_on() {
    git init -q -b "$1" "$TMP/repo"
    mkdir -p "$TMP/repo/apps/web" "$TMP/repo/apps/api"
    printf 'class Foo {}\n' > "$TMP/repo/apps/api/Foo.cs"
    git -C "$TMP/repo" add -A 2>/dev/null
}

# Lancia un hook come fa git: dal file, dalla radice del checkout.
run_hook() {
    local hook="$1"
    shift
    (cd "$TMP/repo" && "$ROOT/.husky/$hook" "$@")
}

# In bats un `! comando` che non è l'ultima riga del test non lo fa fallire: le negazioni passano
# da qui, e in caso di errore si vede che cosa è stato chiamato davvero.
called() {
    if ! grep -q "^$1" "$CALLS"; then
        echo "non chiamato: $1"
        cat "$CALLS"
        return 1
    fi
}

never_called() {
    if grep -q "^$1 " "$CALLS"; then
        echo "chiamato, e non doveva: $1"
        cat "$CALLS"
        return 1
    fi
}

# ─── l'interprete dichiarato ─────────────────────────────────────────────────────────────────────

@test "ogni hook tracciato dichiara bash" {
    # Regola, non preferenza: chi modifica un hook lavora su Windows, dove un bashismo sotto
    # #!/bin/sh funziona. Lo vedrebbe solo chi usa Linux, e come controlli saltati in silenzio.
    checked=0
    while IFS= read -r path; do
        IFS= read -r first < "$ROOT/$path"
        if [ "$first" != '#!/usr/bin/env bash' ]; then
            echo "$path: la prima riga deve essere «#!/usr/bin/env bash», è «$first»"
            false
        fi
        checked=$((checked + 1))
    done < <(git -C "$ROOT" ls-files -- .husky)
    [ "$checked" -ge 1 ]
}

# ─── commit-msg ──────────────────────────────────────────────────────────────────────────────────

@test "commit-msg: un percorso assoluto arriva a commitlint così com'è" {
    # Nei worktree git passa il file del messaggio con il percorso assoluto.
    mkdir -p "$TMP/repo/apps/web" "$TMP/altrove"
    : > "$TMP/altrove/COMMIT_EDITMSG"

    run run_hook commit-msg "$TMP/altrove/COMMIT_EDITMSG"

    [ "$status" -eq 0 ]
    [[ "$output" != *"not found"* ]]
    [ "$(cat "$CALLS")" = "npx --yes commitlint --config ../../commitlint.config.js --edit $TMP/altrove/COMMIT_EDITMSG" ]
}

@test "commit-msg: un percorso con la lettera di unità arriva a commitlint così com'è" {
    mkdir -p "$TMP/repo/apps/web"

    run run_hook commit-msg "C:/Users/x/COMMIT_EDITMSG"

    [ "$status" -eq 0 ]
    [ "$(cat "$CALLS")" = "npx --yes commitlint --config ../../commitlint.config.js --edit C:/Users/x/COMMIT_EDITMSG" ]
}

@test "commit-msg: un percorso relativo viene risolto dalla radice del checkout" {
    mkdir -p "$TMP/repo/apps/web"

    run run_hook commit-msg ".git/COMMIT_EDITMSG"

    [ "$status" -eq 0 ]
    [ "$(cat "$CALLS")" = "npx --yes commitlint --config ../../commitlint.config.js --edit $TMP/repo/.git/COMMIT_EDITMSG" ]
}

# ─── pre-commit: i controlli crescono col ramo ───────────────────────────────────────────────────

@test "pre-commit su un ramo feature: solo i controlli del frontend" {
    new_repo_on feature/issue-1-prova

    run run_hook pre-commit

    [ "$status" -eq 0 ]
    [[ "$output" != *"not found"* ]]
    called 'npx lint-staged'                            # l'hook ha girato fino in fondo
    called 'pnpm typecheck'
    never_called dotnet
    never_called detect-secrets
    never_called semgrep
}

@test "pre-commit su main-dev: anche formattazione del backend e ricerca dei segreti" {
    new_repo_on main-dev

    run run_hook pre-commit

    [ "$status" -eq 0 ]
    [[ "$output" != *"not found"* ]]
    called 'pnpm typecheck'
    called 'dotnet format --verify-no-changes'
    called 'detect-secrets scan --baseline .secrets.baseline apps/api/Foo.cs'
    never_called semgrep
}

@test "pre-commit su main: anche semgrep" {
    new_repo_on main

    run run_hook pre-commit

    [ "$status" -eq 0 ]
    [[ "$output" != *"not found"* ]]
    called 'dotnet format --verify-no-changes'
    called 'detect-secrets scan'
    called 'semgrep --config .semgrep.yml --quiet apps/api/Foo.cs'
}
