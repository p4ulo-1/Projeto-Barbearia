import { forwardRef, useId, useState, type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes } from "react";
import { AlertCircle, CheckCircle2, Eye, EyeOff, FlaskConical, Info, Loader2 } from "lucide-react";
import { IS_DEMO } from "@/lib/api/http";
import s from "./styles";

type BtnProps = ButtonHTMLAttributes<HTMLButtonElement> & { variant?: "primary" | "ghost" | "subtle"; size?: "md" | "lg"; loading?: boolean; block?: boolean };
export const Button = forwardRef<HTMLButtonElement, BtnProps>(function Button({ variant = "primary", size = "md", loading, block, className = "", children, disabled, ...rest }, ref) {
  return (
    <button ref={ref} className={`${s.btn} ${s[variant]} ${size === "lg" ? s.lg : ""} ${block ? s.block : ""} ${className}`} disabled={disabled || loading} aria-busy={loading || undefined} {...rest}>
      {loading && <Loader2 size={16} className={s.spin} aria-hidden />}
      {children}
    </button>
  );
});

interface FieldWrap { label: string; error?: string | undefined; hint?: ReactNode; }
function Wrap({ id, label, error, hint, children }: FieldWrap & { id: string; children: ReactNode }) {
  return (
    <div className={`${s.field} ${error ? s.hasError : ""}`}>
      <label htmlFor={id}>{label}</label>
      {children}
      {error ? <p id={`${id}-err`} className={s.error} role="alert"><AlertCircle size={14} />{error}</p> : hint ? <div className={s.hint}>{hint}</div> : null}
    </div>
  );
}

export function TextField({ label, error, hint, ...rest }: FieldWrap & InputHTMLAttributes<HTMLInputElement>) {
  const id = useId();
  return <Wrap id={id} label={label} error={error} hint={hint}><input id={id} className={s.input} aria-invalid={!!error} aria-describedby={error ? `${id}-err` : undefined} {...rest} /></Wrap>;
}

export function PasswordField({ label, error, hint, ...rest }: FieldWrap & InputHTMLAttributes<HTMLInputElement>) {
  const id = useId();
  const [show, setShow] = useState(false);
  return (
    <Wrap id={id} label={label} error={error} hint={hint}>
      <div className={s.inputGroup}>
        <input id={id} type={show ? "text" : "password"} className={s.input} aria-invalid={!!error} aria-describedby={error ? `${id}-err` : undefined} {...rest} />
        <button type="button" className={s.inputIcon} onClick={() => setShow(!show)} aria-label={show ? "Ocultar senha" : "Mostrar senha"}>{show ? <EyeOff size={18} /> : <Eye size={18} />}</button>
      </div>
    </Wrap>
  );
}

export function SelectField({ label, error, hint, options, placeholder, ...rest }: FieldWrap & SelectHTMLAttributes<HTMLSelectElement> & { options: string[]; placeholder?: string }) {
  const id = useId();
  return (
    <Wrap id={id} label={label} error={error} hint={hint}>
      <select id={id} className={s.input} aria-invalid={!!error} {...rest}>
        {placeholder && <option value="" disabled>{placeholder}</option>}
        {options.map((o) => <option key={o} value={o}>{o}</option>)}
      </select>
    </Wrap>
  );
}

export function Alert({ tone = "info", title, children }: { tone?: "info" | "error" | "success" | "warning"; title?: string | undefined; children?: ReactNode }) {
  const Icon = tone === "error" ? AlertCircle : tone === "success" ? CheckCircle2 : Info;
  return (
    <div className={`${s.alert} ${s[`alert_${tone}`]}`} role={tone === "error" ? "alert" : "status"}>
      <Icon size={18} aria-hidden />
      <div>{title && <strong>{title}</strong>}{children && <div>{children}</div>}</div>
    </div>
  );
}

export function Spinner({ label = "Carregando…" }: { label?: string | undefined }) {
  return <span className={s.spinnerWrap} role="status"><Loader2 size={20} className={s.spin} aria-hidden />{label}</span>;
}

export function FullPageLoader({ label }: { label?: string | undefined }) {
  return <div className={s.fullPage}><Spinner label={label} /></div>;
}

export function DemoBadge() {
  if (!IS_DEMO) return null;
  return <span className={s.demoBadge} title="A API do Servia ainda não está conectada. Os dados ficam apenas neste navegador."><FlaskConical size={13} /> Modo demonstração</span>;
}
