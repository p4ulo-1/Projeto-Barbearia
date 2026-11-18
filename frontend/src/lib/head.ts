/** Head meta for private/auth pages: unique title, never indexed. */
export function privateHead(title: string, description: string) {
  return {
    meta: [
      { title: `${title} — Servia` },
      { name: "description", content: description },
      { name: "robots", content: "noindex, nofollow" },
      { property: "og:title", content: `${title} — Servia` },
      { property: "og:description", content: description },
      { property: "og:type", content: "website" },
      { name: "twitter:card", content: "summary" },
    ],
  };
}
