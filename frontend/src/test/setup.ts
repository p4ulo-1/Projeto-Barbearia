import "@testing-library/jest-dom/vitest";

// Node 26 exposes an experimental global localStorage that may be undefined,
// overriding jsdom's storage. Keep an isolated browser-like store for tests.
const localValues = new Map<string, string>();
const browserLocalStorage: Storage = {
  get length() {
    return localValues.size;
  },
  clear: () => localValues.clear(),
  getItem: (key) => localValues.get(key) ?? null,
  key: (index) => [...localValues.keys()][index] ?? null,
  removeItem: (key) => {
    localValues.delete(key);
  },
  setItem: (key, value) => {
    localValues.set(key, String(value));
  },
};
Object.defineProperty(globalThis, "localStorage", {
  configurable: true,
  value: browserLocalStorage,
});
Object.defineProperty(window, "localStorage", {
  configurable: true,
  value: browserLocalStorage,
});

Object.defineProperty(window, "scrollTo", {
  writable: true,
  value: () => {},
});

Object.defineProperty(window, "matchMedia", {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => {},
  }),
});
