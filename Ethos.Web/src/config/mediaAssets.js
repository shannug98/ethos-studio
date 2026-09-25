/**
 * Ethos Dance Studio — Canonical Media Assets Registry
 *
 * Single source of truth for static application assets (emblems, letterheads, tier badges)
 * and Cloudflare R2 domain configuration.
 *
 * Managed promotional media (Hero, We Are Ethos, Founders, Faculty Trainers, Gallery)
 * is strictly driven by the Admin Media Library API in Cloudflare R2.
 */

export const R2_DOMAIN =
  (typeof import.meta !== "undefined" && import.meta.env?.VITE_R2_MEDIA_BASE_URL) ||
  "https://media.ethosdancestudio.com";

export const MEDIA_ASSETS = {
  brand: {
    emblem: "/assets/brand/ethos-emblem.png",
    tierEmblems: "/assets/trainer/ethos-tier-emblems.png",
    letterheadHeader: "/assets/official-letterhead-header.png",
    letterheadFooter: "/assets/official-letterhead-footer.png",
    avatarPlaceholder: "/images/avatar-placeholder.svg",
  },
  trainers: {
    tierEmblems: "/assets/trainer/ethos-tier-emblems.png",
    applicationHero: "",
    loginHero: "",
  },
  student: {
    membershipCardBg: "",
  },
};
