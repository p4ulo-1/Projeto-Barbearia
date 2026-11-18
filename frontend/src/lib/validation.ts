// Client-side form validation for UX only; the backend remains the source of truth.
export const onlyDigits = (v: string) => v.replace(/\D/g, "");

export function isEmail(v: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(v.trim());
}

export const PASSWORD_RULES = [
  { id: "len", label: "Pelo menos 8 caracteres", test: (p: string) => p.length >= 8 },
  { id: "upper", label: "Uma letra maiúscula", test: (p: string) => /[A-Z]/.test(p) },
  { id: "lower", label: "Uma letra minúscula", test: (p: string) => /[a-z]/.test(p) },
  { id: "num", label: "Um número", test: (p: string) => /\d/.test(p) },
];
export const isStrongPassword = (p: string) => PASSWORD_RULES.every((r) => r.test(p));

/** Formats a CPF (11 digits) or CNPJ (14 digits) while typing. */
export function maskDocument(v: string) {
  const d = onlyDigits(v).slice(0, 14);
  if (d.length <= 11)
    return d
      .replace(/(\d{3})(\d)/, "$1.$2")
      .replace(/(\d{3})(\d)/, "$1.$2")
      .replace(/(\d{3})(\d{1,2})$/, "$1-$2");
  return d
    .replace(/^(\d{2})(\d)/, "$1.$2")
    .replace(/^(\d{2})\.(\d{3})(\d)/, "$1.$2.$3")
    .replace(/\.(\d{3})(\d)/, ".$1/$2")
    .replace(/(\d{4})(\d)/, "$1-$2");
}

function cpfOk(d: string) {
  if (d.length !== 11 || /^(\d)\1+$/.test(d)) return false;
  const calc = (n: number) => {
    let s = 0;
    for (let i = 0; i < n; i++) s += Number(d[i]) * (n + 1 - i);
    const r = (s * 10) % 11;
    return r === 10 ? 0 : r;
  };
  return calc(9) === Number(d[9]) && calc(10) === Number(d[10]);
}
function cnpjOk(d: string) {
  if (d.length !== 14 || /^(\d)\1+$/.test(d)) return false;
  const calc = (n: number) => {
    const w =
      n === 12 ? [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2] : [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    const s = w.reduce((a, x, i) => a + x * Number(d[i]), 0);
    const r = s % 11;
    return r < 2 ? 0 : 11 - r;
  };
  return calc(12) === Number(d[12]) && calc(13) === Number(d[13]);
}
export function isValidDocument(v: string) {
  const d = onlyDigits(v);
  return d.length === 11 ? cpfOk(d) : d.length === 14 ? cnpjOk(d) : false;
}

export interface RegisterForm {
  establishmentName: string;
  address: string;
  segment: string;
  document: string;
  ownerName: string;
  email: string;
  password: string;
  confirmPassword: string;
}
export function validateRegister(f: RegisterForm, mode: "demo" | "real" = "demo") {
  const e: Partial<Record<keyof RegisterForm, string>> = {};
  if (f.establishmentName.trim().length < 2)
    e.establishmentName = "Informe o nome do estabelecimento.";
  if (mode === "demo" && f.address.trim().length < 5) e.address = "Informe o endereço.";
  if (!f.segment) e.segment = "Informe o segmento.";
  else if (mode === "real" && !/^\d+$/.test(f.segment))
    e.segment = "Informe um código de segmento válido.";
  if (mode === "real" && !f.document) e.document = "Informe o CPF ou CNPJ.";
  else if (f.document && !isValidDocument(f.document)) e.document = "CPF ou CNPJ inválido.";
  if (f.ownerName.trim().length < 2) e.ownerName = "Informe o nome do responsável.";
  if (!isEmail(f.email)) e.email = "Informe um e-mail válido.";
  if (
    mode === "demo"
      ? !isStrongPassword(f.password)
      : f.password.length < 8 || f.password.length > 200
  )
    e.password = "A senha não atende aos requisitos.";
  if (f.confirmPassword !== f.password) e.confirmPassword = "As senhas não conferem.";
  return e;
}

export function formatBytes(n: number) {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(0)} KB`;
  return `${(n / 1024 / 1024).toFixed(1)} MB`;
}
