# Baseline dello spike dell'assistente in partita

I run citati dalla spec `docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md` §7.1, così come li ha scritti `run-eval.mjs`. Servono a ricalcolare i numeri dell'esito e a confrontare per nome di scenario una versione nuova del prompt con questa.

`2026-10-12/`: prompt v1, temperatura 0,2, al massimo quattro giri con strumenti.

- `deepseek-flash.set-v2.run1.json` … `run6.json`: il set alla versione 2, con l'immagine dell'API costruita dopo le correzioni della review di #4240.
- `qwen2.5_3b.set-v1.run1.json`: il set alla versione 1, prima che la review aggiungesse tre trappole.

`2026-10-12-prompt-v2/`: l'esito di #4256, con `deepseek-flash` e gli stessi parametri. Ogni file dice nel campo `prompts` quale prompt è stato mandato (`null` è quello del codice di allora, la v1).

- `deepseek-flash.set-v2.run1.json` … `run3.json`: il prompt v2 sul set alla versione 2, tre run completi.
- `deepseek-flash.failing-scenarios.json` e `deepseek-flash.prompt-v1.failing-scenarios.json`: i tre scenari che la v1 falliva, ripetuti, con il prompt v2 e con il v1.
- `deepseek-flash.new-scenarios.json` e `deepseek-flash.prompt-v1.new-scenarios.json`: gli otto scenari aggiunti con la versione 3 del set, tre volte ciascuno, con il prompt v2 e con il v1.
