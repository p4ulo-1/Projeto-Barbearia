import s from "./styles";
export function Wordmark({ inverted = false }: { inverted?: boolean }) {
  return <span className={`${s.wordmark} ${inverted ? s.wordmarkInv : ""}`}>servia<span className={s.wordmarkDot}>.</span></span>;
}
