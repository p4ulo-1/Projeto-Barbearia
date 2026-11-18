import { useEffect, useState } from "react";
import { Link } from "@tanstack/react-router";
import { Menu, X } from "lucide-react";
import { Wordmark } from "./Wordmark";
import s from "./styles";

const links = [
  ["#funcionalidades", "Funcionalidades"],
  ["#como-funciona", "Como funciona"],
  ["#segmentos", "Segmentos"],
  ["#planos", "Planos"],
  ["#faq", "FAQ"],
];

export function Header() {
  const [open, setOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);
  useEffect(() => {
    const on = () => setScrolled(window.scrollY > 8);
    on();
    window.addEventListener("scroll", on, { passive: true });
    return () => window.removeEventListener("scroll", on);
  }, []);
  return (
    <header className={`${s.header} ${scrolled || open ? s.headerScrolled : ""}`}>
      <div className={`${s.container} ${s.headerInner}`}>
        <a href="#topo" aria-label="Servia — início"><Wordmark /></a>
        <nav className={s.nav} aria-label="Principal">
          {links.map(([h, l]) => <a key={h} href={h}>{l}</a>)}
        </nav>
        <div className={s.headerActions}>
          <Link to="/entrar" className={s.linkBtn}>Entrar</Link>
          <Link to="/criar-conta" className={`${s.btn} ${s.btnPrimary}`}>Começar agora</Link>
        </div>
        <button className={s.menuBtn} aria-label={open ? "Fechar menu" : "Abrir menu"} aria-expanded={open} onClick={() => setOpen(!open)}>
          {open ? <X size={22} /> : <Menu size={22} />}
        </button>
      </div>
      {open && (
        <div className={s.mobileMenu}>
          {links.map(([h, l]) => <a key={h} href={h} onClick={() => setOpen(false)}>{l}</a>)}
          <Link to="/entrar" className={`${s.btn} ${s.btnGhost}`}>Entrar</Link>
          <Link to="/criar-conta" className={`${s.btn} ${s.btnPrimary}`}>Começar agora</Link>
        </div>
      )}
    </header>
  );
}
