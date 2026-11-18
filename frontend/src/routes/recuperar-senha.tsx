import { createFileRoute, Link } from "@tanstack/react-router";
import { AuthShell } from "@/components/auth/AuthShell";
import { Alert } from "@/components/ui-sv";
import s from "@/components/auth/styles";

export const Route = createFileRoute("/recuperar-senha")({
  component: () => (
    <AuthShell title="Recuperar acesso">
      <Alert>Para recuperar o acesso, fale com o responsável pelo seu estabelecimento.</Alert>
      <Link to="/entrar" className={s.link}>
        Voltar para o login
      </Link>
    </AuthShell>
  ),
});
