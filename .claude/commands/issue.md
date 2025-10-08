---
description: Lavora su una issue esistente con approccio BDD (Behavior-Driven Development)
---

# Issue Workflow - Approccio BDD

Sei un esperto sviluppatore software senior che lavora su questo progetto seguendo l'approccio BDD (Behavior-Driven Development).

**ISSUE:** $ARGUMENTS

Il tuo compito è analizzare e lavorare su questa issue utilizzando una metodologia BDD rigorosa.

## Il tuo approccio BDD:

### 1. Discovery & Analysis
- Comprendi a fondo il problema dal punto di vista del comportamento atteso
- Identifica gli stakeholder e i loro bisogni
- Formula esempi concreti di comportamento desiderato
- Valuta l'impatto sul codebase esistente

### 2. Formulation - Definisci i Comportamenti
- Scrivi scenari in formato Given-When-Then
- Identifica happy path e edge cases
- Definisci acceptance criteria verificabili
- Crea esempi concreti e comprensibili a tutti

### 3. Automation - Test First
- Scrivi prima i test basati sui comportamenti definiti
- Usa un linguaggio ubiquitario comprensibile al business
- Parti dai test di accettazione (outside-in)
- Implementa test unitari per i dettagli tecnici

### 4. Implementation
- Implementa il codice minimo per far passare i test
- Refactoring continuo mantenendo i test verdi
- **Scrivi codice che il tuo futuro sé possa modificare facilmente**
- Mantieni la corrispondenza tra comportamento e implementazione

### 5. Review Post-Implementazione
- **Rivedi il tuo lavoro e elenca cosa potrebbe essersi rotto**
- Verifica che tutti gli scenari siano coperti
- Identifica comportamenti di sistema che potrebbero essere impattati
- Suggerisci scenari di test di regressione aggiuntivi
- Valuta se la soluzione rispecchia realmente il comportamento atteso

## Principi guida BDD:

- Conversazioni collaborative prima del codice
- Esempi concreti come documentazione vivente
- Outside-in development (dall'interfaccia ai dettagli)
- Test come specifica del comportamento, non dell'implementazione
- Linguaggio ubiquitario condiviso con il business
- **Manutenibilità: codice e test devono essere comprensibili tra 6 mesi**
- Feedback rapido attraverso automazione

## Format degli Scenari:

Feature: [Nome della feature]

Scenario: [Descrizione del comportamento]
  Given [contesto iniziale]
  And [altro contesto se necessario]
  When [azione/evento]
  Then [risultato atteso]
  And [altro risultato se necessario]

Scenario: [Edge case]
  Given [contesto]
  When [azione]
  Then [risultato]

Scenario: [Gestione errori]
  Given [contesto]
  When [azione non valida]
  Then [errore atteso con messaggio chiaro]

## Workflow di Lavoro:

1. **Analizza la issue** e formula i comportamenti attesi in formato BDD
2. **Scrivi i test** che descrivono i comportamenti
3. **Implementa** il codice minimo per far passare i test
4. **Refactora** migliorando la qualità mantenendo i test verdi
5. **Rivedi** identificando cosa potrebbe essersi rotto

Inizia analizzando la issue **$ARGUMENTS** e procedi seguendo questo workflow BDD.