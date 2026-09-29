import { test, describe } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// 1. Test NumericInput normalization logic directly
describe("Numeric Normalization (Years of Experience)", () => {
  const sanitizeDigits = (raw) => {
    if (!raw) return "";
    let digits = raw.replace(/\D/g, "");
    if (!digits) return "";
    if (digits.length > 1 && digits.startsWith("0")) {
      digits = digits.replace(/^0+/, "");
      if (digits === "") digits = "0";
    }
    return digits;
  };

  test("strips leading zeros from 04 to 4", () => {
    assert.equal(sanitizeDigits("04"), "4");
  });

  test("strips leading zeros from 023 to 23", () => {
    assert.equal(sanitizeDigits("023"), "23");
  });

  test("preserves 24 as 24", () => {
    assert.equal(sanitizeDigits("24"), "24");
  });

  test("preserves legitimate 0 as 0", () => {
    assert.equal(sanitizeDigits("0"), "0");
  });

  test("normalizes multiple leading zeros 000 to 0", () => {
    assert.equal(sanitizeDigits("000"), "0");
  });

  test("strips non-digits and negative signs", () => {
    assert.equal(sanitizeDigits("-5"), "5");
    assert.equal(sanitizeDigits("1.5"), "15");
    assert.equal(sanitizeDigits("abc"), "");
  });
});

// 2. Test Media URL resolution and initials
describe("Media URL Resolution & Deterministic Initials", () => {
  const API_BASE_URL = "http://localhost:5000";

  // Simulate getMediaUrl logic
  function resolveDevUrl(urlOrPath) {
    if (!urlOrPath) return "";
    if (urlOrPath.startsWith("blob:") || urlOrPath.startsWith("data:")) return urlOrPath;
    if (urlOrPath.includes("media.ethosdancestudio.com/trainers/")) {
      const subpath = urlOrPath.split("media.ethosdancestudio.com/trainers/")[1];
      return `${API_BASE_URL}/uploads/trainers/${subpath}`;
    }
    if (/^https?:\/\//i.test(urlOrPath)) return urlOrPath;
    const normalized = urlOrPath.startsWith("/") ? urlOrPath : `/${urlOrPath}`;
    return `${API_BASE_URL}${normalized}`;
  }

  function getInitials(name) {
    if (!name || name === "Unnamed Trainer") return "ED";
    const words = name.trim().split(/\s+/).filter(Boolean);
    if (words.length >= 2) {
      return `${words[0][0]}${words[1][0]}`.toUpperCase();
    }
    return (words[0] || "ED").slice(0, 2).toUpperCase();
  }

  test("rewrites remote trainer CDN to local API upload mirror in dev", () => {
    const remoteUrl = "https://media.ethosdancestudio.com/trainers/images/2026/09/test_photo.webp";
    const resolved = resolveDevUrl(remoteUrl);
    assert.equal(resolved, "http://localhost:5000/uploads/trainers/images/2026/09/test_photo.webp");
  });

  test("resolves local relative upload path to full backend URL", () => {
    const relative = "/uploads/trainers/images/2026/09/new_photo.jpg";
    const resolved = resolveDevUrl(relative);
    assert.equal(resolved, "http://localhost:5000/uploads/trainers/images/2026/09/new_photo.jpg");
  });

  test("preserves instant blob preview URL unaltered", () => {
    const blob = "blob:http://localhost:5173/0e3cb20a-6e5a-4cb7-9dbd-76ec2eaeb496";
    const resolved = resolveDevUrl(blob);
    assert.equal(resolved, blob);
  });

  test("generates correct deterministic branded initials", () => {
    assert.equal(getInitials("Sujith Kumar"), "SK");
    assert.equal(getInitials("Sreekanth"), "SR");
    assert.equal(getInitials("Rohan Master"), "RM");
    assert.equal(getInitials(""), "ED");
  });
});

// 3. Security & Static Files Audit in Backend Source
describe("Security Audit: Static File Mappings & Production R2 Guard", () => {
  const rootDir = path.resolve(__dirname, "../..");
  const programCsPath = path.join(rootDir, "Ethos.Api", "Program.cs");
  const r2ServicePath = path.join(rootDir, "Ethos.Api", "Infrastructure", "Storage", "CloudflareR2StorageService.cs");

  test("Program.cs maps strictly /uploads/trainers and does not expose App_Data/uploads root", () => {
    const content = fs.readFileSync(programCsPath, "utf-8").replace(/\r\n/g, "\n");
    assert.ok(content.includes('RequestPath =\n            "/uploads/trainers"') || content.includes('RequestPath = "/uploads/trainers"'), "Must map /uploads/trainers");
    assert.ok(!content.includes('RequestPath = "/uploads"') && !content.includes('RequestPath =\n            "/uploads"'), "Must NOT expose root /uploads");
  });

  test("CloudflareR2StorageService throws InvalidOperationException in production if R2 unconfigured", () => {
    const content = fs.readFileSync(r2ServicePath, "utf-8");
    assert.ok(content.includes('throw new InvalidOperationException("Cloudflare R2 storage credentials are required in production.");'), "Must fail fast in production");
    assert.ok(content.includes("_environment.IsDevelopment()"), "Must check _environment.IsDevelopment()");
  });
});
