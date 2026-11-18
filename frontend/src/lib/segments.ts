// Segment options for registration. TODO(backend): replace with the segments endpoint if one exists.
export const SEGMENTS = [
  "Barbearia",
  "Salão de beleza",
  "Nail designer",
  "Clínica",
  "Estúdio de estética",
  "Sobrancelhas e cílios",
  "Tatuagem",
  "Massoterapia",
  "Maquiagem",
  "Depilação",
  "Tranças",
  "Outro serviço",
];

interface SegmentOption {
  code: number;
  label: string;
}

function configuredSegments(): SegmentOption[] {
  try {
    const data: unknown = JSON.parse(import.meta.env["VITE_SIGNUP_SEGMENTS"] || "[]");
    return Array.isArray(data)
      ? data.filter(
          (entry): entry is SegmentOption =>
            !!entry &&
            Number.isSafeInteger(entry.code) &&
            entry.code >= 0 &&
            typeof entry.label === "string" &&
            !!entry.label.trim(),
        )
      : [];
  } catch {
    return [];
  }
}

export const segmentOptions = configuredSegments();
