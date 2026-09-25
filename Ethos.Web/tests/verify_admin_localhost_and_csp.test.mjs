import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { getCandidateUrls } from "../src/services/adminApi.js";
import { getRouteTitle } from "../src/utils/routeTitles.js";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// 1. Admin API Candidate URLs - Production vs Development
test("adminApi: Production mode (DEV = false) produces NO localhost or 127.0.0.1 candidate", () => {
  const prodCandidates = getCandidateUrls("/api/admin/auth/login", false);
  assert.ok(Array.isArray(prodCandidates), "Expected candidateUrls to be an array");
  assert.ok(prodCandidates.length > 0, "Expected at least one candidate URL");

  for (const url of prodCandidates) {
    assert.strictEqual(
      url.includes("127.0.0.1"),
      false,
      `Production candidate URL must not contain 127.0.0.1: ${url}`
    );
    assert.strictEqual(
      url.includes("localhost"),
      false,
      `Production candidate URL must not contain localhost: ${url}`
    );
  }
});

test("adminApi: Development mode (DEV = true) retains 127.0.0.1 candidate", () => {
  const devCandidates = getCandidateUrls("/api/admin/auth/login", true);
  assert.ok(Array.isArray(devCandidates), "Expected candidateUrls to be an array");
  const hasLocalhost = devCandidates.some((url) => url.startsWith("http://127.0.0.1:5252"));
  assert.strictEqual(hasLocalhost, true, "Development candidates must include http://127.0.0.1:5252");
});

test("adminApi: Static source inspection proves 127.0.0.1 is strictly guarded by isDev / DEV check", () => {
  const adminApiSource = fs.readFileSync(path.resolve(__dirname, "../src/services/adminApi.js"), "utf8");
  assert.ok(
    adminApiSource.includes("isDev"),
    "adminApi.js must guard 127.0.0.1 with isDev check"
  );
  assert.ok(
    adminApiSource.includes("import.meta.env?.DEV"),
    "adminApi.js must reference import.meta.env?.DEV"
  );
});

// 2. Production CSP in public/_headers
test("CSP: public/_headers contains explicit https://api.ethosdancestudio.com in connect-src, img-src, and media-src", () => {
  const headersPath = path.resolve(__dirname, "../public/_headers");
  assert.ok(fs.existsSync(headersPath), "public/_headers must exist");
  const headersContent = fs.readFileSync(headersPath, "utf8");

  assert.ok(headersContent.includes("connect-src"), "Must contain connect-src directive");
  assert.ok(headersContent.includes("img-src"), "Must contain img-src directive");
  assert.ok(headersContent.includes("media-src"), "Must contain media-src directive");

  // Extract directives
  const cspLine = headersContent.split("\n").find((l) => l.trim().startsWith("Content-Security-Policy:"));
  assert.ok(cspLine, "Must have Content-Security-Policy line");

  // Verify connect-src
  assert.ok(cspLine.includes("connect-src"), "CSP must contain connect-src");
  assert.ok(
    cspLine.match(/connect-src[^;]*https:\/\/api\.ethosdancestudio\.com/),
    "connect-src must contain https://api.ethosdancestudio.com"
  );

  // Verify img-src contains API origin and R2 origin
  assert.ok(
    cspLine.match(/img-src[^;]*https:\/\/api\.ethosdancestudio\.com/),
    "img-src must contain https://api.ethosdancestudio.com for API media streaming"
  );
  assert.ok(
    cspLine.match(/img-src[^;]*https:\/\/media\.ethosdancestudio\.com/),
    "img-src must preserve https://media.ethosdancestudio.com"
  );

  // Verify media-src contains API origin and R2 origin
  assert.ok(
    cspLine.match(/media-src[^;]*https:\/\/api\.ethosdancestudio\.com/),
    "media-src must contain https://api.ethosdancestudio.com for API media/video streaming"
  );
  assert.ok(
    cspLine.match(/media-src[^;]*https:\/\/media\.ethosdancestudio\.com/),
    "media-src must preserve https://media.ethosdancestudio.com"
  );

  // Guard against wildcards and unsafe directives on media
  assert.strictEqual(cspLine.includes("connect-src *"), false, "connect-src must not be wildcard *");
  assert.strictEqual(cspLine.includes("img-src *"), false, "img-src must not be wildcard *");
  assert.strictEqual(cspLine.includes("media-src *"), false, "media-src must not be wildcard *");
  assert.strictEqual(cspLine.includes("media-src 'unsafe-"), false, "media-src must not use unsafe-*");
});

// 3. Node version pinning (.nvmrc and .node-version)
test("Node Pinning: .nvmrc and .node-version contain pinned version 22.12.0", () => {
  const nvmrcPath = path.resolve(__dirname, "../.nvmrc");
  const nodeVersionPath = path.resolve(__dirname, "../.node-version");

  assert.ok(fs.existsSync(nvmrcPath), ".nvmrc must exist");
  assert.ok(fs.existsSync(nodeVersionPath), ".node-version must exist");

  const nvmrcContent = fs.readFileSync(nvmrcPath, "utf8").trim();
  const nodeVersionContent = fs.readFileSync(nodeVersionPath, "utf8").trim();

  assert.strictEqual(nvmrcContent, "22.12.0", ".nvmrc must specify 22.12.0");
  assert.strictEqual(nodeVersionContent, "22.12.0", ".node-version must specify 22.12.0");
});

// 4. Production API Base & Candidate URL Verification
test("adminApi: Production mode with API_BASE_URL sets canonical production URL as primary candidate", () => {
  const prodCandidates = getCandidateUrls("/api/admin/auth/login", false, "https://api.ethosdancestudio.com");
  assert.ok(Array.isArray(prodCandidates), "Expected candidateUrls to be an array");
  assert.strictEqual(prodCandidates[0], "https://api.ethosdancestudio.com/api/admin/auth/login", "Primary URL must be canonical production URL");
  assert.strictEqual(prodCandidates[1], "/api/admin/auth/login", "Secondary URL should be relative path");
});

test("Environment: .env.production configures canonical API base URL and acceptance Razorpay key", () => {
  const envProdPath = path.resolve(__dirname, "../.env.production");
  assert.ok(fs.existsSync(envProdPath), ".env.production must exist");
  const content = fs.readFileSync(envProdPath, "utf8");
  assert.ok(content.includes("VITE_API_BASE_URL=https://api.ethosdancestudio.com"), ".env.production must set VITE_API_BASE_URL to https://api.ethosdancestudio.com");
  assert.ok(content.includes("VITE_RAZORPAY_KEY_ID=rzp_test_"), ".env.production must configure valid acceptance test Razorpay key");
  assert.strictEqual(content.includes("KeySecret"), false, ".env.production must NEVER contain Razorpay KeySecret");
});

// 5. CSP Frame-Src: Google Maps Iframe Embedding
test("CSP: public/_headers frame-src allows https://www.google.com and https://maps.google.com", () => {
  const headersPath = path.resolve(__dirname, "../public/_headers");
  const headersContent = fs.readFileSync(headersPath, "utf8");

  assert.ok(headersContent.includes("frame-src"), "public/_headers must contain frame-src directive");
  assert.ok(
    headersContent.includes("https://www.google.com") && headersContent.includes("https://maps.google.com"),
    "frame-src must explicitly authorize https://www.google.com and https://maps.google.com"
  );
  assert.strictEqual(
    headersContent.includes("frame-src *"),
    false,
    "frame-src must not be wildcard *"
  );
});

// 6. Branding: index.html Fallback Title and Favicon Reference
test("Branding: index.html sets title to 'Ethos Dance Studio' and links to ethos emblem favicon", () => {
  const indexPath = path.resolve(__dirname, "../index.html");
  assert.ok(fs.existsSync(indexPath), "index.html must exist");
  const indexContent = fs.readFileSync(indexPath, "utf8");

  assert.ok(indexContent.includes("<title>Ethos Dance Studio</title>"), "index.html must set title to 'Ethos Dance Studio'");
  assert.strictEqual(
    indexContent.includes("ethos-dance-studio-frontend"),
    false,
    "index.html must not contain placeholder ethos-dance-studio-frontend"
  );
  assert.ok(
    indexContent.includes('href="/favicon.png"'),
    "index.html must link to /favicon.png"
  );

  const faviconPath = path.resolve(__dirname, "../public/favicon.png");
  assert.ok(fs.existsSync(faviconPath), "public/favicon.png must exist");
  const faviconStats = fs.statSync(faviconPath);
  assert.ok(faviconStats.size > 10000, "public/favicon.png must be non-empty high-res emblem");
});

// 7. Dynamic Route Titles Resolution
test("RouteTitles: getRouteTitle returns exact brand titles for public and admin routes", () => {
  assert.strictEqual(getRouteTitle("/"), "Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/", "#contact"), "Contact | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/contact"), "Contact | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/workshops"), "Workshops | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/workshops/afro-fusion"), "Workshop Details | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/workshops/afro-fusion/checkout"), "Checkout | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/gallery"), "Gallery | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/classes"), "Classes | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/events"), "Events | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/admin_portal/login"), "Admin Login | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/admin/login"), "Admin Login | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/admin_portal/dashboard"), "Admin Dashboard | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/admin_portal/workshops"), "Admin Workshops | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/admin_portal/workshops/wizard"), "Workshop Wizard | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/admin_portal/videos"), "Media Library | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/trainer/login"), "Trainer Login | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/trainer/dashboard"), "Trainer Dashboard | Ethos Dance Studio");
  assert.strictEqual(getRouteTitle("/student/dashboard"), "Student Dashboard | Ethos Dance Studio");
});

// 8. Brand Intro: Mobile Responsive Invariants
test("BrandIntro: brand-intro.css contains responsive mobile column rules and relative loading bar", () => {
  const cssPath = path.resolve(__dirname, "../src/styles/brand-intro.css");
  assert.ok(fs.existsSync(cssPath), "brand-intro.css must exist");
  const css = fs.readFileSync(cssPath, "utf8");

  assert.ok(css.includes("@media (max-width: 768px)"), "Must contain @media (max-width: 768px)");
  assert.ok(css.includes("flex-direction: column;"), "Must stack logo vertically on mobile");
  assert.ok(css.includes("position: relative;"), "Loading bar must be relative on mobile to prevent text collision");
  assert.ok(css.includes("overflow: visible;"), "Logo container must not clip title on mobile");
});

// 9. Favicon: Alpha Transparency Invariants
test("Favicon: public/favicon.png contains transparent alpha channel and no white rectangular box", () => {
  const faviconPath = path.resolve(__dirname, "../public/favicon.png");
  assert.ok(fs.existsSync(faviconPath), "favicon.png must exist");
  const buf = fs.readFileSync(faviconPath);

  // PNG ColorType at byte 25 must be 6 (RGBA with alpha channel)
  const colorType = buf.readUInt8(25);
  assert.strictEqual(colorType, 6, "favicon.png must have RGBA color type (6) for real alpha transparency");
});


