import type { ReactNode } from "react";
import type { UseQueryResult } from "@tanstack/react-query";
import { LoadingRows } from "@/components/ui-sv/Skeleton";
import { Alert, Button } from "@/components/ui-sv";
import type { Page } from "@/lib/api/contracts";
import s from "./styles";

export function ModuleCard({
  title,
  children,
  id,
}: {
  title: string;
  children: ReactNode;
  id?: string;
}) {
  return (
    <section className={s.moduleCard} id={id}>
      <h2>{title}</h2>
      {children}
    </section>
  );
}

export function QueryState<T>({
  query,
  children,
  loading = "list",
}: {
  loading?: "list" | "table" | "summary";
  query: UseQueryResult<T>;
  children: (data: T) => ReactNode;
}) {
  if (query.isPending) return <LoadingRows variant={loading} />;
  if (query.isError)
    return (
      <Alert tone="error">
        {query.error.message}{" "}
        <Button variant="ghost" onClick={() => void query.refetch()}>
          Tentar novamente
        </Button>
      </Alert>
    );
  return <>{children(query.data)}</>;
}

export function Pagination({
  data,
  setPage,
}: {
  data: Page<unknown>;
  setPage: (page: number) => void;
}) {
  if (!data.hasNext && !data.hasPrevious) return null;
  return (
    <nav aria-label="Paginação" className={s.pagination}>
      <Button variant="subtle" disabled={!data.hasPrevious} onClick={() => setPage(data.page - 1)}>
        Anterior
      </Button>
      <span>
        Página {data.page} de {data.totalPages}
      </span>
      <Button variant="subtle" disabled={!data.hasNext} onClick={() => setPage(data.page + 1)}>
        Próxima
      </Button>
    </nav>
  );
}

export function RecordSelect({
  label,
  name,
  records,
  required = false,
  defaultValue = "",
  value,
  onChange,
}: {
  label: string;
  name: string;
  records: { id: string; name: string }[];
  required?: boolean;
  defaultValue?: string;
  value?: string;
  onChange?: (value: string) => void;
}) {
  return (
    <label className={s.selectField}>
      <span>{label}</span>
      <select
        name={name}
        required={required}
        {...(value === undefined
          ? { defaultValue }
          : { value, onChange: (event) => onChange?.(event.target.value) })}
      >
        <option value="">{required ? "Selecione" : "Nenhum"}</option>
        {records.map((record) => (
          <option key={record.id} value={record.id}>
            {record.name}
          </option>
        ))}
      </select>
    </label>
  );
}
