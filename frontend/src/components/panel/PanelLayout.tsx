import { useEffect, useRef, useState, type ReactNode } from "react";
import { Link, useLocation, useNavigate } from "@tanstack/react-router";
import {
  ChevronDown,
  LogOut,
  Menu,
  PanelLeftClose,
  PanelLeftOpen,
  Settings,
  X,
} from "lucide-react";
import { Wordmark } from "@/components/site/Wordmark";
import { DemoBadge } from "@/components/ui-sv";
import { useAuth } from "@/lib/auth/AuthProvider";
import { canAccess, ROLE_LABEL } from "@/lib/permissions";
import { ThemeToggle } from "./ThemeToggle";
import { NAV } from "./nav";
import s from "./styles";

const COLLAPSE_KEY = "servia.sidebar-collapsed";

export function PanelLayout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const closeNav = useRef<HTMLButtonElement>(null);
  const openNav = useRef<HTMLButtonElement>(null);
  const profileButton = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    setCollapsed(localStorage.getItem(COLLAPSE_KEY) === "1");
  }, []);
  useEffect(() => {
    setMobileOpen(false);
    setMenuOpen(false);
  }, [pathname]);
  useEffect(() => {
    if (!menuOpen) return;
    const close = (e: MouseEvent) => {
      if (!menuRef.current?.contains(e.target as Node)) setMenuOpen(false);
    };
    const esc = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setMenuOpen(false);
        profileButton.current?.focus();
      }
    };
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", esc);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("keydown", esc);
    };
  }, [menuOpen]);

  useEffect(() => {
    if (!mobileOpen) return;
    closeNav.current?.focus();
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setMobileOpen(false);
        requestAnimationFrame(() => openNav.current?.focus());
      }
    };
    document.addEventListener("keydown", closeOnEscape);
    return () => document.removeEventListener("keydown", closeOnEscape);
  }, [mobileOpen]);

  const toggleCollapse = () => {
    const v = !collapsed;
    setCollapsed(v);
    localStorage.setItem(COLLAPSE_KEY, v ? "1" : "0");
  };
  const items = NAV.filter((n) => canAccess(user, n.section));
  const current =
    [...NAV].reverse().find((n) => pathname === n.to || pathname.startsWith(`${n.to}/`)) ?? NAV[0];
  const initials = (user?.name ?? "?")
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]!.toUpperCase())
    .join("");

  return (
    <div className={`${s.shell} ${collapsed ? s.collapsed : ""}`}>
      <a href="#conteudo" className={s.skip} tabIndex={mobileOpen ? -1 : undefined}>
        Pular para o conteúdo
      </a>
      {mobileOpen && <div className={s.scrim} onClick={() => setMobileOpen(false)} aria-hidden />}
      <aside
        className={`${s.sidebar} ${mobileOpen ? s.sidebarOpen : ""}`}
        id="navegacao-painel"
        aria-label="Navegação do painel"
      >
        <div className={s.sideHead}>
          <Link to="/painel" className={s.brand} aria-label="Servia — Dashboard">
            {collapsed ? (
              <span className={s.brandMark}>
                s<span>.</span>
              </span>
            ) : (
              <Wordmark />
            )}
          </Link>
          <button
            className={`${s.iconBtn} ${s.mobileOnly}`}
            ref={closeNav}
            onClick={() => {
              setMobileOpen(false);
              requestAnimationFrame(() => openNav.current?.focus());
            }}
            aria-label="Fechar menu"
          >
            <X size={20} />
          </button>
        </div>
        <nav className={s.nav}>
          <ul>
            {items.map(({ to, label, icon: Icon }) => {
              const active = current.to === to;
              return (
                <li key={to}>
                  <Link
                    to={to}
                    onClick={() => setMobileOpen(false)}
                    className={`${s.navItem} ${active ? s.navActive : ""}`}
                    aria-current={active ? "page" : undefined}
                    aria-label={label}
                    title={collapsed ? label : undefined}
                  >
                    <Icon size={19} aria-hidden />
                    <span className={s.navLabel}>{label}</span>
                  </Link>
                </li>
              );
            })}
          </ul>
        </nav>
        <button
          className={`${s.collapseBtn} ${s.desktopOnly}`}
          onClick={toggleCollapse}
          aria-label={collapsed ? "Expandir menu" : "Recolher menu"}
          aria-pressed={collapsed}
        >
          {collapsed ? (
            <PanelLeftOpen size={18} />
          ) : (
            <>
              <PanelLeftClose size={18} />
              <span>Recolher</span>
            </>
          )}
        </button>
      </aside>

      <div className={s.main} inert={mobileOpen}>
        <header className={s.header}>
          <button
            className={`${s.iconBtn} ${s.mobileOnly}`}
            onClick={() => setMobileOpen(true)}
            ref={openNav}
            aria-controls="navegacao-painel"
            aria-label="Abrir menu"
            aria-expanded={mobileOpen}
          >
            <Menu size={22} />
          </button>
          <div className={s.context}>
            <span className={s.estName}>{user?.establishmentName}</span>
            <nav aria-label="Você está em" className={s.crumbs}>
              <span>Painel</span>
              <span aria-hidden>/</span>
              <b>{current.label}</b>
            </nav>
          </div>
          <DemoBadge />
          <ThemeToggle />
          <div className={s.profile} ref={menuRef}>
            <button
              className={s.profileBtn}
              ref={profileButton}
              aria-label="Menu da conta"
              onClick={() => setMenuOpen(!menuOpen)}
              aria-expanded={menuOpen}
            >
              <span className={s.avatar} aria-hidden>
                {initials}
              </span>
              <span className={s.profileName}>{user?.name.split(" ")[0]}</span>
              <ChevronDown size={16} aria-hidden />
            </button>
            {menuOpen && (
              <div className={s.menu}>
                <div className={s.menuHead}>
                  <strong>{user?.name}</strong>
                  <span>{user?.email}</span>
                  {user?.role && <span className={s.roleTag}>{ROLE_LABEL[user.role]}</span>}
                </div>
                {canAccess(user, "configuracoes") && (
                  <Link to="/painel/configuracoes" className={s.menuItem}>
                    <Settings size={16} aria-hidden /> Configurações
                  </Link>
                )}
                <button
                  className={s.menuItem}
                  onClick={async () => {
                    await logout();
                    void navigate({ to: "/" });
                  }}
                >
                  <LogOut size={16} /> Sair
                </button>
              </div>
            )}
          </div>
        </header>
        <main id="conteudo" key={pathname} tabIndex={-1} className={s.content}>
          {children}
        </main>
      </div>
    </div>
  );
}
