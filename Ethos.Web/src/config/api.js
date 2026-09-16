/**
 * Ethos Dance Studio — Centralized API Base URL Configuration
 *
 * In production builds (Vite), VITE_API_BASE_URL is injected from environment variables.
 * In local development, it falls back to http://localhost:5252 if unset.
 *
 * All API clients, services, modals, and asset loaders must import API_BASE_URL from here.
 * NEVER hardcode localhost URLs directly in components.
 */

const rawBaseUrl = import.meta.env.VITE_API_BASE_URL;

const isLocalhost =
  typeof window !== "undefined" &&
  (window.location.hostname === "localhost" ||
   window.location.hostname === "127.0.0.1" ||
   window.location.hostname === "");

export const API_BASE_URL = (
  rawBaseUrl && typeof rawBaseUrl === "string" && rawBaseUrl.trim().length > 0
    ? rawBaseUrl.trim().replace(/\/+$/, "")
    : ""
);

export const RAZORPAY_KEY_ID = import.meta.env.VITE_RAZORPAY_KEY_ID || "";
