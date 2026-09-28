import { createFileRoute, Link } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";
import { motion, useReducedMotion } from "motion/react";
import gsap from "gsap";
import { useGSAP } from "@gsap/react";
import { Clock, MapPin, Quote, Star } from "lucide-react";
import { Button } from "@/components/ui/button";
import { GallerySkeleton } from "@/components/site/Skeletons";
import heroImg from "@/assets/hero.jpg";
import {
  BARBERS,
  GALLERY,
  SERVICES,
  SHOP,
  TESTIMONIALS,
  brl,
} from "@/lib/shop";

export const Route = createFileRoute("/")({
  head: () => ({
    meta: [
      { title: "New Age — Barbearia boutique na Várzea, Teresópolis" },
      {
        name: "description",
        content:
          "Corte, barba e navalha em ambiente boutique na Várzea, Teresópolis. Veja serviços, preços, equipe e agende online em poucos toques.",
      },
      { property: "og:title", content: "New Age — Barbearia boutique" },
      {
        property: "og:description",
        content:
          "Corte, barba e navalha na Várzea, Teresópolis, com agendamento online.",
      },
    ],
  }),
  component: Home,
});

const fadeUp = {
  initial: { opacity: 0, y: 18 },
  whileInView: { opacity: 1, y: 0 },
  viewport: { once: true, margin: "-80px" },
  transition: { duration: 0.4, ease: "easeOut" as const },
};

function Hero() {
  const scope = useRef<HTMLDivElement>(null);

  useGSAP(
    () => {
      if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
        gsap.set("[data-hero]", { opacity: 1, y: 0 });
        return;
      }
      const tl = gsap.timeline({ defaults: { ease: "power3.out" } });
      tl.from("[data-hero='eyebrow']", { opacity: 0, y: 14, duration: 0.5 })
        .from(
          "[data-hero='title'] .line",
          { opacity: 0, yPercent: 110, duration: 0.75, stagger: 0.12 },
          "-=0.25",
        )
        .from("[data-hero='text']", { opacity: 0, y: 16, duration: 0.5 }, "-=0.4")
        .from(
          "[data-hero='cta'] > *",
          { opacity: 0, y: 14, duration: 0.45, stagger: 0.1 },
          "-=0.3",
        )
        .from(
          "[data-hero='stat']",
          { opacity: 0, y: 12, duration: 0.4, stagger: 0.08 },
          "-=0.3",
        );
    },
    { scope },
  );

  return (
    <section ref={scope} className="relative isolate overflow-hidden">
      <img
        src={heroImg}
        alt="Interior da barbearia New Age com cadeira de couro e luz âmbar"
        width={1600}
        height={1104}
        className="absolute inset-0 -z-10 size-full object-cover opacity-45"
      />
      <div className="absolute inset-0 -z-10 bg-[var(--gradient-dark)] opacity-80" />

      <div className="mx-auto max-w-6xl px-4 py-24 sm:px-6 md:py-36">
        <p
          data-hero="eyebrow"
          className="text-xs uppercase tracking-[0.35em] text-primary"
        >
          Várzea · desde 2016
        </p>

        <h1
          data-hero="title"
          className="mt-6 font-display text-4xl leading-[1.05] sm:text-6xl md:text-7xl"
        >
          <span className="block overflow-hidden">
            <span className="line block">Corte de precisão,</span>
          </span>
          <span className="block overflow-hidden">
            <span className="line block text-gradient-gold">navalha de tradição.</span>
          </span>
        </h1>

        <p
          data-hero="text"
          className="mt-6 max-w-xl text-base text-muted-foreground sm:text-lg"
        >
          Uma barbearia boutique para quem trata o próprio visual como parte do
          ofício. Reserve seu horário em menos de um minuto.
        </p>

        <div data-hero="cta" className="mt-9 flex flex-wrap gap-3">
          <Button asChild size="lg">
            <Link to="/agendamento">Agendar horário</Link>
          </Button>
          <Button asChild size="lg" variant="outline">
            <a href="#servicos">Ver serviços e preços</a>
          </Button>
        </div>

        <dl className="mt-14 grid max-w-lg grid-cols-3 gap-6">
          {[
            ["9 anos", "de casa"],
            ["12k+", "cortes"],
            ["4,9", "no Google"],
          ].map(([v, l]) => (
            <div key={l} data-hero="stat">
              <dt className="font-display text-2xl text-primary sm:text-3xl">{v}</dt>
              <dd className="text-xs uppercase tracking-widest text-muted-foreground">
                {l}
              </dd>
            </div>
          ))}
        </dl>
      </div>
    </section>
  );
}

function Services() {
  const reduce = useReducedMotion();
  return (
    <section id="servicos" className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <motion.div {...fadeUp}>
        <p className="text-xs uppercase tracking-[0.3em] text-primary">Serviços</p>
        <h2 className="mt-3 font-display text-3xl sm:text-4xl">A carta da casa</h2>
      </motion.div>

      <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {SERVICES.map((s, i) => (
          <motion.article
            key={s.id}
            initial={{ opacity: 0, y: 18 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true, margin: "-60px" }}
            transition={{ duration: 0.35, delay: i * 0.05, ease: "easeOut" }}
            {...(reduce ? {} : { whileHover: { y: -4 } })}
            className="surface-card flex flex-col rounded-xl p-6"
          >
            <div className="flex items-start justify-between gap-3">
              <h3 className="font-display text-xl">{s.name}</h3>
              {s.highlight && (
                <span className="rounded-full border border-primary/40 px-2 py-0.5 text-[10px] uppercase tracking-widest text-primary">
                  Favorito
                </span>
              )}
            </div>
            <p className="mt-3 flex-1 text-sm text-muted-foreground">{s.description}</p>
            <div className="mt-6 flex items-center justify-between">
              <span className="font-display text-2xl text-primary">{brl(s.price)}</span>
              <span className="text-xs text-muted-foreground">{s.duration} min</span>
            </div>
          </motion.article>
        ))}
      </div>

      <motion.div {...fadeUp} className="mt-10">
        <Button asChild size="lg">
          <Link to="/agendamento">Escolher serviço e agendar</Link>
        </Button>
      </motion.div>
    </section>
  );
}

function Team() {
  return (
    <section className="border-y border-border/70 bg-card/30 py-20">
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <motion.div {...fadeUp}>
          <p className="text-xs uppercase tracking-[0.3em] text-primary">Equipe</p>
          <h2 className="mt-3 font-display text-3xl sm:text-4xl">Quem segura a navalha</h2>
        </motion.div>

        <div className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {BARBERS.map((b, i) => (
            <motion.div
              key={b.id}
              initial={{ opacity: 0, y: 18 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true, margin: "-60px" }}
              transition={{ duration: 0.35, delay: i * 0.06, ease: "easeOut" }}
              className="group surface-card overflow-hidden rounded-xl"
            >
              <div className="aspect-4/5 overflow-hidden">
                <img
                  src={b.photo}
                  alt={`Retrato de ${b.name}`}
                  loading="lazy"
                  width={800}
                  height={1000}
                  className="size-full object-cover transition-transform duration-500 group-hover:scale-105"
                />
              </div>
              <div className="p-5">
                <h3 className="font-display text-xl">{b.name}</h3>
                <p className="text-xs uppercase tracking-widest text-primary">{b.role}</p>
                <p className="mt-3 text-sm text-muted-foreground">{b.bio}</p>
              </div>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  );
}

function Gallery() {
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    const t = setTimeout(() => setLoading(false), 900);
    return () => clearTimeout(t);
  }, []);

  return (
    <section className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <motion.div {...fadeUp}>
        <p className="text-xs uppercase tracking-[0.3em] text-primary">Galeria</p>
        <h2 className="mt-3 font-display text-3xl sm:text-4xl">Dentro da barbearia</h2>
      </motion.div>

      <div className="mt-10">
        {loading ? (
          <GallerySkeleton />
        ) : (
          <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
            {GALLERY.map((g, i) => (
              <motion.img
                key={g.src}
                src={g.src}
                alt={g.alt}
                loading="lazy"
                width={900}
                height={900}
                initial={{ opacity: 0, scale: 0.98 }}
                animate={{ opacity: 1, scale: 1 }}
                transition={{ duration: 0.35, delay: i * 0.05 }}
                className="aspect-square w-full rounded-xl object-cover"
              />
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

function Testimonials() {
  return (
    <section className="border-y border-border/70 bg-card/30 py-20">
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <motion.div {...fadeUp}>
          <p className="text-xs uppercase tracking-[0.3em] text-primary">Depoimentos</p>
          <h2 className="mt-3 font-display text-3xl sm:text-4xl">O que dizem os clientes</h2>
        </motion.div>

        <div className="mt-10 grid gap-4 md:grid-cols-3">
          {TESTIMONIALS.map((t, i) => (
            <motion.blockquote
              key={t.name}
              initial={{ opacity: 0, y: 18 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true, margin: "-60px" }}
              transition={{ duration: 0.35, delay: i * 0.06, ease: "easeOut" }}
              className="surface-card rounded-xl p-6"
            >
              <Quote className="size-5 text-primary" aria-hidden />
              <p className="mt-4 text-sm text-muted-foreground">{t.text}</p>
              <footer className="mt-5">
                <div className="flex gap-0.5" aria-label={`${t.rating} de 5 estrelas`}>
                  {Array.from({ length: t.rating }).map((_, s) => (
                    <Star key={s} className="size-3.5 fill-primary text-primary" aria-hidden />
                  ))}
                </div>
                <p className="mt-2 text-sm font-medium">{t.name}</p>
                <p className="text-xs text-muted-foreground">{t.role}</p>
              </footer>
            </motion.blockquote>
          ))}
        </div>
      </div>
    </section>
  );
}

function Location() {
  return (
    <section className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <div className="grid gap-8 md:grid-cols-2">
        <motion.div {...fadeUp} className="surface-card rounded-xl p-7">
          <MapPin className="size-5 text-primary" aria-hidden />
          <h2 className="mt-4 font-display text-3xl">Onde nos encontrar</h2>
          <p className="mt-3 text-sm text-muted-foreground">{SHOP.address}</p>
          <p className="mt-1 text-sm text-muted-foreground">
            {SHOP.phoneLabel} · {SHOP.email}
          </p>
          <Button asChild variant="outline" className="mt-6">
            <a href={SHOP.maps} target="_blank" rel="noopener noreferrer">
              Abrir no mapa
            </a>
          </Button>
        </motion.div>

        <motion.div {...fadeUp} className="surface-card rounded-xl p-7">
          <Clock className="size-5 text-primary" aria-hidden />
          <h2 className="mt-4 font-display text-3xl">Horário</h2>
          <ul className="mt-4 space-y-3 text-sm">
            {SHOP.hours.map((h) => (
              <li
                key={h.day}
                className="flex justify-between border-b border-border/60 pb-2 text-muted-foreground"
              >
                <span>{h.day}</span>
                <span className="text-foreground">{h.open}</span>
              </li>
            ))}
          </ul>
        </motion.div>
      </div>
    </section>
  );
}

function Home() {
  return (
    <>
      <Hero />
      <Services />
      <Team />
      <Gallery />
      <Testimonials />
      <Location />
    </>
  );
}
