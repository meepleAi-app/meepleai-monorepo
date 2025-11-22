import { motion } from "framer-motion";
import { useInView } from "react-intersection-observer";
import { Card } from "@/components/ui/card";
import { useTranslation } from "@/hooks/useTranslation";

export default function FeaturesSection() {
  const { t } = useTranslation();
  const { ref: featuresRef, inView: featuresInView } = useInView({
    triggerOnce: true,
    threshold: [0.1, 0.5, 0.9],
  });

  const features = [
    {
      icon: "📤",
      title: `1. ${t('home.features.upload.title')}`,
      description: t('home.features.upload.description'),
    },
    {
      icon: "💬",
      title: `2. ${t('home.features.ask.title')}`,
      description: t('home.features.ask.description'),
    },
    {
      icon: "⚡",
      title: `3. ${t('home.features.play.title')}`,
      description: t('home.features.play.description'),
    },
  ];

  return (
    <section
      id="features"
      ref={featuresRef}
      className="py-20 px-6 bg-gradient-to-b from-slate-950 to-slate-900"
    >
      <div className="max-w-6xl mx-auto">
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={featuresInView ? { opacity: 1, y: 0 } : {}}
          transition={{ duration: 0.6 }}
          className="text-center mb-16"
        >
          <h2 className="text-5xl font-bold mb-4">{t('home.features.title')}</h2>
        </motion.div>

        <div className="grid md:grid-cols-3 gap-8">
          {features.map((feature, index) => (
            <Card key={index}>
              <motion.div
                initial={{ opacity: 0, y: 20 }}
                animate={featuresInView ? { opacity: 1, y: 0 } : {}}
                transition={{ duration: 0.6, delay: index * 0.1 }}
                className="text-center p-8 hover:scale-105 transition-transform"
              >
                <div className="text-6xl mb-4">{feature.icon}</div>
                <h3 className="text-2xl font-semibold mb-3">{feature.title}</h3>
                <p className="text-slate-50 leading-relaxed">{feature.description}</p>
              </motion.div>
            </Card>
          ))}
        </div>
      </div>
    </section>
  );
}
