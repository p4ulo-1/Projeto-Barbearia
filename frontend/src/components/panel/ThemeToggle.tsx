import { useSyncExternalStore } from "react";
import { Moon, Sun } from "lucide-react";
import { getTheme, setTheme, subscribeTheme } from "@/lib/theme";
import s from "./styles";

export function ThemeToggle() {
  const theme = useSyncExternalStore(subscribeTheme, getTheme, () => "light" as const);
  const dark = theme === "dark";
  return (
    <button
      type="button"
      className={`${s.iconBtn} ${s.themeToggle}`}
      aria-label="Tema escuro"
      aria-pressed={dark}
      title={dark ? "Ativar tema claro" : "Ativar tema escuro"}
      onClick={() => setTheme(dark ? "light" : "dark")}
    >
      {dark ? <Sun size={18} aria-hidden /> : <Moon size={18} aria-hidden />}
    </button>
  );
}
