import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, "..");

test("ImageCropperModal - TDZ Regression & Preset Resolution Verification", async (t) => {
  const cropperPath = path.join(projectRoot, "src", "components", "admin", "common", "ImageCropperModal.jsx");
  assert.ok(fs.existsSync(cropperPath), "ImageCropperModal.jsx must exist");

  const content = fs.readFileSync(cropperPath, "utf-8");

  await t.test("activePreset declaration precedes baseDimensions memoization", () => {
    const activePresetIndex = content.indexOf("const activePreset = useMemo(");
    const baseDimensionsIndex = content.indexOf("const baseDimensions = useMemo(");

    assert.ok(activePresetIndex !== -1, "activePreset must be declared via useMemo");
    assert.ok(baseDimensionsIndex !== -1, "baseDimensions must be declared via useMemo");
    assert.ok(
      activePresetIndex < baseDimensionsIndex,
      `activePreset declaration (pos: ${activePresetIndex}) MUST occur before baseDimensions (pos: ${baseDimensionsIndex}) to prevent Temporal Dead Zone ReferenceError`
    );
  });

  await t.test("Crop presets support all standard ratios (16:9, 3:4, 1:1, natural)", () => {
    assert.ok(content.includes('"16:9":'), "Must define 16:9 preset");
    assert.ok(content.includes('"3:4":'), "Must define 3:4 preset");
    assert.ok(content.includes('"1:1":'), "Must define 1:1 preset");
    assert.ok(content.includes('"natural":'), "Must define natural preset");
  });

  await t.test("AdminErrorBoundary provides contextual guidance for Image Editor errors", () => {
    const boundaryPath = path.join(projectRoot, "src", "components", "admin", "AdminErrorBoundary.jsx");
    assert.ok(fs.existsSync(boundaryPath), "AdminErrorBoundary.jsx must exist");
    const boundaryContent = fs.readFileSync(boundaryPath, "utf-8");

    assert.ok(boundaryContent.includes("Image Editor Interruption"), "Boundary must classify image editor interruptions");
    assert.ok(boundaryContent.includes("activepreset"), "Boundary must detect activepreset errors");
    assert.ok(boundaryContent.includes("CORRELATION REFERENCE"), "Boundary must provide correlation reference");
    assert.ok(boundaryContent.includes("admin-error-tech-accordion"), "Boundary must include expandable developer accordion");
  });
});
