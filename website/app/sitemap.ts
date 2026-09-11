import type { MetadataRoute } from "next";

export const dynamic = "force-static";

export default function sitemap(): MetadataRoute.Sitemap { const base = "https://jashdubal.github.io/atten"; return ["", "/compare", "/faq", "/cli", "/privacy"].map((path) => ({ url: `${base}${path}`, lastModified: new Date("2026-09-10"), changeFrequency: "monthly", priority: path === "" ? 1 : .7 })); }
