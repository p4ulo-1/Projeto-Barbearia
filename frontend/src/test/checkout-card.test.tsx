import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";

it("monta campos seguros, gera apenas token e impede submissão repetida", async () => {
  vi.stubEnv("VITE_MERCADO_PAGO_PUBLIC_KEY", "TEST-public-key");
  const unmount = vi.fn();
  const create = vi.fn((_kind: string, _options: { placeholder: string }) => ({
    mount: vi.fn(function () {
      return { unmount };
    }),
  }));
  let finish!: (token: { id: string }) => void;
  const createCardToken = vi.fn(
    () =>
      new Promise<{ id: string }>((resolve) => {
        finish = resolve;
      }),
  );
  const sdk = {
    fields: { create, createCardToken },
    getIdentificationTypes: vi.fn().mockResolvedValue([{ id: "CPF", name: "CPF" }]),
  };
  vi.stubGlobal(
    "MercadoPago",
    vi.fn(function () {
      return sdk;
    }),
  );
  const { CheckoutCard } = await import("@/components/panel/CheckoutCard");
  const submit = vi.fn().mockResolvedValue(undefined);
  const view = render(<CheckoutCard busy={false} onToken={submit} />);
  const type = await screen.findByLabelText("Tipo de documento");
  await screen.findByRole("option", { name: "CPF" });
  fireEvent.change(type, { target: { value: "CPF" } });
  fireEvent.change(screen.getByLabelText("Nome no cartão"), { target: { value: "Titular" } });
  fireEvent.change(screen.getByLabelText("Documento do titular"), {
    target: { value: "12345678900" },
  });
  const form = screen
    .getByRole("button", { name: "Autorizar cartão e recorrência" })
    .closest("form")!;
  fireEvent.submit(form);
  fireEvent.submit(form);
  expect(createCardToken).toHaveBeenCalledTimes(1);
  expect(createCardToken).toHaveBeenCalledWith({
    cardholderName: "Titular",
    identificationType: "CPF",
    identificationNumber: "12345678900",
  });
  finish({ id: "opaque-token" });
  await waitFor(() => expect(submit).toHaveBeenCalledWith("opaque-token"));
  expect(create.mock.calls.map((call) => call[0])).toEqual([
    "cardNumber",
    "expirationDate",
    "securityCode",
  ]);
  view.unmount();
  expect(unmount).toHaveBeenCalledTimes(3);
});
afterEach(() => {
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
});

it("não envia token concluído depois que o formulário é desmontado", async () => {
  vi.stubEnv("VITE_MERCADO_PAGO_PUBLIC_KEY", "TEST-public-key");
  let finish!: (token: { id: string }) => void;
  const sdk = {
    fields: {
      create: () => ({ mount: () => ({ unmount: vi.fn() }) }),
      createCardToken: () =>
        new Promise<{ id: string }>((resolve) => {
          finish = resolve;
        }),
    },
    getIdentificationTypes: vi.fn().mockResolvedValue([{ id: "CPF", name: "CPF" }]),
  };
  vi.stubGlobal(
    "MercadoPago",
    vi.fn(function () {
      return sdk;
    }),
  );
  const { CheckoutCard } = await import("@/components/panel/CheckoutCard");
  const submit = vi.fn();
  const view = render(<CheckoutCard busy={false} onToken={submit} />);
  await screen.findByRole("option", { name: "CPF" });
  fireEvent.change(screen.getByLabelText("Tipo de documento"), { target: { value: "CPF" } });
  fireEvent.change(screen.getByLabelText("Nome no cartão"), { target: { value: "Titular" } });
  fireEvent.change(screen.getByLabelText("Documento do titular"), {
    target: { value: "12345678900" },
  });
  fireEvent.submit(
    screen.getByRole("button", { name: "Autorizar cartão e recorrência" }).closest("form")!,
  );
  view.unmount();
  finish({ id: "opaque-token" });
  await Promise.resolve();
  expect(submit).not.toHaveBeenCalled();
});
