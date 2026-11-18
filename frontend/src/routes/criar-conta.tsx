import { useRef, useState, type FormEvent } from "react";
import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { Check, Circle } from "lucide-react";
import { SegmentField } from "@/components/auth/SegmentField";
import { AuthShell } from "@/components/auth/AuthShell";
import { GuestOnly } from "@/components/auth/GuestOnly";
import { Alert, Button, PasswordField, SelectField, TextField } from "@/components/ui-sv";
import { useAuth } from "@/lib/auth/AuthProvider";
import { slugPattern } from "@/lib/api/contracts";
import { ApiError, IS_DEMO } from "@/lib/api/http";
import { SEGMENTS } from "@/lib/segments";
import {
  maskDocument,
  onlyDigits,
  PASSWORD_RULES,
  validateRegister,
  type RegisterForm,
} from "@/lib/validation";
import s from "@/components/auth/styles";

export const Route = createFileRoute("/criar-conta")({
  head: () => ({
    meta: [
      { title: "Criar conta — Servia" },
      {
        name: "description",
        content:
          "Cadastre seu estabelecimento no Servia e crie seu site usando um modelo do seu segmento.",
      },
      { property: "og:title", content: "Criar conta — Servia" },
      {
        property: "og:description",
        content: "Cadastre seu estabelecimento e personalize o site do seu estabelecimento.",
      },
      { property: "og:type", content: "website" },
      { name: "twitter:card", content: "summary" },
    ],
  }),
  component: () => (
    <GuestOnly>
      <SignUp />
    </GuestOnly>
  ),
});

const empty: RegisterForm = {
  establishmentName: "",
  address: "",
  segment: "",
  document: "",
  ownerName: "",
  email: "",
  password: "",
  confirmPassword: "",
};

function SignUp() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [slug, setSlug] = useState("");
  const [f, setF] = useState(empty);
  const [errors, setErrors] = useState<Partial<Record<keyof RegisterForm, string>>>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(false);
  const busy = useRef(false);

  const set = (k: keyof RegisterForm) => (e: { target: { value: string } }) => {
    const v = k === "document" ? maskDocument(e.target.value) : e.target.value;
    setF((p) => ({ ...p, [k]: v }));
    if (errors[k]) setErrors((p) => ({ ...p, [k]: undefined }));
  };

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (busy.current) return;
    const errs = validateRegister(f, IS_DEMO ? "demo" : "real");
    if (!IS_DEMO && !new RegExp(`^${slugPattern}$`).test(slug.trim())) {
      setFormError("Use 3 a 40 letras minúsculas, números ou hífens no identificador.");
      return;
    }
    setErrors(errs);
    setFormError("");
    if (Object.keys(errs).length) return;
    busy.current = true;
    setLoading(true);
    try {
      await register({
        ...f,
        slug: slug.trim(),
        document: f.document ? onlyDigits(f.document) : undefined,
        email: f.email.trim(),
      });
      void navigate({ to: "/onboarding", replace: true });
    } catch (err) {
      if (err instanceof ApiError) {
        const fields = err.fieldErrors;
        const mapped = { ...fields };
        if (fields["name"]) mapped["establishmentName"] = fields["name"];
        if (fields["ownerEmail"]) mapped["email"] = fields["ownerEmail"];
        if (fields["ownerPassword"]) mapped["password"] = fields["ownerPassword"];
        setErrors(mapped);
        setFormError(err.message);
      } else setFormError("Não foi possível criar a conta.");
    } finally {
      busy.current = false;
      setLoading(false);
    }
  }

  return (
    <AuthShell
      title="Crie sua conta"
      subtitle="Informe os dados iniciais do seu estabelecimento. Depois, personalize o site do seu estabelecimento."
    >
      <form method="post" className={s.form} onSubmit={onSubmit} noValidate>
        {formError && <Alert tone="error">{formError}</Alert>}
        <p className={s.groupLabel}>Estabelecimento</p>
        <TextField
          label="Nome do estabelecimento"
          name="establishmentName"
          maxLength={150}
          value={f.establishmentName}
          onChange={set("establishmentName")}
          error={errors.establishmentName}
          autoComplete="organization"
        />
        {IS_DEMO ? (
          <SelectField
            label="Segmento de atuação"
            value={f.segment}
            onChange={set("segment")}
            error={errors.segment}
            options={SEGMENTS}
            placeholder="Selecione"
          />
        ) : (
          <>
            <TextField
              label="Identificador do estabelecimento"
              name="slug"
              value={slug}
              onChange={(e) => setSlug(e.target.value)}
              maxLength={40}
              spellCheck={false}
              autoComplete="off"
            />
            <SegmentField value={f.segment} onChange={set("segment")} error={errors.segment} />
          </>
        )}
        {IS_DEMO && (
          <TextField
            label="Endereço"
            value={f.address}
            onChange={set("address")}
            error={errors.address}
            autoComplete="street-address"
            placeholder="Rua, número, bairro, cidade…"
          />
        )}
        <TextField
          label={IS_DEMO ? "CPF ou CNPJ (opcional)" : "CPF ou CNPJ"}
          value={f.document}
          onChange={set("document")}
          error={errors.document}
          inputMode="numeric"
          placeholder="000.000.000-00"
        />
        <p className={s.groupLabel}>Responsável</p>
        <TextField
          label="Seu nome"
          name="ownerName"
          maxLength={150}
          value={f.ownerName}
          onChange={set("ownerName")}
          error={errors.ownerName}
          autoComplete="name"
        />
        <TextField
          label="E-mail"
          name="email"
          maxLength={200}
          type="email"
          value={f.email}
          onChange={set("email")}
          error={errors.email}
          autoComplete="email"
        />
        <PasswordField
          label="Senha"
          name="password"
          maxLength={200}
          value={f.password}
          onChange={set("password")}
          error={errors.password}
          autoComplete="new-password"
          hint={
            IS_DEMO ? (
              <ul className={s.rules}>
                {PASSWORD_RULES.map((r) => {
                  const ok = r.test(f.password);
                  return (
                    <li key={r.id} className={ok ? s.ruleOk : ""}>
                      {ok ? <Check size={13} /> : <Circle size={11} />}
                      {r.label}
                    </li>
                  );
                })}
              </ul>
            ) : (
              "Use de 8 a 200 caracteres."
            )
          }
        />
        <PasswordField
          label="Confirme a senha"
          name="confirmPassword"
          maxLength={200}
          value={f.confirmPassword}
          onChange={set("confirmPassword")}
          error={errors.confirmPassword}
          autoComplete="new-password"
        />
        <Button type="submit" size="lg" block loading={loading}>
          {loading ? "Criando conta…" : "Criar conta"}
        </Button>
        <p className={s.alt}>
          Já tem conta?{" "}
          <Link to="/entrar" className={s.link}>
            Entrar
          </Link>
        </p>
      </form>
    </AuthShell>
  );
}
