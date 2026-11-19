import { useEffect, useRef, useState, type FormEvent } from "react";
import { Alert, Button, TextField } from "@/components/ui-sv";
import s from "./styles";

type SecureField = { mount(id: string): SecureField; unmount(): void };
type CardPaymentSettings = {
  initialization: { amount: number };
  customization: { paymentMethods: { maxInstallments: number; types: { excluded: string[] } } };
  callbacks: {
    onReady: () => void;
    onError: () => void;
    onSubmit: (data: { token?: string; payment_method_id?: string }) => Promise<void>;
  };
};
type MercadoPagoInstance = {
  bricks(): {
    create(
      type: "cardPayment",
      containerId: string,
      settings: CardPaymentSettings,
    ): Promise<{ unmount(): void }>;
  };
  fields: {
    create(type: string, options: { placeholder: string }): SecureField;
    createCardToken(input: {
      cardholderName: string;
      identificationType: string;
      identificationNumber: string;
    }): Promise<{ id: string }>;
  };
  getIdentificationTypes(): Promise<{ id: string; name: string }[]>;
};
declare global {
  interface Window {
    MercadoPago?: new (key: string, options: { locale: string }) => MercadoPagoInstance;
  }
}
const publicKey = (import.meta.env["VITE_MERCADO_PAGO_PUBLIC_KEY"] as string | undefined)?.trim();
let sdkLoading: Promise<void> | undefined;
function loadSdk() {
  if (window.MercadoPago) return Promise.resolve();
  return (sdkLoading ??= new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");
    script.src = "https://sdk.mercadopago.com/js/v2";
    script.async = true;
    script.onload = () => (window.MercadoPago ? resolve() : reject(new Error()));
    script.onerror = () => reject(new Error());
    document.head.appendChild(script);
  }).catch(() => {
    sdkLoading = undefined;
    throw new Error("Não foi possível carregar o formulário seguro do Mercado Pago.");
  }));
}

export function UpgradePaymentCard({
  amount,
  onPayment,
}: {
  amount: number;
  onPayment: (token: string, method: string) => Promise<void>;
}) {
  const submit = useRef(onPayment);
  submit.current = onPayment;
  const [error, setError] = useState("");
  const [ready, setReady] = useState(false);
  useEffect(() => {
    let active = true;
    let sending = false;
    let brick: { unmount(): void } | undefined;
    const key = publicKey;
    if (!key) {
      setError("O pagamento por cartão está indisponível.");
      return;
    }
    void loadSdk()
      .then(async () => {
        if (!active || !window.MercadoPago) return;
        const mp = new window.MercadoPago(key, { locale: "pt-BR" });
        brick = await mp.bricks().create("cardPayment", "mp-upgrade-payment", {
          initialization: { amount },
          customization: {
            paymentMethods: {
              maxInstallments: 1,
              types: { excluded: ["debit_card", "prepaid_card"] },
            },
          },
          callbacks: {
            onReady: () => {
              if (active) setReady(true);
            },
            onError: () => {
              if (active) setError("O formulário de pagamento não está disponível.");
            },
            onSubmit: async ({ token, payment_method_id: method }) => {
              if (!active || sending) return;
              if (!token || !method) throw new Error("Dados do cartão incompletos.");
              sending = true;
              try {
                await submit.current(token, method);
              } finally {
                sending = false;
              }
            },
          },
        });
        if (!active) brick.unmount();
      })
      .catch(() => {
        if (active) setError("Não foi possível carregar o formulário de pagamento.");
      });
    return () => {
      active = false;
      brick?.unmount();
    };
  }, [amount]);
  return (
    <>
      {error ? <Alert tone="error">{error}</Alert> : !ready && <p>Carregando formulário seguro…</p>}
      <div id="mp-upgrade-payment" />
    </>
  );
}

export function CheckoutCard({
  busy,
  onToken,
}: {
  busy: boolean;
  onToken: (token: string) => Promise<void>;
}) {
  const sdk = useRef<MercadoPagoInstance | null>(null);
  const lock = useRef(false);
  const alive = useRef(false);
  const [tokenizing, setTokenizing] = useState(false);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [types, setTypes] = useState<{ id: string; name: string }[]>([]);
  useEffect(() => {
    let live = true;
    alive.current = true;
    const mounted: SecureField[] = [];
    if (!publicKey) {
      setError("A autorização de cartão está indisponível no momento.");
      return () => {
        alive.current = false;
      };
    }
    void loadSdk()
      .then(async () => {
        if (!live || !window.MercadoPago) return;
        const mp = new window.MercadoPago(publicKey, { locale: "pt-BR" });
        sdk.current = mp;
        mounted.push(
          mp.fields
            .create("cardNumber", { placeholder: "Número do cartão" })
            .mount("mp-card-number"),
        );
        mounted.push(
          mp.fields.create("expirationDate", { placeholder: "MM/AA" }).mount("mp-card-expiration"),
        );
        mounted.push(
          mp.fields
            .create("securityCode", { placeholder: "Código de segurança" })
            .mount("mp-card-security"),
        );
        const available = await mp.getIdentificationTypes();
        if (live) {
          setTypes(available);
          setReady(true);
        }
      })
      .catch(() => {
        if (live)
          setError("Não foi possível carregar o formulário seguro. Tente novamente mais tarde.");
      });
    return () => {
      live = false;
      alive.current = false;
      sdk.current = null;
      for (const item of mounted) item.unmount();
    };
  }, []);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (lock.current || busy || !sdk.current) return;
    lock.current = true;
    setTokenizing(true);
    setError("");
    const form = new FormData(event.currentTarget);
    try {
      const token = await sdk.current.fields.createCardToken({
        cardholderName: String(form.get("holder") ?? "").trim(),
        identificationType: String(form.get("type") ?? ""),
        identificationNumber: String(form.get("document") ?? "").trim(),
      });
      if (!token.id) throw new Error();
      if (!alive.current) return;
      await onToken(token.id);
    } catch {
      if (alive.current)
        setError(
          "Não foi possível autorizar os dados informados. Confira os campos e tente novamente.",
        );
    } finally {
      lock.current = false;
      if (alive.current) setTokenizing(false);
    }
  }
  return (
    <form className={s.formGrid} onSubmit={(event) => void submit(event)}>
      <p>
        Cartão de crédito. Os dados do cartão são preenchidos nos campos seguros do Mercado Pago.
      </p>
      {error && <Alert tone="error">{error}</Alert>}
      <label>
        Número do cartão
        <div id="mp-card-number" style={{ minHeight: 40 }} />
      </label>
      <label>
        Validade
        <div id="mp-card-expiration" style={{ minHeight: 40 }} />
      </label>
      <label>
        Código de segurança
        <div id="mp-card-security" style={{ minHeight: 40 }} />
      </label>
      <TextField name="holder" label="Nome no cartão" required autoComplete="cc-name" />
      <label>
        Tipo de documento
        <select name="type" required defaultValue="">
          <option value="">Selecione</option>
          {types.map((type) => (
            <option key={type.id} value={type.id}>
              {type.name}
            </option>
          ))}
        </select>
      </label>
      <TextField name="document" label="Documento do titular" required />
      <Button type="submit" disabled={!ready || busy || tokenizing} loading={busy || tokenizing}>
        Autorizar cartão e recorrência
      </Button>
    </form>
  );
}
