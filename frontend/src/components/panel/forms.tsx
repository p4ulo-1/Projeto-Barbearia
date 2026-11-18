import type { FormEvent } from "react";
import { toast } from "sonner";
import { fieldFeedback, operationErrorMessage } from "@/lib/operation-feedback";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Alert } from "@/components/ui-sv";

export const weekLabels = [
  "Domingo",
  "Segunda-feira",
  "Terça-feira",
  "Quarta-feira",
  "Quinta-feira",
  "Sexta-feira",
  "Sábado",
];

export function useOperation() {
  const qc = useQueryClient();
  const mutation = useMutation({
    mutationFn: ({ action }: { action: () => Promise<unknown>; success: string }) => action(),
    onSuccess: (_result, { success }) => {
      toast.success(success);
      return qc.invalidateQueries({ queryKey: ["panel"] });
    },
    onError: (error) => {
      if (!fieldFeedback(error).length) toast.error(operationErrorMessage(error));
    },
  });
  function run(action: () => Promise<unknown>, success: string, after?: () => void) {
    if (mutation.isPending) return;
    mutation.mutate({ action, success }, { onSuccess: () => after?.() });
  }
  function submit(
    event: FormEvent<HTMLFormElement>,
    action: (data: FormData) => Promise<unknown>,
    success: string,
    reset = true,
  ) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    run(() => action(data), success, reset ? () => form.reset() : undefined);
  }
  const fieldErrors = fieldFeedback(mutation.error);
  return {
    run,
    submit,
    busy: mutation.isPending,
    feedback:
      fieldErrors.length > 0 ? (
        <Alert tone="error" title="Revise os dados informados">
          <ul>
            {fieldErrors.map((message, index) => (
              <li key={index}>{message}</li>
            ))}
          </ul>
        </Alert>
      ) : null,
  };
}

export const field = (data: FormData, key: string) => String(data.get(key) ?? "").trim();
export const numberField = (data: FormData, key: string) => Number(field(data, key));
export const optional = <K extends string>(data: FormData, key: K) => {
  const value = field(data, key);
  return (value ? { [key]: value } : {}) as Partial<Record<K, string>>;
};
export const timeField = (value: string) => (value.length === 5 ? value + ":00" : value);
export function localDate(date = new Date()) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}
export function displayDate(date: string) {
  return new Intl.DateTimeFormat("pt-BR", { dateStyle: "medium", timeZone: "UTC" }).format(
    new Date(date.slice(0, 10) + "T12:00:00Z"),
  );
}
