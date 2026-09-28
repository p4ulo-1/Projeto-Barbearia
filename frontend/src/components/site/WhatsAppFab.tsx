import { motion, useReducedMotion } from "motion/react";
import { MessageCircle } from "lucide-react";
import { SHOP } from "@/lib/shop";

export function WhatsAppFab() {
  const reduce = useReducedMotion();
  const href = `https://wa.me/${SHOP.whatsapp}?text=${encodeURIComponent(
    "Olá! Gostaria de tirar uma dúvida sobre horários na New Age.",
  )}`;

  return (
    <motion.a
      href={href}
      target="_blank"
      rel="noopener noreferrer"
      aria-label="Falar com a barbearia pelo WhatsApp"
      initial={reduce ? false : { scale: 0.8, opacity: 0 }}
      animate={{ scale: 1, opacity: 1 }}
      transition={{ delay: 0.6, duration: 0.3, ease: "easeOut" }}
      {...(reduce ? {} : { whileHover: { scale: 1.06 }, whileTap: { scale: 0.95 } })}
      className="fixed bottom-5 right-5 z-50 flex size-14 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-[var(--shadow-gold)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background"
    >
      <MessageCircle className="size-6" aria-hidden />
    </motion.a>
  );
}