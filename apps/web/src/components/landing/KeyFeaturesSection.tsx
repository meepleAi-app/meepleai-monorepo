import { motion } from "framer-motion";
import { useInView } from "react-intersection-observer";
import { Card } from "@/components/ui/card";

export default function KeyFeaturesSection() {
  const { ref: keyFeaturesRef, inView: keyFeaturesInView } = useInView({
    triggerOnce: true,
    threshold: [0.1, 0.5, 0.9],
  });

  const keyFeatures = [
    {
      icon: "🎯",
      title: "Semantic Search",
      description:
        "Advanced AI understands context and meaning, not just keywords. Ask complex questions and get accurate answers.",
    },
    {
      icon: "📚",
      title: "Multi-Game Support",
      description:
        "Upload rulebooks for chess, complex board games, TCGs, and more. Switch between games seamlessly.",
    },
    {
      icon: "🔍",
      title: "Source Citations",
      description:
        "Every answer includes exact page numbers and sections. Trust but verify with direct source references.",
    },
    {
      icon: "⚙️",
      title: "RuleSpec Editor",
      description:
        "Create machine-readable rule specifications. Perfect for game designers and tournament organizers.",
    },
  ];

  return (
    <section ref={keyFeaturesRef} className="py-20 px-6 bg-slate-950">
      <div className="max-w-6xl mx-auto">
        <div className="grid md:grid-cols-2 gap-6">
          {keyFeatures.map((feature, index) => (
            <Card key={index}>
              <motion.div
                initial={{ opacity: 0, x: index % 2 === 0 ? -20 : 20 }}
                animate={keyFeaturesInView ? { opacity: 1, x: 0 } : {}}
                transition={{ duration: 0.6, delay: index * 0.1 }}
                className="p-6 hover:border-primary/50 transition-colors"
              >
                <h3 className="text-xl font-semibold mb-2 flex items-center gap-2">
                  <span className="text-2xl">{feature.icon}</span>
                  {feature.title}
                </h3>
                <p className="text-slate-50">{feature.description}</p>
              </motion.div>
            </Card>
          ))}
        </div>
      </div>
    </section>
  );
}
