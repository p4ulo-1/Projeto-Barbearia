import { cn } from "@/lib/utils";

function Block({ className }: { className?: string }) {
  return <div className={cn("shimmer rounded-md bg-muted/70", className)} />;
}

export function SlotsSkeleton() {
  return (
    <div
      className="grid grid-cols-3 gap-2 sm:grid-cols-4 md:grid-cols-6"
      aria-hidden
    >
      {Array.from({ length: 12 }).map((_, i) => (
        <Block key={i} className="h-11" />
      ))}
    </div>
  );
}

export function AppointmentCardSkeleton() {
  return (
    <div className="surface-card rounded-xl p-5" aria-hidden>
      <div className="flex items-start justify-between gap-4">
        <div className="w-full space-y-3">
          <Block className="h-5 w-40" />
          <Block className="h-4 w-56" />
          <Block className="h-4 w-32" />
        </div>
        <Block className="h-6 w-24 rounded-full" />
      </div>
      <div className="mt-5 flex gap-2">
        <Block className="h-9 w-28" />
        <Block className="h-9 w-28" />
      </div>
    </div>
  );
}

export function AppointmentListSkeleton({ count = 3 }: { count?: number }) {
  return (
    <div className="space-y-4">
      {Array.from({ length: count }).map((_, i) => (
        <AppointmentCardSkeleton key={i} />
      ))}
    </div>
  );
}

export function GallerySkeleton() {
  return (
    <div className="grid grid-cols-2 gap-3 md:grid-cols-4" aria-hidden>
      {Array.from({ length: 4 }).map((_, i) => (
        <Block key={i} className="aspect-square w-full rounded-xl" />
      ))}
    </div>
  );
}

export function TableRowsSkeleton({ cols = 5, rows = 4 }: { cols?: number; rows?: number }) {
  return (
    <div className="space-y-2" aria-hidden>
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} className="grid gap-3" style={{ gridTemplateColumns: `repeat(${cols}, minmax(0,1fr))` }}>
          {Array.from({ length: cols }).map((_, c) => (
            <Block key={c} className="h-10" />
          ))}
        </div>
      ))}
    </div>
  );
}