const stats = [
  { value: '2.400+', label: 'Giochi nel catalogo' },
  { value: '95%+', label: 'Accuratezza citazioni' },
  { value: 'Gratis', label: 'Per iniziare' },
];

export function SocialProofBar() {
  return (
    <section aria-label="Statistiche" className="bg-muted/50 px-4 py-16">
      <div className="mx-auto flex max-w-3xl flex-col items-center gap-8 sm:flex-row sm:justify-center sm:gap-16">
        {stats.map(stat => (
          <div key={stat.label} className="text-center">
            <p className="text-4xl font-bold text-foreground">{stat.value}</p>
            <p className="text-sm text-muted-foreground">{stat.label}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
