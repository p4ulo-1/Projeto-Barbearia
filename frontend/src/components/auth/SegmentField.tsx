import { TextField } from "@/components/ui-sv";
import { segmentOptions } from "@/lib/segments";
import s from "@/components/ui-sv/styles";

export function SegmentField({
  value,
  defaultValue,
  onChange,
  error,
}: {
  value?: string;
  defaultValue?: string;
  onChange?: (event: { target: { value: string } }) => void;
  error?: string | undefined;
}) {
  if (segmentOptions.length) {
    return (
      <label className={s.field}>
        Segmento de atuação
        <select
          name="segment"
          className={s.input}
          required
          aria-invalid={!!error}
          {...(value === undefined ? { defaultValue: defaultValue ?? "" } : { value, onChange })}
        >
          <option value="">Selecione um segmento</option>
          {segmentOptions.map((segment) => (
            <option key={segment.code} value={segment.code}>
              {segment.label}
            </option>
          ))}
        </select>
        {error && (
          <span role="alert" className={s.error}>
            {error}
          </span>
        )}
      </label>
    );
  }
  return (
    <TextField
      label="Código do segmento"
      name="segment"
      inputMode="numeric"
      pattern="[0-9]+"
      required
      error={error}
      hint="Use o código de segmento fornecido pelo Servia."
      {...(value === undefined ? { defaultValue } : { value, onChange })}
    />
  );
}
