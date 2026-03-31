const steps = [
  {
    num: '1',
    icon: '⚡',
    title: 'Regola subita',
    desc: 'Chiedi in italiano, ricevi la risposta dalla pagina esatta del manuale. In 10 secondi.',
  },
  {
    num: '2',
    icon: '🤝',
    title: 'Niente più dispute',
    desc: "L'AI cita la pagina. La discussione finisce lì. Il gioco continua.",
  },
  {
    num: '3',
    icon: '🎲',
    title: 'Serata salvata',
    desc: 'Punteggi, timer, setup guidato — tutto al tavolo senza interrompere la partita.',
  },
  {
    num: '4',
    icon: '📖',
    title: 'Ricorda tutto',
    desc: 'La tua storia di gioco, le partite, lo stile. Il tuo profilo da gamer cresce con te.',
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
        <ul className="grid list-none grid-cols-1 gap-8 p-0 sm:grid-cols-2 lg:grid-cols-4">
          {steps.map(step => (
            <li key={step.num} className="flex flex-col items-center text-center">
              <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-full bg-primary text-sm font-bold text-primary-foreground">
                {step.num}
              </div>
              <span aria-hidden="true" className="mb-2 text-2xl">
                {step.icon}
              </span>
              <h3 className="mb-1 text-lg font-semibold text-foreground">{step.title}</h3>
              <p className="text-sm text-muted-foreground">{step.desc}</p>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
