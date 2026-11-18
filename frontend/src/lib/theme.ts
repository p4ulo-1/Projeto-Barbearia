type Theme = "light" | "dark";

const themeKey = "servia.theme";
const themeEvent = "servia:theme";

export const initializeTheme = `(() => {
  try {
    const dark = localStorage.getItem("${themeKey}") === "dark";
    document.documentElement.classList.toggle("dark", dark);
    document.querySelector('meta[name="theme-color"]')?.setAttribute("content", dark ? "#080D18" : "#F8FAFC");
  } catch {}
})();`;

export function getTheme(): Theme {
  return document.documentElement.classList.contains("dark") ? "dark" : "light";
}

export function setTheme(theme: Theme) {
  document.documentElement.classList.toggle("dark", theme === "dark");
  document
    .querySelector('meta[name="theme-color"]')
    ?.setAttribute("content", theme === "dark" ? "#080D18" : "#F8FAFC");
  try {
    localStorage.setItem(themeKey, theme);
  } catch {
    // Keep the selected theme for this page if browser storage is unavailable.
  }
  window.dispatchEvent(new Event(themeEvent));
}

export function subscribeTheme(notify: () => void) {
  window.addEventListener(themeEvent, notify);
  return () => window.removeEventListener(themeEvent, notify);
}
