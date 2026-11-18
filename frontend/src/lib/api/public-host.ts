import { createServerFn } from "@tanstack/react-start";
import { getRequestHost } from "@tanstack/react-start/server";
import { isPublicSiteHost } from "./public-site";

export const publicHost = createServerFn({ method: "GET" }).handler(() => {
  const host = getRequestHost().replace(/:\d+$/, "");
  return isPublicSiteHost(host) ? host : null;
});
