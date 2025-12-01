/**
 * Features Section - Landing Page
 *
 * Three-column grid showcasing key features with icons and descriptions.
 * Design: Playful Boardroom (wireframes-playful-boardroom.md)
 *
 * Features:
 * - Mobile: Single column stacked
 * - Tablet: 2 columns
 * - Desktop: 3 columns
 * - Icon + Title + Description cards
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md (lines 57-80)
 */

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Bot, BookOpen, Smartphone } from 'lucide-react';

const features = [
  {
    icon: Bot,
    title: 'AI Intelligente',
    description:
      'Risposte immediate con citazioni dal manuale. Niente più discussioni sulle regole.',
    color: 'text-primary',
  },
  {
    icon: BookOpen,
    title: 'Catalogo Ampio',
    description: 'Migliaia di giochi già disponibili. Aggiungi i tuoi manuali personalizzati.',
    color: 'text-secondary',
  },
  {
    icon: Smartphone,
    title: 'Mobile-First',
    description: 'Perfetto durante le partite. Risposte rapide senza interrompere il gioco.',
    color: 'text-accent',
  },
];

export function FeaturesSection() {
  return (
    <section id="features" className="py-16 md:py-24 px-4 bg-muted/30">
      <div className="container mx-auto max-w-6xl">
        {/* Section Header */}
        <div className="text-center mb-12">
          <h2 className="font-heading text-3xl md:text-4xl font-bold text-foreground mb-4">
            ✨ Caratteristiche
          </h2>
          <p className="text-lg text-muted-foreground max-w-2xl mx-auto">
            Tutto quello che ti serve per giocare senza dubbi
          </p>
        </div>

        {/* Features Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 md:gap-8">
          {features.map((feature, index) => {
            const Icon = feature.icon;
            return (
              <Card
                key={index}
                className="group hover:shadow-lg transition-all duration-300 hover:-translate-y-1 border-muted bg-card"
              >
                <CardHeader>
                  <div
                    className={`w-12 h-12 rounded-lg bg-primary/10 flex items-center justify-center mb-4 group-hover:bg-primary/20 transition-colors`}
                  >
                    <Icon className={`h-6 w-6 ${feature.color}`} />
                  </div>
                  <CardTitle className="font-heading text-xl">{feature.title}</CardTitle>
                </CardHeader>
                <CardContent>
                  <CardDescription className="text-base leading-relaxed">
                    {feature.description}
                  </CardDescription>
                </CardContent>
              </Card>
            );
          })}
        </div>
      </div>
    </section>
  );
}
