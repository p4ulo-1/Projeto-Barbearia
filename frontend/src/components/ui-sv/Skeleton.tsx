import s from "./styles";

export function Skeleton({ className = "" }: { className?: string }) {
  return <span className={`${s.skeleton} ${className}`} aria-hidden="true" />;
}

const widths = {
  list: ["minmax(0, 1fr)", "minmax(0, .42fr)"],
  table: ["minmax(0, .8fr)", "minmax(0, 1.6fr)", "minmax(0, 1fr)", "minmax(0, .65fr)"],
  summary: ["minmax(0, 1fr)", "minmax(0, 1fr)", "minmax(0, 1fr)", "minmax(0, 1fr)"],
};

export function LoadingRows({ variant = "list" }: { variant?: keyof typeof widths }) {
  const columns = widths[variant];
  const count = variant === "summary" ? 2 : 3;
  return (
    <div className={`${s.loadingRows} ${s[variant]}`} role="status" aria-busy="true">
      <span className={s.loadingLabel}>Carregando dados…</span>
      {Array.from({ length: count }, (_, row) => (
        <div
          key={row}
          className={s.loadingRow}
          aria-hidden="true"
          style={{ gridTemplateColumns: columns.join(" ") }}
        >
          {columns.map((_, column) => (
            <Skeleton key={column} />
          ))}
        </div>
      ))}
    </div>
  );
}
