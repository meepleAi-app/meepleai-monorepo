import { motion } from "framer-motion";
import { useInView } from "react-intersection-observer";
import { Card } from "@/components/ui/card";

export default function TestimonialsSection() {
  const { ref: testimonialsRef, inView: testimonialsInView } = useInView({
    triggerOnce: true,
    threshold: [0.1, 0.5, 0.9],
  });

  const testimonials = [
    {
      quote: "MeepleAI has completely transformed our game nights. No more endless rulebook searches!",
      author: "Marco R.",
      role: "Board Game Enthusiast",
      avatar: "🎲",
    },
    {
      quote: "As a tournament organizer, having instant rule clarifications is invaluable. Highly recommended!",
      author: "Sofia L.",
      role: "Tournament Organizer",
      avatar: "🏆",
    },
    {
      quote: "The semantic search is incredibly accurate. It's like having a rules expert at the table.",
      author: "Giovanni M.",
      role: "Game Designer",
      avatar: "🎮",
    },
  ];

  return (
    <section ref={testimonialsRef} className="py-20 px-6 bg-gradient-to-b from-slate-900 to-slate-950">
      <div className="max-w-6xl mx-auto">
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={testimonialsInView ? { opacity: 1, y: 0 } : {}}
          transition={{ duration: 0.6 }}
          className="text-center mb-16"
        >
          <h2 className="text-5xl font-bold mb-4">What Players Say</h2>
          <p className="text-xl text-slate-50">
            Join thousands of gamers who trust MeepleAI
          </p>
        </motion.div>

        <div className="grid md:grid-cols-3 gap-8">
          {testimonials.map((testimonial, index) => (
            <Card key={index} className="bg-slate-900/50 border-slate-800">
              <motion.div
                initial={{ opacity: 0, y: 20 }}
                animate={testimonialsInView ? { opacity: 1, y: 0 } : {}}
                transition={{ duration: 0.6, delay: index * 0.1 }}
                className="p-6 space-y-4"
              >
                <div className="text-4xl">{testimonial.avatar}</div>
                <blockquote className="text-slate-50 italic leading-relaxed">
                  &quot;{testimonial.quote}&quot;
                </blockquote>
                <div className="pt-4 border-t border-slate-800">
                  <p className="font-semibold text-white">{testimonial.author}</p>
                  <p className="text-sm text-slate-400">{testimonial.role}</p>
                </div>
              </motion.div>
            </Card>
          ))}
        </div>
      </div>
    </section>
  );
}
