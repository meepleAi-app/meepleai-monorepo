## Comandi

in [session/new](https://meepleai.app/sessions/new) c'e' un pop

## Template

/using-superpowers ultrathink /sc:spec-panel
Riflettiamo sulla seguente user story:
Esploriamo questa idea.
Solo un test sul browser valida l'implementazione della US.

### User story

/using-superpowers ultrathink /sc:spec-panel riflettiamo sulla seguente user story:
sono un admin e voglio provare un gioco che ho acquistato e che ho il pdf, voglio testare il processo di embeeding e come funziona l'agente usando il rag che gli ho fornito con il kb creato. dopo l'upload, voglio andare nella pagina admin per visualizzare le code di embeeding .
Solo un test sul browser valida l'implementazione della US

/superpowers:using-superpowers ultrathink /sc:spec-panel riflettiamo sulla seguente user story, analizziamo le open issue e creiamo sequenza di issue da implementare (gia' aperte o nuove) legata a una epic per la seguente user story:

- Sei un game host e avrai due amici, Alessandro e Pouli, che vengono a trovarti per giocare a dei giochi da tavolo e vuoi usare meepleai.app come assistente, se ci sono delle opinioni differenti sulle regole, per fare un riassunto del funzionamento dei turni e per aiutare a preparare il primo turno di gioco. Vuoi usare l'app per tenere un diario, generare eventi random (tiro di un dado, pesca di una carta)(parte del toolkit di quel gioco), per ricordare il turno e/o la fase attuale (parte del toolkit di quel gioco)
- Decidiamo 3 giochi, uno con il RAG presente (almeno un pdf inizializzato), uno che verra' creato (cercanwdolo in BGG e con pdf privato caricato) e uno senza pdf.
- Durante la serata vuoi segnare delle note, degli eventi, come note vocali o come note, per tenere un diario di quello che succede durante la partita (parte del toolkit di quel gioco). un agente potrebbe aiutarti.

/using-superpowers ultrathink /sc:spec-panel analizziamo l'idea di toolbox. Un toolbox rappresenta un contenitore che puo' essere diverso gioco da gioco. Ci sono giochi che richiedono un semplice segnapunti e il tiro di dadi, altri con complesse fasi/turni e sistemi di punTEGGIO. potremmo usare deck di poker per estrarre una carta o mazzi con carte specializzate. E altre funzioni utili con un certo gioco.,

/using-superpowers ultrathink /sc:spec-panel
a che punto e' la user story game night improvvisata?
esploriamo questa idea.
Degli amici inaspettatamente vengono a trovarmi. Vedendo la collezioni dei giochi a nostra disposizione , cerchiamo nell’app. Non e’ presente e provo a cercare dall’app in BGG. Lo trovo,lo aggiungo alla collezione come gioco privato e posseduto, e carico un pdf. Appare l’accordo che il pdf puo’ essere usato solo se si possiede il gioco , per visualizzare i riferimenti e accetto.
Ricevo una notifica quando il pdf e’ pronto e posso creare l’agente con il rag.
L’agente mi aiuta a preparare la tavola per la partita. L’agente ci aiuta a tenere traccia dei punteggi. L’agente ci aiuta se ci sono dubbi o discussioni sulle regole (decide chi ha ragione).A fine serata, non riusciamo a finire la partita e salviamo, con la memoria agente, con foto o altro lo stato della partita, per poter riprendere in un secondo momento.
Controlla quanto e’ gia’ pronto e le issue aperte per determinare una roadmap al completamento della user story

/using-superpowers ultrathink /sc:spec-panel Come admin, voglio vedere, organizzati in libreria, gli elementi ui, i layout, per poter vedere lo stato attuale senza dover crearlo nell'app

/using-superpowers ultrathink /sc:spec-panel
-user story serata descent
/using-superpowers ultrathink /sc:spec-panel analizziamo questa user story, creiamo epic con issue
create e issue aperte per completare la user story.
Admin vuole aggiungere un gioco alla lista shared games. Crea gioco -> Cerca Descent (ha un pdf in data/rulebooks)
->carica pdf di descent,visualizza il progresso di embeeding,Salva il gioco . 2esegue un test di chat con il rag
-Db versione 0
/superpowers:using-superpowers ultrathink /sc:spec-panel analizza le issue, ci sono issue che richiedono la modifica allo schema/tabelle del db? voglio partire da una versione beta del database, rimuovendo i dati e le migrationi, partendo da una versione 0

### UX

/using-superpowers ultrathink /sc:spec-panel miglioriamo la UX. Su smartphone la grandezza della singola meeple card dovrebbe occupare quasi l'intero schermo, quando in focus.
Cambiando la visualizzazione a lista/griglia, una versione media della meeple card.
Non ci devono esser troppi "scroll" da fare, ma navigare velocemente tra elementi di domini differenti e pagine

### -CI

/superpowers:using-superpowers ultrathink correggiamo gli errori di ci e altre actions, se l'ultima run e' in errore
/superpowers:using-superpowers ultrathink /sc:spec-panel ottimizziamo la velocita' di fase integration test
❯ /superpowers:using-superpowers ultrathink /sc:spec-panel ci sono dei piani che abbiamo creato a cui mancano issue da creare?  
analizza i piani e, se ne trovi controllane uno alla volta. se e' stato implementato o le issue sono state create e chiuse eliminalo.  
se qualcosa manca, fermati e parliamone.

## - Issue

/using-superpowers ultrathink fai uno screenshot sulla pagina /discover
in modalita' smartphone

/using-superpowers ultrathink /sc:spec-panel analizza le issue, ci sono issue che richiedono la
/sc:pm analizza le open issue e prepara roadmap per la risoluzione, la parte sul publisher e sul training ai sono a bassa priorita'

## Meeple Card

❯ si. Un discorso piu' ampio. ultrathink /sc:spec-panel Che azione sono disponibili abilitate/disabilitate per ogni tipo di card in base al contesto? La card ha una fronte, un retro e un drawer. Bisogna decidere, per ogni card, cosa si mostra dove.
Bisogna decidere :

- info da visualizzare (label, link,etc...), -azioni a livello card -azioni "veloci" -navigazione a un altra card
  -ci sono altre cose da poter inserire nella card? lo scopo e' far avere a portata azioni e collegamenti con un tipo di card verso altri tipi, collegati

  /superpowers:using-superpowers ultrathink /sc:spec-panel cerchiamo gap tra backend e frontend
