import { createFileRoute, Link } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { AnimatePresence, motion } from "motion/react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { AppointmentListSkeleton } from "@/components/site/Skeletons";
import { ConfirmDialog } from "@/components/site/ConfirmDialog";
import { barberById, brl, serviceById } from "@/lib/shop";
import { buildSlots, useStore, type Appointment } from "@/lib/store";
import { formatDate } from "./agendamento";

export const Route = createFileRoute("/conta")({
  head: () => ({
    meta: [
      { title: "Minha conta — New Age" },
      {
        name: "description",
        content:
          "Veja, remarque ou cancele seus agendamentos, atualize seu perfil e sua senha na New Age.",
      },
      { property: "og:title", content: "Minha conta — New Age" },
      {
        property: "og:description",
        content: "Área do cliente da barbearia New Age.",
      },
    ],
  }),
  component: Account,
});

function Account() {
  const {
    ready,
    user,
    appointments,
    cancelAppointment,
    rescheduleAppointment,
    updateProfile,
    changePassword,
    deleteAccount,
  } = useStore();

  const [loading, setLoading] = useState(true);
  const [cancelTarget, setCancelTarget] = useState<Appointment | null>(null);
  const [rescheduleTarget, setRescheduleTarget] = useState<Appointment | null>(null);
  const [newDate, setNewDate] = useState("");
  const [newTime, setNewTime] = useState("");
  const [pwOpen, setPwOpen] = useState(false);
  const [newPassword, setNewPassword] = useState("");
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [profile, setProfile] = useState({ name: "", email: "", phone: "" });
  const [savingProfile, setSavingProfile] = useState(false);

  useEffect(() => {
    if (!ready) return;
    setProfile({ name: user?.name ?? "", email: user?.email ?? "", phone: user?.phone ?? "" });
    const t = setTimeout(() => setLoading(false), 700);
    return () => clearTimeout(t);
  }, [ready, user]);

  if (ready && !user) {
    return (
      <section className="mx-auto max-w-md px-4 py-20 text-center sm:px-6">
        <h1 className="font-display text-3xl">Área do cliente</h1>
        <p className="mt-3 text-sm text-muted-foreground">
          Entre na sua conta para ver seus agendamentos.
        </p>
        <Button asChild className="mt-6">
          <Link to="/auth">Entrar</Link>
        </Button>
      </section>
    );
  }

  const mine = appointments
    .filter((a) => a.userId === user?.id)
    .sort((a, b) => (a.date + a.time < b.date + b.time ? 1 : -1));

  const rescheduleSlots = (() => {
    if (!rescheduleTarget || !newDate) return [];
    const barber = barberById(rescheduleTarget.barberId);
    const service = serviceById(rescheduleTarget.serviceId);
    if (!barber || !service) return [];
    return buildSlots(barber, newDate, service.duration, appointments);
  })();

  return (
    <motion.section
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3, ease: "easeOut" }}
      className="mx-auto max-w-3xl px-4 py-14 sm:px-6"
    >
      <h1 className="font-display text-3xl sm:text-4xl">Olá, {user?.name.split(" ")[0]}</h1>
      <p className="mt-2 text-sm text-muted-foreground">
        Gerencie seus horários e seus dados de acesso.
      </p>

      <Tabs defaultValue="agendamentos" className="mt-8">
        <TabsList>
          <TabsTrigger value="agendamentos">Agendamentos</TabsTrigger>
          <TabsTrigger value="perfil">Perfil</TabsTrigger>
          <TabsTrigger value="seguranca">Segurança</TabsTrigger>
        </TabsList>

        <TabsContent value="agendamentos" className="mt-6">
          {loading ? (
            <AppointmentListSkeleton />
          ) : mine.length === 0 ? (
            <div className="surface-card rounded-xl p-8 text-center">
              <p className="text-sm text-muted-foreground">
                Você ainda não tem agendamentos.
              </p>
              <Button asChild className="mt-5">
                <Link to="/agendamento">Agendar agora</Link>
              </Button>
            </div>
          ) : (
            <AnimatePresence initial={false}>
              <div className="space-y-4">
                {mine.map((a) => {
                  const s = serviceById(a.serviceId);
                  const b = barberById(a.barberId);
                  return (
                    <motion.article
                      key={a.id}
                      layout
                      initial={{ opacity: 0, y: 10 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, y: -10 }}
                      transition={{ duration: 0.25 }}
                      className="surface-card rounded-xl p-5"
                    >
                      <div className="flex items-start justify-between gap-4">
                        <div>
                          <h2 className="font-display text-lg">{s?.name}</h2>
                          <p className="mt-1 text-sm text-muted-foreground">
                            {b?.name} · {formatDate(a.date)} às {a.time}
                          </p>
                          <p className="mt-1 text-sm text-primary">{brl(s?.price ?? 0)}</p>
                        </div>
                        <StatusBadge status={a.status} />
                      </div>

                      {a.status === "confirmado" && (
                        <div className="mt-5 flex flex-wrap gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => {
                              setRescheduleTarget(a);
                              setNewDate(a.date);
                              setNewTime("");
                            }}
                          >
                            Remarcar
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setCancelTarget(a)}
                          >
                            Cancelar
                          </Button>
                        </div>
                      )}
                    </motion.article>
                  );
                })}
              </div>
            </AnimatePresence>
          )}
        </TabsContent>

        <TabsContent value="perfil" className="mt-6">
          <form
            className="surface-card space-y-4 rounded-xl p-6"
            onSubmit={async (e) => {
              e.preventDefault();
              setSavingProfile(true);
              try {
                await updateProfile(profile);
                toast.success("Perfil atualizado.");
              } catch (err) {
                toast.error(err instanceof Error ? err.message : "Erro ao salvar.");
              } finally {
                setSavingProfile(false);
              }
            }}
          >
            <div className="space-y-2">
              <Label htmlFor="p-name">Nome</Label>
              <Input
                id="p-name"
                value={profile.name}
                onChange={(e) => setProfile({ ...profile, name: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="p-email">E-mail</Label>
              <Input
                id="p-email"
                type="email"
                value={profile.email}
                onChange={(e) => setProfile({ ...profile, email: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="p-phone">WhatsApp</Label>
              <Input
                id="p-phone"
                value={profile.phone}
                onChange={(e) => setProfile({ ...profile, phone: e.target.value })}
              />
            </div>
            <Button type="submit" disabled={savingProfile}>
              Salvar alterações
            </Button>
          </form>
        </TabsContent>

        <TabsContent value="seguranca" className="mt-6 space-y-4">
          <div className="surface-card rounded-xl p-6">
            <h2 className="font-display text-xl">Senha</h2>
            <p className="mt-2 text-sm text-muted-foreground">
              Alterar a senha exige a confirmação da senha atual.
            </p>
            <Button className="mt-5" onClick={() => setPwOpen(true)}>
              Alterar senha
            </Button>
          </div>

          <div className="surface-card rounded-xl p-6">
            <h2 className="font-display text-xl">Excluir conta</h2>
            <p className="mt-2 text-sm text-muted-foreground">
              Isso remove seus dados e agendamentos. A ação não pode ser desfeita.
            </p>
            <Button variant="destructive" className="mt-5" onClick={() => setDeleteOpen(true)}>
              Excluir minha conta
            </Button>
          </div>
        </TabsContent>
      </Tabs>

      {/* Cancelar */}
      <ConfirmDialog
        open={!!cancelTarget}
        onOpenChange={(o) => !o && setCancelTarget(null)}
        title="Cancelar agendamento"
        description={
          cancelTarget
            ? `${serviceById(cancelTarget.serviceId)?.name} em ${formatDate(cancelTarget.date)} às ${cancelTarget.time}.`
            : ""
        }
        confirmLabel="Cancelar horário"
        destructive
        onConfirm={async () => {
          if (!cancelTarget) return;
          try {
            await cancelAppointment(cancelTarget.id);
            toast.success("Agendamento cancelado.");
          } catch {
            toast.error("Não foi possível cancelar.");
          } finally {
            setCancelTarget(null);
          }
        }}
      />

      {/* Remarcar */}
      <ConfirmDialog
        open={!!rescheduleTarget}
        onOpenChange={(o) => !o && setRescheduleTarget(null)}
        title="Remarcar agendamento"
        description="Escolha uma nova data e horário disponíveis."
        confirmLabel="Remarcar"
        onConfirm={async () => {
          if (!rescheduleTarget || !newTime) {
            toast.error("Escolha um horário.");
            return;
          }
          try {
            await rescheduleAppointment(rescheduleTarget.id, newDate, newTime);
            toast.success("Agendamento remarcado.");
            setRescheduleTarget(null);
          } catch {
            toast.error("Não foi possível remarcar.");
          }
        }}
      >
        <div className="space-y-3">
          <div className="space-y-2">
            <Label htmlFor="new-date">Nova data</Label>
            <Input
              id="new-date"
              type="date"
              value={newDate}
              min={new Date().toISOString().slice(0, 10)}
              onChange={(e) => {
                setNewDate(e.target.value);
                setNewTime("");
              }}
            />
          </div>
          <div className="max-h-40 overflow-y-auto">
            {rescheduleSlots.length === 0 ? (
              <p className="text-xs text-muted-foreground">
                Sem horários livres nessa data.
              </p>
            ) : (
              <div className="grid grid-cols-4 gap-2">
                {rescheduleSlots.map((s) => (
                  <button
                    key={s}
                    type="button"
                    onClick={() => setNewTime(s)}
                    className={`h-10 rounded-md border text-sm transition-colors ${
                      newTime === s
                        ? "border-primary bg-primary/10 text-primary"
                        : "border-border text-muted-foreground hover:border-primary/50"
                    }`}
                  >
                    {s}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      </ConfirmDialog>

      {/* Alterar senha */}
      <ConfirmDialog
        open={pwOpen}
        onOpenChange={setPwOpen}
        title="Alterar senha"
        description="Informe a nova senha e confirme com a senha atual."
        confirmLabel="Alterar senha"
        requirePassword
        onConfirm={async (current) => {
          try {
            await changePassword(current, newPassword);
            toast.success("Senha alterada com sucesso.");
            setNewPassword("");
            setPwOpen(false);
          } catch (e) {
            toast.error(e instanceof Error ? e.message : "Erro ao alterar senha.");
          }
        }}
      >
        <div className="space-y-2">
          <Label htmlFor="new-password">Nova senha</Label>
          <Input
            id="new-password"
            type="password"
            autoComplete="new-password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
          />
        </div>
      </ConfirmDialog>

      {/* Excluir conta */}
      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Excluir conta"
        description="Todos os seus dados e agendamentos serão apagados."
        confirmLabel="Excluir definitivamente"
        destructive
        requirePassword
        onConfirm={async (password) => {
          try {
            await deleteAccount(password);
            toast.success("Conta excluída.");
            setDeleteOpen(false);
          } catch (e) {
            toast.error(e instanceof Error ? e.message : "Erro ao excluir conta.");
          }
        }}
      />
    </motion.section>
  );
}

function StatusBadge({ status }: { status: Appointment["status"] }) {
  const map = {
    confirmado: "border-primary/50 text-primary",
    cancelado: "border-destructive/50 text-destructive",
    concluido: "border-border text-muted-foreground",
  } as const;
  const label = { confirmado: "Confirmado", cancelado: "Cancelado", concluido: "Concluído" };
  return (
    <span
      className={`shrink-0 rounded-full border px-3 py-1 text-[11px] uppercase tracking-widest ${map[status]}`}
    >
      {label[status]}
    </span>
  );
}