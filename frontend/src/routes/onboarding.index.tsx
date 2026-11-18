import { createFileRoute } from "@tanstack/react-router";
import { SiteBuilder } from "@/components/onboarding/SiteBuilder";

export const Route = createFileRoute("/onboarding/")({ component: SiteBuilder });
