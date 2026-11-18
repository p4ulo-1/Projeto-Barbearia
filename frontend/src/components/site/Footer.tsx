import { Link } from "@tanstack/react-router";
import { ArrowRight } from "lucide-react";
import { Wordmark } from "./Wordmark";
import s from "./styles";

export function FinalCta() {
  return (
    <section className={s.ctaSection}>
      <div className={`${s.container} ${s.cta}`}>
        <h2 className={s.h2}>Seu próximo passo começa aqui.</h2>
        <p>Leve seu estabelecimento para outro nível com um site profissional, agendamentos online e gestão integrada.</p>
        <Link to="/criar-conta" className={`${s.btn} ${s.btnLight} ${s.btnLg}`}>Começar agora <ArrowRight size={18} /></Link>
      </div>
    </section>
  );
}

export function Footer() {
  return (
    <footer className={s.footer}>
      <div className={`${s.container} ${s.footerGrid}`}>
        <div className={s.footerBrand}>
          <Wordmark inverted />
          <p>Presença digital, agendamento e gestão para estabelecimentos de serviços.</p>
        </div>
        <div><h4>Produto</h4><a href="#funcionalidades">Funcionalidades</a><a href="#como-funciona">Como funciona</a><a href="#planos">Planos</a><a href="#faq">FAQ</a></div>
        <div><h4>Conta</h4><Link to="/entrar">Entrar</Link><Link to="/criar-conta">Criar conta</Link></div>
        {/* TODO: substituir pelos destinos oficiais (contato, termos, privacidade) */}
        <div><h4>Servia</h4><a href="mailto:contato@servia.com.br">Contato</a><a href="#">Termos de uso</a><a href="#">Política de privacidade</a></div>
      </div>
      <div className={`${s.container} ${s.footerBottom}`}>© {new Date().getFullYear()} Servia. Todos os direitos reservados.</div>
    </footer>
  );
}
