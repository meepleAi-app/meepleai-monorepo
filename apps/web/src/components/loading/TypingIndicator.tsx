/**
 * TypingIndicator Component
 *
 * Animated typing indicator for chat interfaces.
 * Shows 3 bouncing dots with staggered animation.
 * Respects user's reduced motion preferences.
 *
 * @example
 * ```tsx
 * <TypingIndicator
 *   visible={isTyping}
 *   agentName="AI Assistant"
 * />
 * ```
 */

import { motion, AnimatePresence } from 'framer-motion';
import { useReducedMotion } from '@/lib/animations';

export interface TypingIndicatorProps {
  /**
   * Whether the indicator is visible
   */
  visible: boolean;

  /**
   * Name of the agent that is typing (for accessibility)
   */
  agentName: string;

  /**
   * Additional CSS classes
   */
  className?: string;
}

/**
 * TypingIndicator component for chat interfaces
 */
export function TypingIndicator({
  visible,
  agentName,
  className = '',
}: TypingIndicatorProps) {
  const shouldReduceMotion = useReducedMotion();

  // Animation variants for container
  const containerVariants = {
    initial: { opacity: 0, scale: 0.95 },
    animate: { opacity: 1, scale: 1 },
    exit: { opacity: 0, scale: 0.95 },
  };

  // Animation variants for dots (bounce effect with stagger)
  const dotVariants = shouldReduceMotion
    ? {
        initial: { y: 0 },
        animate: { y: 0 },
      }
    : {
        initial: { y: 0 },
        animate: {
          y: [-2, 0, -2],
        },
      };

  // Transition configuration for dots
  const getDotTransition = (delay: number) =>
    shouldReduceMotion
      ? { duration: 0 }
      : {
          duration: 0.6,
          repeat: Infinity,
          ease: 'easeInOut' as const,
          delay,
        };

  // Stagger delays for each dot
  const staggerDelays = [0, 0.1, 0.2];

  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          role="status"
          aria-live="polite"
          aria-label={`${agentName} is typing`}
          variants={containerVariants}
          initial="initial"
          animate="animate"
          exit="exit"
          transition={{ duration: 0.2 }}
          className={`flex items-center space-x-1 px-4 py-2 bg-slate-100 dark:bg-slate-800 rounded-2xl w-fit ${className}`}
        >
          {/* Screen reader text */}
          <span className="sr-only">{agentName} is typing...</span>

          {/* Animated dots */}
          <div className="flex items-center space-x-1" aria-hidden="true">
            {staggerDelays.map((delay, index) => (
              <motion.span
                key={index}
                variants={dotVariants}
                initial="initial"
                animate="animate"
                transition={getDotTransition(delay)}
                className="w-2 h-2 bg-slate-500 dark:bg-slate-400 rounded-full"
              />
            ))}
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
