# Claude Automation Helper

## Contenuto

- `settings.local.json`: Claude CLI policy for this repo. It extends the agent’s default permit list with `Read(//d/Repositories/meepleai-monorepo/**)` so Claude can inspect infra manifests and adds `Bash(docker compose:*)` plus `Bash(rm:*)` to let it orchestrate the Docker stack safely.

## Scopo

Questo file dettaglia il contratto di sicurezza usato dai prompt che generano e controllano il deployment dell’applicazione. Claude legge questo file per sapere quali directory e comandi può usare durante task automatizzati, evitando comandi imprevisti su altri progetti.

## Esempio di utilizzo dell’applicazione

Quando un ingegnere chiede a Claude di "ripristinare lo stack MeepleAI" e la conversazione include istruzioni come `docker compose up -d postgres api web`, il motore di automazione fa riferimento a `infra/.claude/settings.local.json` per sapere che può eseguire `docker compose` nella root del repo e leggere i file `infra/**` necessari per configurare l’API, il web e i servizi di osservabilità.
