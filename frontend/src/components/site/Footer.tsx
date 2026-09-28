import { Link } from "@tanstack/react-router";
import { Instagram, Mail, MapPin, Phone } from "lucide-react";
import { SHOP } from "@/lib/shop";

export function Footer() {
  return (
    <footer className="border-t border-border/70 bg-card/40">
      <div className="mx-auto grid max-w-6xl gap-10 px-4 py-14 sm:px-6 md:grid-cols-3">
        <div>
          <h3 className="font-display text-2xl">{SHOP.name}</h3>
          <p className="mt-3 max-w-xs text-sm text-muted-foreground">
            {SHOP.tagline} na Várzea. Corte, barba e um cuidado que dura
            até a próxima visita.
          </p>
        </div>

        <div className="space-y-3 text-sm text-muted-foreground">
          <p className="font-medium text-foreground">Contato</p>
          <p className="flex items-start gap-2">
            <MapPin className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden />
            {SHOP.address}
          </p>
          <p className="flex items-center gap-2">
            <Phone className="size-4 text-primary" aria-hidden />
            {SHOP.phoneLabel}
          </p>
          <p className="flex items-center gap-2">
            <Mail className="size-4 text-primary" aria-hidden />
            {SHOP.email}
          </p>
          <p className="flex items-center gap-2">
            <Instagram className="size-4 text-primary" aria-hidden />
            @newagebarber
          </p>
        </div>

        <div className="space-y-3 text-sm text-muted-foreground">
          <p className="font-medium text-foreground">Horário</p>
          {SHOP.hours.map((h) => (
            <p key={h.day} className="flex justify-between gap-4">
              <span>{h.day}</span>
              <span>{h.open}</span>
            </p>
          ))}
        </div>
      </div>

      <div className="border-t border-border/70 py-6 text-center text-xs text-muted-foreground">
        © {new Date().getFullYear()} {SHOP.name}. Todos os direitos reservados. ·{" "}
        <Link to="/agendamento" className="text-primary hover:underline">
          Agende seu horário
        </Link>
      </div>
    </footer>
  );
}