import type { Metadata } from "next";
import Link from "next/link";
import { AppPreview, LogoMark } from "@/components/app-preview";
import { DownloadButton } from "@/components/download-button";
import { ArrowRight, GitHub } from "@/components/icons";
import { NeuralVeil } from "@/components/neural-veil";

export const metadata: Metadata = {
  title: "Atten | Free, offline text-to-speech app for macOS and Windows",
  description: "Atten is free, open-source text-to-speech that runs locally on your computer. No cloud, account, subscription, or API key.",
  alternates: { canonical: "/" },
  openGraph: {
    title: "Atten — Local text-to-speech. No cloud required.",
    description: "Natural text-to-speech that runs entirely on your computer. Free, private, and offline.",
    type: "website",
  },
};

const productJsonLd = {
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  name: "Atten",
  applicationCategory: "MultimediaApplication",
  operatingSystem: "macOS 14+, Windows 10 1809+",
  description: "Free and open-source local text-to-speech for Mac, Windows, and the command line.",
  isAccessibleForFree: true,
  license: "https://www.gnu.org/licenses/gpl-3.0.html",
  url: "https://jashdubal.github.io/atten/",
  downloadUrl: "https://github.com/jashdubal/atten/releases/latest",
  featureList: ["Offline text-to-speech", "37 voices", "MP3 and WAV export", "Command-line interface"],
};

export default function Home() {
  return <main className="relative min-h-svh overflow-hidden bg-[#080807] text-[#e7eef8]">
    <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(productJsonLd) }} />
    <NeuralVeil />
    <div className="noise pointer-events-none absolute inset-0 opacity-[.07] mix-blend-soft-light" />
    <div className="grid-floor pointer-events-none absolute inset-x-0 bottom-0 h-[62%] opacity-70" />

    <div className="relative z-10 mx-auto grid min-h-svh min-w-0 max-w-[1728px] grid-rows-[auto_minmax(0,1fr)_auto] px-4 sm:px-6 lg:px-8">
      <nav className="flex min-w-0 items-center justify-between py-3 sm:py-5" aria-label="Main navigation">
        <Link href="/" className="flex items-center gap-2.5 rounded-xl py-1" aria-label="Atten home">
          <LogoMark small />
          <span className="mono text-[11px] font-bold tracking-[.25em] text-[#dce8f5]">ATTEN</span>
        </Link>
        <div className="flex items-center gap-1">
          <Link href="/compare" className="hidden rounded-lg px-3 py-2 text-xs text-[#8fa2ba] transition hover:bg-white/[.05] hover:text-white sm:block">Compare</Link>
          <Link href="/faq" className="hidden rounded-lg px-3 py-2 text-xs text-[#8fa2ba] transition hover:bg-white/[.05] hover:text-white sm:block">FAQ</Link>
          <a href="https://github.com/jashdubal/atten" aria-label="View Atten on GitHub" className="rounded-lg p-2 text-[#8fa2ba] transition hover:bg-white/[.05] hover:text-white"><GitHub className="size-[17px]" /></a>
          <span className="mx-1 h-5 w-px bg-white/10" aria-hidden="true" />
        </div>
      </nav>

      <section className="grid min-h-0 items-center gap-8 py-8 xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)] xl:gap-10 xl:py-0 2xl:grid-cols-[minmax(0,640px)_minmax(0,1fr)] 2xl:gap-14">
        <div className="min-w-0">
          <div className="mono flex w-fit items-center gap-2 rounded-full border border-[#5ddbff]/25 bg-[#5ddbff]/8 px-3 py-1.5 text-[9px] tracking-[.16em] text-[#91e8ff] backdrop-blur-md sm:text-[10px]">
            <i className="size-1.5 rounded-full bg-[#8fb996] shadow-[0_0_9px_#8fb996]" />
            ATTEN · LOCAL TTS
          </div>

          <h1 className="mt-4 font-semibold leading-[.9] tracking-[-.065em] text-white sm:mt-6">
            <span className="block whitespace-nowrap text-[clamp(1.75rem,4.3vw,3.75rem)]">Local text-to-speech.</span>
            <span className="mt-2 block whitespace-nowrap bg-gradient-to-r from-[#5ddbff] via-[#b7f2ff] to-[#9e70ff] bg-clip-text text-[clamp(1.75rem,4.3vw,3.75rem)] text-transparent">No cloud required.</span>
          </h1>
          <p className="mt-5 max-w-lg text-base leading-7 text-[#b4c1d3] sm:mt-6 sm:text-lg sm:leading-8">Private, unlimited natural voice generation that works even when you&apos;re offline.</p>

          <div className="mt-8 hidden sm:block"><DownloadButton /></div>
          <div className="mt-4 flex items-center gap-4 sm:hidden"><a href="https://github.com/jashdubal/atten/releases/latest" className="inline-flex items-center gap-2 rounded-xl bg-[#5ddbff] px-4 py-3 text-sm font-semibold text-[#061018]">Get Atten <ArrowRight className="size-4" /></a><Link href="/compare" className="text-xs font-medium text-[#91e8ff]">Why local? →</Link></div>
        </div>

        <div className="relative hidden min-h-0 xl:block">
          <div className="absolute -inset-10 rounded-full bg-[#5a2ec2]/12 blur-[90px]" />
          <div className="relative rounded-[22px] border border-white/10 bg-[#0a101b]/60 p-2 shadow-[0_35px_100px_rgba(0,0,0,.55)] backdrop-blur-sm">
            <AppPreview />
          </div>
          <div className="mono absolute -bottom-4 left-8 right-8 flex items-center justify-between rounded-xl border border-white/10 bg-[#080c14]/90 px-4 py-3 text-[9px] tracking-[.12em] text-[#8296af] shadow-xl backdrop-blur-xl">
            <span><i className="mr-2 inline-block size-1.5 rounded-full bg-[#8fb996]" />PROCESSING LOCALLY</span>
            <span>NO ACCOUNT · NO API KEY</span>
          </div>
        </div>
      </section>

      <footer className="flex items-center justify-between border-t border-white/8 py-3 text-[10px] text-[#71849d] sm:py-4 sm:text-xs">
        <p className="hidden sm:block">Free and open source · GPL-3.0</p>
        <div className="flex w-full items-center justify-between gap-4 sm:w-auto sm:justify-end">
          <Link href="/cli" className="transition hover:text-[#5ddbff]">CLI</Link>
          <Link href="/compare" className="transition hover:text-[#5ddbff]">Compare</Link>
          <Link href="/faq" className="transition hover:text-[#5ddbff]">FAQ</Link>
          <Link href="/privacy" className="transition hover:text-[#5ddbff]">Privacy</Link>
        </div>
      </footer>
    </div>
  </main>;
}
