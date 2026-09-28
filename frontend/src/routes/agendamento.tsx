import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { AnimatePresence, motion } from "motion/react";
import { toast } from "sonner";
import { Check, ChevronLeft, Clock, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { SlotsSkeleton } from "@/components/site/Skeletons";
import { ConfirmDialog } from "@/components/site/ConfirmDialog";
import { BARBERS, SERVICES, barberById, brl, serviceById } from "@/lib/shop";
import { buildSlots, barberWorksOn, useStore } from "@/lib/store";

export const Route = createFileRoute("/agendamento")({
  head: () => ({
    meta: [
      { title: "Agendar horário — New Age" },
      {
        name: "description",
        content:
          "Escolha serviço, barbeiro, data e horário e confirme seu agendamento na New Age em poucos toques.",
      },
      { property: "og:title", content: "Agendar horário — New Age" },
      {
        property: "og:description",
        content: "Agendamento online de corte e barba na Várzea, Teresópolis.",
      },
    ],
  }),
  component: Booking,
});

const STEPS = ["Serviço", "Barbeiro", "Data e hora", "Revisão"];

function nextDays(count: number) {
  return Array.from({ length: count }).map((_, i) => {
    const d = new Date();
    d.setDate(d.getDate() + i);
    return {
      iso: d.toISOString().slice(0, 10),
      weekday: d.toLocaleDateString("pt-BR", { weekday: "short" }).replace(".", ""),
      day: d.getDate(),
      month: d.toLocaleDateString("pt-BR", { month: "short" }).replace(".", ""),
    };
  });
}

function Booking() {
  const { user, appointments, book } = useStore();
  const navigate = useNavigate();

  const [step, setStep] = useState(0);
  const [serviceId, setServiceId] = useState<string | null>(null);
  const [barberId, setBarberId] = useState<string | null>(null);
  const [date, setDate] = useState<string | null>(null);
  const [time, setTime] = useState<string | null>(null);
  const [loadingSlots, setLoadingSlots] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [done, setDone] = useState(false);

  const service = serviceId ? serviceById(serviceId) : undefined;
  const barber = barberId ? barberById(barberId) : undefined;
  const days = useMemo(() => nextDays(14), []);

  useEffect(() => {
    if (step !== 2 || !date) return;
    setLoadingSlots(true);
    const t = setTimeout(() => setLoadingSlots(false), 700);
    return () => clearTimeout(t);
  }, [step, date, barberId, serviceId]);

  const slots =
    barber && date && service ? buildSlots(barber, date, service.duration, appointments) : [];

  async function confirm() {
    if (!service || !barber || !date || !time) return;
    if (!user) {
      toast.error("Entre na sua conta para concluir o agendamento.");
      setConfirmOpen(false);
      navigate({ to: "/auth" });
      return;
    }
    try {
      await book({ serviceId: service.id, barberId: barber.id, date, time });
      setConfirmOpen(false);
      setDone(true);
      toast.success("Agendamento confirmado!", {
        description: `${service.name} com ${barber.name} — ${formatDate(date)} às ${time}.`,
      });
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Não foi possível agendar.");
    }
  }

  if (done && service && barber && date && time) {
    return (
      <motion.section
        initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3 }}
        className="mx-auto max-w-xl px-4 py-20 text-center sm:px-6"
      >
        <div className="mx-auto flex size-16 items-center justify-center rounded-full bg-primary/15">
          <Check className="size-8 text-primary" aria-hidden />
        </div>
        <h1 className="mt-6 font-display text-3xl">Horário reservado</h1>
        <p className="mt-3 text-sm text-muted-foreground">
          {service.name} com {barber.name}, {formatDate(date)} às {time}. Enviamos os
          detalhes para o seu WhatsApp.
        </p>
        <div className="mt-8 flex flex-wrap justify-center gap-3">
          <Button asChild>
            <Link to="/conta">Ver meus agendamentos</Link>
          </Button>
          <Button asChild variant="outline">
            <Link to="/">Voltar para a home</Link>
          </Button>
        </div>
      </motion.section>
    );
  }

  return (
    <section className="mx-auto max-w-3xl px-4 py-14 sm:px-6">
      <h1 className="font-display text-3xl sm:text-4xl">Agendar horário</h1>

      <ol className="mt-8 grid grid-cols-4 gap-2" aria-label="Etapas do agendamento">
        {STEPS.map((s, i) => (
          <li key={s} className="text-center">
            <div
              className={`mx-auto flex size-8 items-center justify-center rounded-full text-xs font-medium transition-colors ${
                i <= step
                  ? "bg-primary text-primary-foreground"
                  : "bg-secondary text-muted-foreground"
              }`}
              aria-current={i === step ? "step" : undefined}
            >
              {i + 1}
            </div>
            <span className="mt-2 block text-[11px] uppercase tracking-widest text-muted-foreground">
              {s}
            </span>
          </li>
        ))}
      </ol>

      <AnimatePresence mode="wait">
        <motion.div
          key={step}
          initial={{ opacity: 0, x: 16 }}
          animate={{ opacity: 1, x: 0 }}
          exit={{ opacity: 0, x: -16 }}
          transition={{ duration: 0.22, ease: "easeOut" }}
          className="mt-10"
        >
          {step === 0 && (
            <div className="grid gap-3 sm:grid-cols-2">
              {SERVICES.map((s) => (
                <button
                  key={s.id}
                  onClick={() => {
                    setServiceId(s.id);
                    setTime(null);
                    setStep(1);
                  }}
                  className={`surface-card rounded-xl p-5 text-left transition-colors hover:border-primary/60 ${
                    serviceId === s.id ? "border-primary" : ""
                  }`}
                >
                  <div className="flex items-center justify-between gap-3">
                    <h2 className="font-display text-lg">{s.name}</h2>
                    <span className="font-display text-lg text-primary">{brl(s.price)}</span>
                  </div>
                  <p className="mt-2 text-sm text-muted-foreground">{s.description}</p>
                  <p className="mt-3 flex items-center gap-1.5 text-xs text-muted-foreground">
                    <Clock className="size-3.5" aria-hidden /> {s.duration} min
                  </p>
                </button>
              ))}
            </div>
          )}

          {step === 1 && (
            <div className="grid gap-3 sm:grid-cols-3">
              {BARBERS.map((b) => (
                <button
                  key={b.id}
                  onClick={() => {
                    setBarberId(b.id);
                    setTime(null);
                    setStep(2);
                  }}
                  className={`surface-card overflow-hidden rounded-xl text-left transition-colors hover:border-primary/60 ${
                    barberId === b.id ? "border-primary" : ""
                  }`}
                >
                  <img
                    src={b.photo}
                    alt={`Retrato de ${b.name}`}
                    loading="lazy"
                    width={800}
                    height={1000}
                    className="aspect-4/5 w-full object-cover"
                  />
                  <div className="p-4">
                    <p className="font-display text-lg">{b.name}</p>
                    <p className="text-xs text-muted-foreground">{b.role}</p>
                  </div>
                </button>
              ))}
            </div>
          )}

          {step === 2 && (
            <div>
              <div className="flex gap-2 overflow-x-auto pb-2">
                {days.map((d) => {
                  const disabled = barber ? !barberWorksOn(barber, d.iso) : false;
                  return (
                    <button
                      key={d.iso}
                      disabled={disabled}
                      onClick={() => {
                        setDate(d.iso);
                        setTime(null);
                      }}
                      className={`min-w-16 shrink-0 rounded-lg border px-3 py-2 text-center text-sm transition-colors ${
                        date === d.iso
                          ? "border-primary bg-primary/10 text-primary"
                          : "border-border text-muted-foreground hover:border-primary/50"
                      } disabled:cursor-not-allowed disabled:opacity-35`}
                    >
                      <span className="block text-[10px] uppercase">{d.weekday}</span>
                      <span className="block font-display text-lg">{d.day}</span>
                      <span className="block text-[10px] uppercase">{d.month}</span>
                    </button>
                  );
                })}
              </div>

              <div className="mt-6">
                {!date ? (
                  <p className="text-sm text-muted-foreground">
                    Escolha um dia para ver os horários livres.
                  </p>
                ) : loadingSlots ? (
                  <SlotsSkeleton />
                ) : slots.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    Sem horários livres neste dia. Tente outra data.
                  </p>
                ) : (
                  <div className="grid grid-cols-3 gap-2 sm:grid-cols-4 md:grid-cols-6">
                    {slots.map((s) => (
                      <motion.button
                        key={s}
                        whileTap={{ scale: 0.96 }}
                        onClick={() => setTime(s)}
                        className={`h-11 rounded-lg border text-sm transition-colors ${
                          time === s
                            ? "border-primary bg-primary/10 text-primary"
                            : "border-border text-muted-foreground hover:border-primary/50"
                        }`}
                      >
                        {s}
                      </motion.button>
                    ))}
                  </div>
                )}
              </div>

              <Button className="mt-8" disabled={!time} onClick={() => setStep(3)}>
                Revisar agendamento
              </Button>
            </div>
          )}

          {step === 3 && service && barber && date && time && (
            <div className="surface-card rounded-xl p-6">
              <h2 className="font-display text-2xl">Revisão</h2>
              <dl className="mt-5 space-y-3 text-sm">
                <Row label="Serviço" value={`${service.name} · ${service.duration} min`} />
                <Row label="Barbeiro" value={barber.name} />
                <Row label="Data" value={formatDate(date)} />
                <Row label="Horário" value={time} />
                <Row label="Valor" value={brl(service.price)} />
              </dl>

              {!user && (
                <p className="mt-5 rounded-lg border border-primary/40 bg-primary/5 p-3 text-xs text-muted-foreground">
                  Você precisa estar conectado para confirmar.{" "}
                  <Link to="/auth" className="text-primary hover:underline">
                    Entrar ou criar conta
                  </Link>
                </p>
              )}

              <Button className="mt-6 w-full" onClick={() => setConfirmOpen(true)}>
                Confirmar agendamento
              </Button>
            </div>
          )}
        </motion.div>
      </AnimatePresence>

      {step > 0 && (
        <Button variant="ghost" className="mt-8" onClick={() => setStep(step - 1)}>
          <ChevronLeft className="size-4" aria-hidden />
          Voltar
        </Button>
      )}

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Confirmar agendamento"
        description={
          service && barber && date && time
            ? `${service.name} com ${barber.name} em ${formatDate(date)} às ${time}.`
            : ""
        }
        confirmLabel="Confirmar"
        onConfirm={confirm}
      />
    </section>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between border-b border-border/60 pb-2">
      <dt className="text-muted-foreground">{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

export function formatDate(iso: string) {
  return new Date(`${iso}T12:00:00`).toLocaleDateString("pt-BR", {
    weekday: "long",
    day: "2-digit",
    month: "long",
  });
}

export const Spinner = () => <Loader2 className="size-4 animate-spin" aria-hidden />;