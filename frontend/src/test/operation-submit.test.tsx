import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { useOperation } from "@/components/panel/forms";

function Form({ action }: { action: () => Promise<void> }) {
  const operation = useOperation();
  return (
    <form onSubmit={(event) => operation.submit(event, action, "Concluído")}>
      <button type="submit">Enviar</button>
    </form>
  );
}

it("envia uma única operação quando o formulário é submetido duas vezes no mesmo ciclo", async () => {
  let finish: (() => void) | undefined;
  const pending = new Promise<void>((resolve) => { finish = resolve; });
  const action = vi.fn(() => pending);
  const queryClient = new QueryClient();
  render(<QueryClientProvider client={queryClient}><Form action={action} /></QueryClientProvider>);
  const form = screen.getByRole("button", { name: "Enviar" }).closest("form");
  expect(form).not.toBeNull();
  act(() => {
    fireEvent.submit(form!);
    fireEvent.submit(form!);
  });
  await waitFor(() => expect(action).toHaveBeenCalledTimes(1));
  await act(async () => { finish?.(); await pending; });
});
