import { useRef, useState, type FormEvent } from "react";
import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { AuthShell } from "@/components/auth/AuthShell";
import { GuestOnly } from "@/components/auth/GuestOnly";
import { Alert, Button, PasswordField, TextField } from "@/components/ui-sv";
import { homeFor, useAuth } from "@/lib/auth/AuthProvider";
import { tenantPattern } from "@/lib/api/contracts";
import { ApiError, IS_DEMO } from "@/lib/api/http";
import { isEmail } from "@/lib/validation";
import s from "@/components/auth/styles";

export const Route = createFileRoute("/entrar")({
  head: () => ({
    meta: [
      { title: "Entrar — Servia" },
      { name: "description", content: "Acesse o painel do seu estabelecimento no Servia." },
      { property: "og:title", content: "Entrar — Servia" },
      { property: "og:description", content: "Acesse o painel do seu estabelecimento no Servia." },
      { property: "og:type", content: "website" },
      { name: "twitter:card", content: "summary" },
    ],
  }),
  component: () => (
    <GuestOnly>
      <SignIn />
    </GuestOnly>
  ),
});

function SignIn() {
  const { login, sessionExpired } = useAuth();
  const navigate = useNavigate();
  const [tenantId, setTenantId] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [remember, setRemember] = useState(false);
  const [errors, setErrors] = useState<{
    email?: string | undefined;
    password?: string | undefined;
  }>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(false);
  const busy = useRef(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (busy.current) return;
    const errs = {
      email: isEmail(email) ? undefined : "Informe um e-mail válido.",
      password: password ? undefined : "Informe sua senha.",
    };
    setErrors(errs);
    setFormError("");
    if (errs.email || errs.password) return;
    if (!IS_DEMO && !new RegExp(`^${tenantPattern}$`).test(tenantId.trim())) {
      setFormError("Informe o ID do estabelecimento no formato UUID.");
      return;
    }
    busy.current = true;
    setLoading(true);
    try {
      const user = await login(
        { email: email.trim(), password, ...(!IS_DEMO ? { tenantId: tenantId.trim() } : {}) },
        remember,
      );
      void navigate({ to: homeFor(user), replace: true });
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : "Não foi possível entrar.");
    } finally {
      busy.current = false;
      setLoading(false);
    }
  }

  return (
    <AuthShell title="Bem-vindo de volta" subtitle="Entre para gerenciar seu estabelecimento.">
      <form method="post" className={s.form} onSubmit={onSubmit} noValidate>
        {sessionExpired && !formError && (
          <Alert tone="warning">Sua sessão expirou. Entre novamente.</Alert>
        )}
        {formError && <Alert tone="error">{formError}</Alert>}
        {!IS_DEMO && (
          <TextField
            label="ID do estabelecimento"
            name="tenantId"
            value={tenantId}
            onChange={(e) => setTenantId(e.target.value)}
            autoComplete="off"
            spellCheck={false}
          />
        )}
        <TextField
          name="email"
          label="E-mail"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          error={errors.email}
          autoComplete="email"
        />
        <PasswordField
          name="password"
          label="Senha"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          error={errors.password}
          autoComplete="current-password"
        />
        <div className={s.between}>
          {IS_DEMO && (
            <label className={s.check}>
              <input
                type="checkbox"
                checked={remember}
                onChange={(e) => setRemember(e.target.checked)}
              />{" "}
              Manter conectado
            </label>
          )}
          <Link to="/recuperar-senha" className={s.link}>
            Esqueci minha senha
          </Link>
        </div>
        <Button type="submit" size="lg" block loading={loading}>
          {loading ? "Entrando…" : "Entrar"}
        </Button>
        <p className={s.alt}>
          Ainda não tem conta?{" "}
          <Link to="/criar-conta" className={s.link}>
            Criar conta
          </Link>
        </p>
      </form>
    </AuthShell>
  );
}
