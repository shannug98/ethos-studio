import { test, describe } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import QRCode from "qrcode";
import jsQR from "jsqr";

describe("Part 1: QR Code Generation & Decoding Verification", () => {
  test("QrCode component source uses standard 'qrcode' package and does not use pseudo-hash bits", () => {
    const qrCodePath = path.resolve("src/components/common/QrCode.jsx");
    assert.ok(fs.existsSync(qrCodePath), "QrCode.jsx must exist");
    const content = fs.readFileSync(qrCodePath, "utf-8");

    // Must import qrcode
    assert.ok(content.includes('import QRCode from "qrcode"') || content.includes("import QRCode from 'qrcode'"), "Must import official qrcode package");
    // Must NOT contain old pseudo-hashing formula
    assert.ok(!content.includes("0x811c9dc5"), "Must not contain custom pseudo-hash seed");
    assert.ok(!content.includes("Math.imul(hash, 0x01000193)"), "Must not contain custom pseudo-hash math");
  });

  test("Generates standards-compliant ISO/IEC 18004 QR codes that decode reliably with jsQR", () => {
    const testTokens = [
      "ETHOS-TKT-3f8a9e01b4c5d6e7f8a9b0c1d2e3f4a56b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e",
      "ETHOS-WKS-7F8A9B0C-01",
      "https://ethosdancestudio.com/feedback?token=sample-feedback-token-12345"
    ];

    for (const token of testTokens) {
      const qr = QRCode.create(token, { errorCorrectionLevel: "M" });
      const N = qr.modules.size;
      const margin = 4;
      const scale = 8;
      const totalModules = N + margin * 2;
      const imgSize = totalModules * scale;

      // Construct raw RGBA buffer for decoder with proper module scaling
      const imgData = new Uint8ClampedArray(imgSize * imgSize * 4);
      imgData.fill(255); // White background

      for (let r = 0; r < N; r++) {
        for (let c = 0; c < N; c++) {
          if (qr.modules.get(r, c)) {
            for (let sy = 0; sy < scale; sy++) {
              for (let sx = 0; sx < scale; sx++) {
                const px = (c + margin) * scale + sx;
                const py = (r + margin) * scale + sy;
                const idx = (py * imgSize + px) * 4;
                imgData[idx] = 0;     // R
                imgData[idx + 1] = 0; // G
                imgData[idx + 2] = 0; // B
                imgData[idx + 3] = 255;
              }
            }
          }
        }
      }

      const decoded = jsQR(imgData, imgSize, imgSize);
      assert.ok(decoded, `QR code for '${token}' must be decoded by standard QR decoder`);
      assert.equal(decoded.data, token, "Decoded token must match input token exactly");
    }
  });
});

describe("Part 2: Admin QR Scanner Configuration & Mobile Optimization", () => {
  test("AdminWorkshopScanner uses responsive scanner configuration", () => {
    const scannerPath = path.resolve("src/pages/admin/workshops/AdminWorkshopScanner.jsx");
    const content = fs.readFileSync(scannerPath, "utf-8");

    assert.ok(content.includes("formatsToSupport: [Html5QrcodeSupportedFormats.QR_CODE]"), "Must restrict scanner to QR_CODE");
    assert.ok(content.includes("disableFlip: true"), "Must maintain disableFlip to prevent camera mirroring");
    assert.ok(!content.includes("qrbox: (viewfinderWidth"), "Must use full-frame decoding without restrictive crop");
  });

  test("AdminWorkshopScanner CSS provides responsive camera viewport and styling", () => {
    const cssPath = path.resolve("src/pages/admin/workshops/AdminWorkshopScanner.css");
    const content = fs.readFileSync(cssPath, "utf-8");

    assert.ok(content.includes(".viewfinder-container"), "Viewfinder container class must be present");
    assert.ok(content.includes(".viewfinder-box"), "Viewfinder box class must be present");
    assert.ok(content.includes(".corner-bracket"), "Corner bracket styling must be present");
  });
});

describe("Part 3: Admin Workshop Feedback Automation View", () => {
  test("AdminWorkshopFeedback shows real template names and dynamic variables without truncation", () => {
    const feedbackPath = path.resolve("src/pages/admin/workshops/AdminWorkshopFeedback.jsx");
    const content = fs.readFileSync(feedbackPath, "utf-8");

    // Real backend template names
    assert.ok(content.includes("ethos_feedback_attended"), "Must show real attended template name ethos_feedback_attended");
    assert.ok(content.includes("ethos_feedback_noshow"), "Must show real no-show template name ethos_feedback_noshow");

    // Full variables and preview
    assert.ok(content.includes("{{1}}") || content.includes("{{name}}"), "Must show attendee name parameter");
    assert.ok(content.includes("{{2}}") || content.includes("{{workshop_name}}"), "Must show workshop title parameter");
    assert.ok(content.includes("feedback/workshop/") || content.includes("feedback?token="), "Must show feedback link structure");
    assert.ok(content.includes("Rahul"), "Must render realistic attendee sample");
    assert.ok(content.includes("Meta WhatsApp Policy Notice") || content.includes("Meta"), "Must include Meta policy notice");
  });
});

describe("Part 4: Preservation of Wizard Base64 in-memory decoding", () => {
  test("AdminWorkshopWizard retains atob + Uint8Array in-memory decoding", () => {
    const wizardPath = path.resolve("src/pages/admin/workshops/wizard/AdminWorkshopWizard.jsx");
    const content = fs.readFileSync(wizardPath, "utf-8");

    assert.ok(content.includes("const bstr = atob(parts[1]);"), "Must decode Base64 in-memory with atob");
    assert.ok(content.includes("const u8arr = new Uint8Array(n);"), "Must convert to Uint8Array");
    assert.ok(!content.includes("const res = await fetch(dataUrl);"), "Must NOT use fetch(dataUrl) which violates CSP");
  });
});
