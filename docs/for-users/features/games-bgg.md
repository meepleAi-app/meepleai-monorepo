# Catalogo giochi & integrazione BoardGameGeek

> **Cos'è in una frase**: aggiungi giochi alla tua libreria importandoli da BoardGameGeek (BGG) o creandoli a mano.

---

## In breve

| | |
|---|---|
| **A cosa serve** | Avere il gioco "esistente" prima di caricare manuale, fare chat o tracciare partite |
| **Per chi** | Tutti gli utenti |
| **Fonti dati** | BoardGameGeek API (catalogo pubblico, ~140k giochi) + creazione manuale |
| **Dove si trova** | Pagina **Catalogo** → **Aggiungi gioco** |

---

## Tre modi per aggiungere un gioco

### 1. Cerca su BGG (raccomandato)

1. **Catalogo** → **Aggiungi gioco** → tab **BGG**.
2. Cerca per titolo (es. *"Brass Birmingham"*).
3. Risultati con copertina, anno, designer, voto medio BGG.
4. Clicca **Importa**: tutti i metadati (designer, editore, peso, durata, n° giocatori, mecaniche, categorie, lingua, immagini) vengono importati.

**Tempo**: ~5 secondi.

### 2. Importazione massiva da BGG

Se hai una collezione su BGG col tuo username:

1. **Catalogo** → **Importa collezione BGG**.
2. Inserisci il tuo BGG username.
3. MeepleAI scarica tutti i giochi della tua "Owned Collection".
4. Conferma con un clic.

**Tempo**: 30-60s per 50 giochi.

### 3. Creazione manuale

Per giochi non su BGG (homebrew, prototipo, edizione regionale):

1. **Catalogo** → **Aggiungi gioco** → tab **Manuale**.
2. Compila: titolo, anno, n° giocatori, durata, peso (1-5), descrizione.
3. Carica copertina (opzionale, JPG/PNG max 5 MB).
4. Salva.

Il gioco è subito disponibile — funzioni AI (chat, agents) richiedono [il manuale PDF](./pdf-upload.md).

---

## Dati importati da BGG

| Campo | Esempio |
|-------|---------|
| Titolo + titolo originale | *Brass: Birmingham* / *Brass: Birmingham* |
| Anno pubblicazione | 2018 |
| Designer | Martin Wallace, Gavan Brown, Matt Tolman |
| Editore | Roxley Games |
| N° giocatori | 2-4 |
| Durata | 60-120 min |
| Età consigliata | 14+ |
| Peso (BGG weight) | 3.89 / 5 |
| Voto medio BGG | 8.6 / 10 |
| Meccaniche | Network Building, Tile Placement, ... |
| Categorie | Economic, Industry / Manufacturing |
| Copertina | Auto-import dalla CDN BGG |

I dati BGG vengono **rinfrescati** ogni 30 giorni (peso, voti).

---

## Workspace condiviso (gioco di gruppo)

I giochi aggiunti possono essere:

- **Privati** (visibili solo a te)
- **Workspace** (visibili a tutto il tavolo / gruppo)
- **Comunitari** (proposti per il catalogo pubblico MeepleAI — richiede approvazione admin)

Questo controlla chi vede il gioco e chi può fare chat/aggiungere errata.

---

## Limiti noti

- ⚠️ **BGG rate limit**: 10 ricerche/minuto. Se importi una collezione grande, può richiedere qualche minuto.
- ⚠️ **Edizioni multiple**: BGG indica un solo "preferred name" — se hai l'edizione italiana retitlata, modifica il titolo a mano dopo l'import.
- ❌ **Giochi non in BGG**: solo creazione manuale (nessun auto-import).
- ⚠️ **Espansioni**: importa l'espansione separatamente; collegala al gioco base nella scheda dell'espansione.

---

## Modificare / eliminare un gioco

- **Modifica**: pagina del gioco → ⋮ → **Modifica scheda** (campi editabili anche dopo l'import BGG; le tue modifiche sopravvivono al refresh).
- **Elimina**: solo se sei admin del workspace o owner del gioco. **Soft-delete** — recuperabile per 30 giorni dal cestino.

---

## Funzioni correlate

- [Caricamento PDF manuale](./pdf-upload.md) — sblocca chat AI sul gioco
- [Libreria personale](./library.md) — collezione, wishlist, statistiche
- [Chat sulle regole](./rag-chat.md)

---

**Ultima revisione**: 2026-05-06
