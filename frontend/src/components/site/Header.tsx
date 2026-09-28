import { Link, useNavigate } from "@tanstack/react-router";
import { motion } from "motion/react";
import { Menu, Scissors, User2 } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { useStore } from "@/lib/store";
import { SHOP } from "@/lib/shop";

const links = [
  { to: "/", label: "Home" },
  { to: "/agendamento", label: "Agendar" },
  { to: "/conta", label: "Minha conta" },
];

export function Header() {
  const { user, signOut } = useStore();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);

  const nav = (
    <>
      {links.map((l) => (
        <Link
          key={l.to}
          to={l.to}
          onClick={() => setOpen(false)}
          className="text-sm tracking-wide text-muted-foreground transition-colors hover:text-primary"
          activeProps={{ className: "text-primary" }}
          activeOptions={{ exact: l.to === "/" }}
        >
          {l.label}
        </Link>
      ))}
      {user?.role === "admin" && (
        <Link
          to="/admin"
          onClick={() => setOpen(false)}
          className="text-sm tracking-wide text-muted-foreground transition-colors hover:text-primary"
          activeProps={{ className: "text-primary" }}
        >
          Painel
        </Link>
      )}
    </>
  );

  return (
    <motion.header
      initial={{ y: -24, opacity: 0 }}
      animate={{ y: 0, opacity: 1 }}
      transition={{ duration: 0.35, ease: "easeOut" }}
      className="sticky top-0 z-40 border-b border-border/70 bg-background/85 backdrop-blur-md"
    >
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-4 sm:px-6">
        <Link to="/" className="flex items-center gap-2">
          <Scissors className="size-5 text-primary" aria-hidden />
          <span className="font-display text-lg tracking-wide sm:text-xl">
            {SHOP.name}
          </span>
        </Link>

        <nav className="hidden items-center gap-8 md:flex">{nav}</nav>

        <div className="flex items-center gap-2">
          {user ? (
            <>
              <Button
                variant="ghost"
                size="sm"
                className="hidden sm:inline-flex"
                onClick={() => signOut()}
              >
                Sair
              </Button>
              <Button asChild size="sm">
                <Link to="/agendamento">Agendar</Link>
              </Button>
            </>
          ) : (
            <>
              <Button
                variant="ghost"
                size="sm"
                className="hidden sm:inline-flex"
                onClick={() => navigate({ to: "/auth" })}
              >
                <User2 className="size-4" aria-hidden />
                Entrar
              </Button>
              <Button asChild size="sm">
                <Link to="/agendamento">Agendar</Link>
              </Button>
            </>
          )}

          <Sheet open={open} onOpenChange={setOpen}>
            <SheetTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                aria-label="Abrir menu"
                className="min-h-11 min-w-11 md:hidden"
              >
                <Menu className="size-5" aria-hidden />
              </Button>
            </SheetTrigger>
            <SheetContent side="right" className="w-72">
              <SheetTitle className="font-display text-xl">{SHOP.name}</SheetTitle>
              <nav className="mt-6 flex flex-col gap-5 px-1">
                {nav}
                {user ? (
                  <button
                    className="text-left text-sm text-muted-foreground hover:text-primary"
                    onClick={() => {
                      signOut();
                      setOpen(false);
                    }}
                  >
                    Sair
                  </button>
                ) : (
                  <Link
                    to="/auth"
                    onClick={() => setOpen(false)}
                    className="text-sm text-muted-foreground hover:text-primary"
                  >
                    Entrar
                  </Link>
                )}
              </nav>
            </SheetContent>
          </Sheet>
        </div>
      </div>
    </motion.header>
  );
}