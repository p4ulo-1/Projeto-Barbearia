import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { motion } from "motion/react";
import { toast } from "sonner";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useStore } from "@/lib/store";

export const Route = createFileRoute("/auth")({
  head: () => ({
    meta: [
      { title: "Entrar ou criar conta — New Age" },
      {
        name: "description",
        content:
          "Acesse sua conta na New Age para agendar, remarcar ou cancelar horários.",
      },
      { property: "og:title", content: "Entrar — New Age" },
      {
        property: "og:description",
        content: "Login e cadastro de clientes da barbearia New Age.",
      },
    ],
  }),
  component: AuthPage,
});

function AuthPage() {
  const { signIn, signUp, user } = useStore();
  const navigate = useNavigate();

  const [loginOpen, setLoginOpen] = useState(false);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);

  const [form, setForm] = useState({ name: "", email: "", phone: "", password: "" });

  async function handleLogin() {
    setLoading(true);
    try {
      const u = await signIn(email, password);
      toast.success(`Bem-vindo de volta, ${u.name.split(" ")[0]}!`);
      setLoginOpen(false);
      navigate({ to: u.role === "admin" ? "/admin" : "/conta" });
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Não foi possível entrar.");
    } finally {
      setLoading(false);
    }
  }

  async function handleSignUp(e: React.FormEvent) {
    e.preventDefault();
    if (form.password.length < 6) {
      toast.error("A senha precisa ter ao menos 6 caracteres.");
      return;
    }
    setLoading(true);
    try {
      await signUp(form);
      toast.success("Conta criada! Agora é só escolher um horário.");
      navigate({ to: "/agendamento" });
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Erro ao criar conta.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <motion.section
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3, ease: "easeOut" }}
      className="mx-auto max-w-md px-4 py-16 sm:px-6"
    >
      <h1 className="font-display text-3xl">Sua conta</h1>
      <p className="mt-2 text-sm text-muted-foreground">
        {user
          ? `Você já está conectado como ${user.name}.`
          : "Entre para gerenciar seus agendamentos ou crie sua conta em 30 segundos."}
      </p>

      <Tabs defaultValue="entrar" className="mt-8">
        <TabsList className="w-full">
          <TabsTrigger value="entrar" className="flex-1">
            Entrar
          </TabsTrigger>
          <TabsTrigger value="cadastrar" className="flex-1">
            Criar conta
          </TabsTrigger>
        </TabsList>

        <TabsContent value="entrar" className="mt-6">
          <div className="surface-card rounded-xl p-6">
            <p className="text-sm text-muted-foreground">
              Por segurança, a senha é pedida em uma janela dedicada.
            </p>
            <Button className="mt-5 w-full" onClick={() => setLoginOpen(true)}>
              Entrar com e-mail e senha
            </Button>
            <p className="mt-4 text-xs text-muted-foreground">
              Demonstração — cliente: cliente@exemplo.com / cliente123 · admin:
              admin@newagebarber.com.br / newage123
            </p>
          </div>
        </TabsContent>

        <TabsContent value="cadastrar" className="mt-6">
          <form onSubmit={handleSignUp} className="surface-card space-y-4 rounded-xl p-6">
            <div className="space-y-2">
              <Label htmlFor="su-name">Nome completo</Label>
              <Input
                id="su-name"
                required
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="su-email">E-mail</Label>
              <Input
                id="su-email"
                type="email"
                required
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="su-phone">WhatsApp</Label>
              <Input
                id="su-phone"
                required
                placeholder="(21) 99999-0000"
                value={form.phone}
                onChange={(e) => setForm({ ...form, phone: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="su-password">Senha</Label>
              <Input
                id="su-password"
                type="password"
                required
                autoComplete="new-password"
                value={form.password}
                onChange={(e) => setForm({ ...form, password: e.target.value })}
              />
            </div>
            <Button type="submit" className="w-full" disabled={loading}>
              {loading && <Loader2 className="size-4 animate-spin" aria-hidden />}
              Criar conta
            </Button>
          </form>
        </TabsContent>
      </Tabs>

      <Dialog open={loginOpen} onOpenChange={setLoginOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="font-display text-xl">Entrar</DialogTitle>
            <DialogDescription>
              Informe seu e-mail e senha para acessar seus agendamentos.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="li-email">E-mail</Label>
              <Input
                id="li-email"
                type="email"
                autoComplete="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="li-password">Senha</Label>
              <Input
                id="li-password"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") void handleLogin();
                }}
              />
            </div>
          </div>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="ghost" onClick={() => setLoginOpen(false)} disabled={loading}>
              Cancelar
            </Button>
            <Button onClick={() => void handleLogin()} disabled={loading}>
              {loading && <Loader2 className="size-4 animate-spin" aria-hidden />}
              Entrar
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </motion.section>
  );
}