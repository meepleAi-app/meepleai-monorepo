/**
 * Features Section - Landing Page
 */

import { Bot, BookOpen, Smartphone } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

const features = [
  {
    icon: Bot,
    title: 'AI Intelligente',
    description:
      'Risposte immediate con citazioni dal manuale. Niente più discussioni sulle regole.',
    gradientFrom: 'from-primary/20',
    gradientTo: 'to-primary/5',
    bgColor: 'bg-primary/10',
    iconColor: 'text-primary',
  },
  {
    icon: BookOpen,
    title: 'Catalogo Ampio',
    description: 'Migliaia di giochi già disponibili. Aggiungi i tuoi manuali personalizzati.',
    gradientFrom: 'from-secondary/20',
    gradientTo: 'to-secondary/5',
    bgColor: 'bg-secondary/10',
    iconColor: 'text-secondary',
  },
  {
    icon: Smartphone,
    title: 'Mobile-First',
    description: 'Perfetto durante le partite. Risposte rapide senza interrompere il gioco.',
    gradientFrom: 'from-accent/20',
    gradientTo: 'to-accent/5',
    bgColor: 'bg-accent/10',
    iconColor: 'text-accent',
  },
];

export function FeaturesSection() {
  return (
    <section
      id="features"
      className="py-16 md:py-24 px-4 bg-gradient-to-b from-muted/30 to-background relative overflow-hidden"
    >
      <div className="absolute top-0 left-1/4 w-64 h-64 bg-primary/5 rounded-full blur-3xl" />
      <div className="absolute bottom-0 right-1/4 w-64 h-64 bg-secondary/5 rounded-full blur-3xl" />

      <div className="container mx-auto max-w-6xl relative z-10">
        <div className="text-center mb-12 animate-fade-in">
          <div className="inline-flex items-center justify-center px-4 py-2 mb-4 rounded-full bg-primary/10 text-primary text-sm font-semibold">
            ✨ Caratteristiche Principali
          </div>
          <h2 className="font-heading text-3xl md:text-4xl font-bold text-foreground mb-4">
            Caratteristiche
          </h2>
          <p className="text-lg text-muted-foreground max-w-2xl mx-auto">
            Tutto quello che ti serve per giocare senza dubbi
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 md:gap-8">
          {features.map((feature, index) => {
            const Icon = feature.icon;
            return (
              <Card
                key={feature.title}
                className="group relative overflow-hidden hover:shadow-lg transition-all duration-500 hover:-translate-y-2 border-muted/50 bg-card/50 backdrop-blur-sm animate-slide-up"
                style={{ animationDelay: `${index * 100}ms` }}
              >
                <div
                  className={`absolute inset-0 bg-gradient-to-br ${feature.gradientFrom} ${feature.gradientTo} opacity-0 group-hover:opacity-100 transition-opacity duration-500`}
                />

                <CardHeader className="relative z-10">
                  <div
                    className={`w-14 h-14 rounded-xl ${feature.bgColor} flex items-center justify-center mb-4 group-hover:scale-110 group-hover:rotate-6 transition-all duration-500 shadow-lg`}
                  >
                    <Icon
                      className={`h-7 w-7 ${feature.iconColor} group-hover:scale-110 transition-transform duration-300`}
                    />
                  </div>
                  <CardTitle className="font-heading text-xl group-hover:text-primary transition-colors duration-300">
                    {feature.title}
                  </CardTitle>
                </CardHeader>
                <CardContent className="relative z-10">
                  <CardDescription className="text-base leading-relaxed group-hover:text-foreground/80 transition-colors duration-300">
                    {feature.description}
                  </CardDescription>
                </CardContent>

                <div className="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-500">
                  <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/10 to-transparent -translate-x-full group-hover:translate-x-full transition-transform duration-1000" />
                </div>
              </Card>
            );
          })}
        </div>
      </div>
    </section>
  );
}
