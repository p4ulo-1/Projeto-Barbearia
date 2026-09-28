import { createFileRoute, Link } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { AnimatePresence, motion } from "motion/react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { TableRowsSkeleton } from "@/components/site/Skeletons";
import { ConfirmDialog } from "@/components/site/ConfirmDialog";
import { SERVICES, barberById, brl, serviceById, type Service } from "@/lib/shop";
import { useStore, type Appointment } from "@/lib/store";
import { formatDate } from "./agendamento";

export const Route = createFileRoute("/admin")({
  head: () => ({
    meta: [
      { title: "Painel do barbeiro — New Age" },
      {
        name: "description",
        content:
          "Gerencie a agenda do dia, os serviços da carta e a base de clientes da New Age.",
      },
      { property: "og:title", content: "Painel do barbeiro — New Age" },
      {
        property: "og:description",
        content: "Agenda, serviços e clientes da barbearia New Age.",
      },
    ],
  }),
  component: Admin,
});

function Admin() {
  const { ready, user, users, appointments, completeAppointment, removeAppointment } =
    useStore();
  const [loading, setLoading] = useState(true);
  const [services, setServices] = useState<Service[]>(SERVICES);
  const [removeTarget, setRemoveTarget] = useState<Appointment | null>(null);
  const [serviceTarget, setServiceTarget] = useState<Service | null>(null);

  useEffect(() => {
    const t = setTimeout(() => setLoading(false), 700);
    return () => clearTimeout(t);
  }, []);

  if (ready && user?.role !== "admin") {
    return (
      <section className="mx-auto max-w-md px-4 py-20 text-center sm:px-6">
        <h1 className="font-display text-3xl">Painel restrito</h1>
        <p className="mt-3 text-sm text-muted-foreground">
          Entre com uma conta de barbeiro ou proprietário para acessar o painel.
        </p>
        <Button asChild className="mt-6">
          <Link to="/auth">Entrar</Link>
        </Button>
      </section>
    );
  }

  const agenda = [...appointments].sort((a, b) =>
    a.date + a.time > b.date + b.time ? 1 : -1,
  );

  return (
    <motion.section
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3, ease: "easeOut" }}
      className="mx-auto max-w-5xl px-4 py-14 sm:px-6"
    >
      <h1 className="font-display text-3xl sm:text-4xl">Painel da barbearia</h1>
      <p className="mt-2 text-sm text-muted-foreground">
        Agenda, carta de serviços e clientes cadastrados.
      </p>

      <Tabs defaultValue="agenda" className="mt-8">
        <TabsList>
          <TabsTrigger value="agenda">Agenda</TabsTrigger>
          <TabsTrigger value="servicos">Serviços</TabsTrigger>
          <TabsTrigger value="clientes">Clientes</TabsTrigger>
        </TabsList>

        <TabsContent value="agenda" className="mt-6">
          {loading ? (
            <TableRowsSkeleton cols={4} rows={5} />
          ) : (
            <AnimatePresence initial={false}>
              <div className="space-y-3">
                {agenda.map((a) => (
                  <motion.div
                    key={a.id}
                    layout
                    initial={{ opacity: 0, y: 8 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -8 }}
                    transition={{ duration: 0.22 }}
                    className="surface-card grid gap-3 rounded-xl p-4 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center"
                  >
                    <div className="min-w-0">
                      <p className="truncate font-medium">
                        {a.userName} · {serviceById(a.serviceId)?.name}
                      </p>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {barberById(a.barberId)?.name} · {formatDate(a.date)} às {a.time} ·{" "}
                        <span className="uppercase tracking-widest">{a.status}</span>
                      </p>
                    </div>
                    <div className="flex gap-2">
                      {a.status === "confirmado" && (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={async () => {
                            await completeAppointment(a.id);
                            toast.success("Atendimento concluído.");
                          }}
                        >
                          Concluir
                        </Button>
                      )}
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => setRemoveTarget(a)}
                      >
                        Excluir
                      </Button>
                    </div>
                  </motion.div>
                ))}
              </div>
            </AnimatePresence>
          )}
        </TabsContent>

        <TabsContent value="servicos" className="mt-6">
          {loading ? (
            <TableRowsSkeleton cols={3} rows={4} />
          ) : (
            <div className="space-y-3">
              {services.map((s) => (
                <div
                  key={s.id}
                  className="surface-card grid gap-3 rounded-xl p-4 sm:grid-cols-[minmax(0,1fr)_auto_auto] sm:items-center"
                >
                  <div className="min-w-0">
                    <p className="truncate font-medium">{s.name}</p>
                    <p className="text-sm text-muted-foreground">{s.duration} min</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Label htmlFor={`price-${s.id}`} className="text-xs text-muted-foreground">
                      Preço
                    </Label>
                    <Input
                      id={`price-${s.id}`}
                      type="number"
                      className="w-24"
                      value={s.price}
                      onChange={(e) =>
                        setServices((prev) =>
                          prev.map((p) =>
                            p.id === s.id ? { ...p, price: Number(e.target.value) } : p,
                          ),
                        )
                      }
                    />
                  </div>
                  <div className="flex gap-2">
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => toast.success(`${s.name} salvo por ${brl(s.price)}.`)}
                    >
                      Salvar
                    </Button>
                    <Button size="sm" variant="ghost" onClick={() => setServiceTarget(s)}>
                      Remover
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="clientes" className="mt-6">
          {loading ? (
            <TableRowsSkeleton cols={3} rows={4} />
          ) : (
            <div className="space-y-3">
              {users
                .filter((u) => u.role === "client")
                .map((u) => (
                  <div key={u.id} className="surface-card rounded-xl p-4">
                    <p className="font-medium">{u.name}</p>
                    <p className="text-sm text-muted-foreground">
                      {u.email} · {u.phone}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {appointments.filter((a) => a.userId === u.id).length} agendamento(s)
                    </p>
                  </div>
                ))}
            </div>
          )}
        </TabsContent>
      </Tabs>

      <ConfirmDialog
        open={!!removeTarget}
        onOpenChange={(o) => !o && setRemoveTarget(null)}
        title="Excluir agendamento"
        description={
          removeTarget
            ? `${removeTarget.userName} — ${formatDate(removeTarget.date)} às ${removeTarget.time}.`
            : ""
        }
        confirmLabel="Excluir"
        destructive
        requirePassword
        onConfirm={async (password) => {
          if (password !== user?.password) {
            toast.error("Senha incorreta.");
            return;
          }
          if (!removeTarget) return;
          await removeAppointment(removeTarget.id);
          toast.success("Agendamento excluído.");
          setRemoveTarget(null);
        }}
      />

      <ConfirmDialog
        open={!!serviceTarget}
        onOpenChange={(o) => !o && setServiceTarget(null)}
        title="Remover serviço"
        description={
          serviceTarget ? `${serviceTarget.name} sairá da carta da casa.` : ""
        }
        confirmLabel="Remover"
        destructive
        requirePassword
        onConfirm={async (password) => {
          if (password !== user?.password) {
            toast.error("Senha incorreta.");
            return;
          }
          setServices((prev) => prev.filter((p) => p.id !== serviceTarget?.id));
          toast.success("Serviço removido da carta.");
          setServiceTarget(null);
        }}
      />
    </motion.section>
  );
}