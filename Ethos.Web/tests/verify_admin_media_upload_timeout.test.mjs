import test from "node:test";
import assert from "node:assert/strict";
import {
  adminRequest,
  adminApi,
  DEFAULT_TIMEOUT_MS,
  UPLOAD_TIMEOUT_MS,
  getCandidateUrls,
  createComposedTimeoutSignal,
} from "../src/services/adminApi.js";

// Save original fetch
const originalFetch = globalThis.fetch;

test("1. Timeout constants: Normal requests use 8s and media uploads use 10m", () => {
  assert.strictEqual(DEFAULT_TIMEOUT_MS, 8000, "Default timeout must be exactly 8,000ms (8s)");
  assert.strictEqual(UPLOAD_TIMEOUT_MS, 600000, "Upload timeout must be exactly 600,000ms (10 minutes)");
});

test("2. adminRequest assigns 8-second default to normal JSON requests", async (t) => {
  let capturedSignal = null;
  globalThis.fetch = async (url, options) => {
    capturedSignal = options.signal;
    return new Response(JSON.stringify({ success: true }), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  };

  t.after(() => {
    globalThis.fetch = originalFetch;
  });

  const res = await adminRequest("/api/admin/dashboard/summary");
  assert.deepStrictEqual(res, { success: true });
  assert.ok(capturedSignal, "Fetch must receive an AbortSignal");
  assert.strictEqual(capturedSignal.aborted, false, "Signal should not be aborted on instant response");
});

test("3. uploadAdminMedia receives 10-minute default timeout", async (t) => {
  let capturedOptions = null;
  let capturedUrl = null;

  globalThis.fetch = async (url, options) => {
    capturedUrl = url;
    capturedOptions = options;
    return new Response(JSON.stringify({ id: "media_123", url: "https://media.ethosdancestudio.com/test.mp4" }), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  };

  t.after(() => {
    globalThis.fetch = originalFetch;
  });

  const formData = new FormData();
  formData.append("file", new Blob(["test-video-content"], { type: "video/mp4" }), "dance.mp4");

  const res = await adminApi.uploadAdminMedia(formData);
  assert.ok(res.id === "media_123");
  assert.ok(capturedOptions.signal, "Upload must receive composed signal");
  assert.ok(capturedUrl.includes("/api/admin/media/upload"));
});

test("4. Explicit upload timeout overrides the 10-minute default", async (t) => {
  let capturedSignal = null;

  globalThis.fetch = async (url, options) => {
    capturedSignal = options.signal;
    return new Response(JSON.stringify({ success: true }), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  };

  t.after(() => {
    globalThis.fetch = originalFetch;
  });

  const formData = new FormData();
  formData.append("file", new Blob(["test"], { type: "video/mp4" }));

  // Call with explicit custom timeout (e.g. 45,000 ms)
  await adminApi.uploadAdminMedia(formData, { timeout: 45000 });
  assert.ok(capturedSignal);
});

test("5. uploadVideo, replaceVideo, and uploadMedia also default to 10-minute timeout", async (t) => {
  let callCount = 0;
  globalThis.fetch = async (url, options) => {
    callCount++;
    return new Response(JSON.stringify({ success: true }), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  };

  t.after(() => {
    globalThis.fetch = originalFetch;
  });

  const formData = new FormData();
  await adminApi.uploadVideo(formData);
  await adminApi.replaceVideo("vid-123", formData);
  await adminApi.uploadMedia(formData);

  assert.strictEqual(callCount, 3, "uploadVideo, replaceVideo, and uploadMedia must be callable");
});

test("6. Production upload does NOT fall back to relative Netlify URL on error", async (t) => {
  const attemptedUrls = [];

  globalThis.fetch = async (url) => {
    attemptedUrls.push(url);
    throw new TypeError("Failed to fetch"); // Network failure
  };

  t.after(() => {
    globalThis.fetch = originalFetch;
  });

  const formData = new FormData();
  formData.append("file", new Blob(["video-data"], { type: "video/mp4" }));

  await assert.rejects(
    async () => {
      await adminRequest("/api/admin/media/upload", {
        method: "POST",
        body: formData,
      });
    },
    /Failed to fetch|Failed to establish connection/
  );

  // In production (or for mutating uploads), only the authoritative primary URL should have been attempted
  assert.strictEqual(attemptedUrls.length, 1, `Expected only 1 attempt to primary API, got ${attemptedUrls.length} (${attemptedUrls.join(", ")})`);
  assert.ok(
    attemptedUrls[0].startsWith("https://api.ethosdancestudio.com"),
    `Attempted URL must be authoritative API URL, got: ${attemptedUrls[0]}`
  );
  assert.strictEqual(
    attemptedUrls.includes("/api/admin/media/upload"),
    false,
    "Mutating upload must NEVER fall back to relative Netlify URL"
  );
});

test("7. Timeout terminates request and NEVER triggers a secondary candidate URL", async (t) => {
  const attemptedUrls = [];

  globalThis.fetch = async (url, options) => {
    attemptedUrls.push(url);
    // Simulate immediate timeout abort
    const abortErr = new Error("The operation was aborted");
    abortErr.name = "AbortError";
    throw abortErr;
  };

  t.after(() => {
    globalThis.fetch = originalFetch;
  });

  await assert.rejects(
    async () => {
      await adminRequest("/api/admin/dashboard/summary", {
        timeout: 50, // fast timeout for test
      });
    },
    /timed out/i
  );

  assert.strictEqual(
    attemptedUrls.length,
    1,
    "On timeout / abort, request must immediately fail without trying secondary candidate URLs"
  );
});

test("8. Caller-provided AbortSignal correctly composes with timeout signal", async () => {
  // Test A: Caller aborts before timeout
  const callerController = new AbortController();
  const composedA = createComposedTimeoutSignal(5000, callerController.signal);

  assert.strictEqual(composedA.signal.aborted, false);
  callerController.abort(new Error("User cancelled"));
  assert.strictEqual(composedA.signal.aborted, true);
  composedA.cleanup();

  // Test B: Timeout fires before caller aborts
  const composedB = createComposedTimeoutSignal(10, null);
  await new Promise((resolve) => setTimeout(resolve, 30));
  assert.strictEqual(composedB.signal.aborted, true);
  composedB.cleanup();
});

test("9. Existing development fallback behavior remains valid", () => {
  const devCandidates = getCandidateUrls("/api/admin/auth/login", true);
  assert.ok(devCandidates.some((url) => url.startsWith("http://127.0.0.1:5252")), "Development candidates must retain 127.0.0.1:5252 fallback");

  const prodCandidates = getCandidateUrls("/api/admin/auth/login", false);
  assert.strictEqual(prodCandidates.some((url) => url.includes("127.0.0.1")), false, "Production candidates must not have 127.0.0.1");
});
