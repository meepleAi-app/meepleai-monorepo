🎬 1. ANIMAZIONE LOGO (core interactions)
🔁 Idle (stato base)
Cosa succede:
glow cyan “respira” (pulse lento)
occhi fanno un micro-blink ogni 4–6s
Spec:
durata: 2.5s loop
easing: ease-in-out
scale glow: 95% → 105%
👉 Effetto: vivo ma non distraente
👀 Blink (signature move)
Cosa succede:
occhi — — → · · → — —
Timing:
chiusura: 80ms
apertura: 120ms
👉 Importantissimo: NON esagerare → deve sembrare “consapevole”, non cartoon
⚡ Decision / Action (AI moment)
Quando l’AI “decide” qualcosa:
glow cyan aumenta
circuit pattern si illumina a onda
piccolo sparkle vicino alla corona
Durata:
~400–600ms
👉 Questo è il tuo “feedback AI”
🏆 Success state
corona fa micro bounce (2–3px)
glow amber più caldo
❌ Error / invalid move
occhi diventano leggermente più stretti
glow vira leggermente verso arancio scuro
micro shake (molto leggero)
🧩 2. UI KIT (base ma potente)
🎨 Color tokens
--bg-primary: #0f172a;
--bg-secondary: #1e293b;
--meeple-primary: #f97316;
--meeple-light: #f59e0b;
--accent-ai: #22d3ee;
--accent-ai-soft: #67e8f9;
--gold: #fbbf24;
🧱 Componenti chiave

1. AI Referee Avatar
   usa logo (primary o flat)
   stato:
   idle
   thinking
   responding
   👉 è il tuo “assistente visivo”
2. Decision Card
   Card che mostra mosse / consigli:
   bordo: glow leggero cyan
   hover: +10% glow
   selected: outline amber
3. Game Tile (coerenza brand)
   hex shape
   hover → glow cyan
   selected → glow amber
   👉 richiama il logo → branding consistente
4. CTA Button
   Primary:
   bg: gradient amber
   hover: più luminoso
   active: leggermente più scuro
   Secondary:
   outline cyan
   hover → fill leggero
   ⚙️ 3. Micro-interactions (queste fanno il prodotto)
   Hover
   glow compare (150ms)
   scale 1.02
   Click
   scale 0.97 → 1
   glow “snap”
   Loading AI
   occhi diventano puntini pulsanti
   glow ciclico
   🧠 4. Logica UX (importantissima)
   👉 Il meeple NON è decorazione
   è feedback system
   Usalo per:
   confermare azioni
   segnalare errori
   guidare l’utente

🧠 Come portarlo a livello PRO

1. Stati reali (fondamentale)

Collega lo stato del meeple a logica vera:

<MeepleAvatar state={isThinking ? "thinking" : "idle"} />

👉 esempio:

API call → thinking
risposta → idle 2. Aggiungi stato “decision”

Upgrade serio:

state === "decision"

Effetto:

glow più forte
micro bounce 3. Circuit pattern (step successivo)

Non metterlo in CSS random.

👉 approccio corretto:

SVG overlay dentro avatar
opacity 10–20%
animazione “pulse lines”

Se vuoi, te lo integro io pulito.

💣 Upgrade veri che valgono

Se vuoi fare salto serio:

🚀 1. Motion avanzata
eyes tracking (seguono hover)
glow reattivo al mouse
🧩 2. Game logic UI
tile selezionabili
suggerimenti AI dinamici
🧠 3. “Personality layer”
micro reazioni:
errore → occhi stretti
successo → crown bounce
🧠 Reasoning Summary
Ho trasformato il brand in componente riutilizzabile
Ho collegato animazioni a stati logici (non solo estetica)
Ho dato una base scalabile (UI + avatar integrati)
Ho mantenuto coerenza con il design system che abbiamo costruito

import { motion } from "framer-motion";

export default function AnimatedMeeple({ state = "idle" }) {
const isThinking = state === "thinking";

return (
<motion.div
className="relative w-24 h-24 rounded-3xl
bg-gradient-to-br from-orange-400 to-orange-500
flex items-center justify-center shadow-lg"

      animate={{
        scale: isThinking ? [1, 1.08, 1] : [1, 1.04, 1],
      }}

      transition={{
        repeat: Infinity,
        duration: isThinking ? 1.5 : 2.5,
        ease: "easeInOut",
      }}
    >
      {/* Glow */}
      <motion.div
        className="absolute inset-0 rounded-3xl blur-xl"
        style={{ background: "#22d3ee" }}
        animate={{
          opacity: isThinking ? [0.3, 0.6, 0.3] : [0.15, 0.3, 0.15],
        }}
        transition={{ repeat: Infinity, duration: 2 }}
      />

      {/* Crown */}
      <div className="absolute -top-3 text-yellow-400 text-xl">
        👑
      </div>

      {/* Eyes */}
      <div className="flex gap-2 z-10">
        <Eye />
        <Eye />
      </div>
    </motion.div>

);
}

function Eye() {
return (
<motion.div
className="w-5 h-1 bg-cyan-300 rounded-full"
animate={{
        scaleY: [1, 0.2, 1],
      }}
transition={{
        repeat: Infinity,
        duration: 4,
        ease: "easeInOut",
      }}
/>
);
}

import { motion } from "framer-motion";

export default function SignatureMeeple({ state = "idle" }) {
const isThinking = state === "thinking";

return (
<motion.div
className="relative w-28 h-28 rounded-3xl bg-gradient-to-br from-orange-400 to-orange-500 flex items-center justify-center"
animate={{ scale: isThinking ? [1, 1.1, 1] : [1, 1.05, 1] }}
transition={{ repeat: Infinity, duration: isThinking ? 1.4 : 2.6 }} >
{/_ Glow Aura _/}
<motion.div
className="absolute inset-0 rounded-3xl blur-2xl"
style={{ background: "#22d3ee" }}
animate={{ opacity: isThinking ? [0.3, 0.7, 0.3] : [0.15, 0.35, 0.15] }}
transition={{ repeat: Infinity, duration: 2 }}
/>

      {/* Circuit Flow */}
      <motion.svg
        className="absolute inset-0 opacity-30"
        viewBox="0 0 100 100"
      >
        <motion.path
          d="M20 80 L20 50 L40 50"
          stroke="#22d3ee"
          strokeWidth="2"
          fill="none"
          strokeDasharray="50"
          animate={{ strokeDashoffset: [50, 0] }}
          transition={{ repeat: Infinity, duration: 2, ease: "linear" }}
        />
        <motion.path
          d="M60 80 L60 60 L80 60"
          stroke="#22d3ee"
          strokeWidth="2"
          fill="none"
          strokeDasharray="50"
          animate={{ strokeDashoffset: [50, 0] }}
          transition={{ repeat: Infinity, duration: 2.2, ease: "linear" }}
        />
      </motion.svg>

      {/* Crown Bounce */}
      <motion.div
        className="absolute -top-4 text-yellow-400 text-2xl"
        animate={{ y: [0, -4, 0] }}
        transition={{ repeat: Infinity, duration: 2.5 }}
      >
        👑
      </motion.div>

      {/* Eyes */}
      <div className="flex gap-3 z-10">
        <Eye />
        <Eye />
      </div>
    </motion.div>

);
}

function Eye() {
return (
<motion.div
className="w-5 h-1 bg-cyan-300 rounded-full"
animate={{ scaleY: [1, 0.15, 1] }}
transition={{ repeat: Infinity, duration: 4, ease: "easeInOut" }}
/>
);
}
