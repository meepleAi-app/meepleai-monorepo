# Baseline dello spike dell'assistente in partita

I run citati dalla spec `docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md` §7.1, così come li ha scritti `run-eval.mjs`. Servono a ricalcolare i numeri dell'esito e a confrontare per nome di scenario una versione nuova del prompt con questa.

`2026-10-12/`: prompt v1, temperatura 0,2, al massimo quattro giri con strumenti.

- `deepseek-flash.set-v2.run1.json` … `run6.json`: il set alla versione 2, con l'immagine dell'API costruita dopo le correzioni della review di #4240.
- `qwen2.5_3b.set-v1.run1.json`: il set alla versione 1, prima che la review aggiungesse tre trappole.
