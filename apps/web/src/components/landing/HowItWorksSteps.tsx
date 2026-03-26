const steps = [
  {
    num: '1',
    icon: '🎲',
    title: 'Trova il gioco',
    desc: "Cerca nel catalogo condiviso. Non c'è? Aggiungilo come gioco privato.",
  },
  {
    num: '2',
    icon: '📄',
    title: 'Carica le regole',
    desc: "Upload del PDF del regolamento. L'AI lo indicizza automaticamente.",
  },
  {
    num: '3',
    icon: '🤖',
    title: "Gioca con l'arbitro AI",
    desc: "Setup, regole, punteggi, dispute — l'agente vi assiste al tavolo.",
  },
  {
    num: '4',
    icon: '💾',
    title: 'Salva e riprendi',
    desc: "Non finite? L'agente ricorda lo stato della partita per la prossima volta.",
  },
];

export function HowItWorksSteps() {
  return (
    <section
      id="come-funziona"
      aria-labelledby="how-it-works-heading"
      className="bg-muted/30 px-4 py-20"
    >
      <div className="mx-auto max-w-5xl">
        <h2
          id="how-it-works-heading"
          className="mb-12 text-center text-3xl font-bold text-foreground"
        >
          Come funziona
        </h2>
        <div className="grid grid-cols-1 gap-8 sm:grid-cols-2 lg:grid-cols-4">
          {steps.map(step => (
            <div key={step.num} className="flex flex-col items-center text-center">
              <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-full bg-primary text-sm font-bold text-primary-foreground">
                {step.num}
              </div>
              <span aria-hidden="true" className="mb-2 text-2xl">
                {step.icon}
              </span>
              <h3 className="mb-1 text-lg font-semibold text-foreground">{step.title}</h3>
              <p className="text-sm text-muted-foreground">{step.desc}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
