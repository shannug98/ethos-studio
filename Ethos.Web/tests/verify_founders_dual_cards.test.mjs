import { describe, it } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webSrcDir = path.resolve(__dirname, "../src");

describe("Founders Section Dual-Card & Admin Media Gallery Verification", () => {
  const foundersJsxPath = path.join(webSrcDir, "components/home/Founders.jsx");
  const catalogPath = path.join(webSrcDir, "config/MediaPlacementCatalog.js");
  const adminVideosPath = path.join(webSrcDir, "pages/admin/AdminVideos.jsx");
  const foundersCssPath = path.join(webSrcDir, "styles/founders.css");

  it("MediaPlacementCatalog.js configures 2 independent Founder slots", () => {
    assert.ok(fs.existsSync(catalogPath), "MediaPlacementCatalog.js must exist");
    const content = fs.readFileSync(catalogPath, "utf-8");

    // Founder placement definition
    assert.ok(content.includes('id: "founder"'), "Must contain id: 'founder'");
    assert.ok(content.includes('slotCount: 2'), "Must specify slotCount: 2");
    assert.ok(content.includes('limit: 2'), "Must specify limit: 2");
    assert.ok(content.includes('aspectRatio: "3:4 Portrait"'), "Must specify 3:4 portrait aspect ratio");

    // Must define slot 1 and slot 2
    assert.ok(content.includes('Slot 01: Founder 1'), "Must define Slot 01: Founder 1");
    assert.ok(content.includes('Slot 02: Founder 2'), "Must define Slot 02: Founder 2");
    assert.ok(content.includes('Co-Founder & Lead Choreographer'), "Must list Founder 1 role");
    assert.ok(content.includes('Co-Founder & Executive Director'), "Must list Founder 2 role");
  });

  it("AdminVideos.jsx renders 2-slot Founders placement card", () => {
    assert.ok(fs.existsSync(adminVideosPath), "AdminVideos.jsx must exist");
    const content = fs.readFileSync(adminVideosPath, "utf-8");

    // Must show Founders with 2 images
    assert.ok(content.includes('Founders</h4>'), "Must have Founders placement card");
    assert.ok(content.includes('2 images • Images only'), "Must describe 2 images limit");
    assert.ok(content.includes('{founderItems.length} / 2'), "Must show founderItems / 2 count badge");
    assert.ok(content.includes('setManagingPlacementId("founder")'), "Must wire Manage button to founder placement");
  });

  it("Founders.jsx renders dual-card architecture with zero hardcoded image URLs", () => {
    assert.ok(fs.existsSync(foundersJsxPath), "Founders.jsx must exist");
    const content = fs.readFileSync(foundersJsxPath, "utf-8");

    // Must NOT have hardcoded image paths
    assert.ok(!content.includes("/founder1.jpg"), "Must not have hardcoded /founder1.jpg");
    assert.ok(!content.includes("/founder2.jpg"), "Must not have hardcoded /founder2.jpg");
    assert.ok(!content.includes("/founders.jpg"), "Must not have hardcoded /founders.jpg");

    // Must use dynamic publicApi.getPublicMedia({ section: "Founders" })
    assert.ok(
      content.includes('getPublicMedia({ section: "Founders" })'),
      "Must fetch public media for 'Founders' section"
    );

    // Must render .founders-dual-grid and .founder-card
    assert.ok(content.includes('className="founders-dual-grid"'), "Must render dual-grid container");
    assert.ok(content.includes('className="founder-card"'), "Must render individual founder cards");

    // Must render empty-state fallback when image is missing
    assert.ok(
      content.includes('founder-card__image--empty'),
      "Must render truthful empty state placeholder when photo is missing"
    );
    assert.ok(
      content.includes('PHOTO TO BE UPLOADED'),
      "Must display branded 'PHOTO TO BE UPLOADED' text"
    );
  });

  it("founders.css contains 2-column grid and mobile responsive stacking", () => {
    assert.ok(fs.existsSync(foundersCssPath), "founders.css must exist");
    const cssContent = fs.readFileSync(foundersCssPath, "utf-8");

    assert.ok(
      cssContent.includes(".founders-dual-grid"),
      "Must contain .founders-dual-grid class"
    );
    assert.ok(
      cssContent.includes("grid-template-columns: repeat(2, minmax(0, 1fr))"),
      "Must define 2-column desktop grid for equal visual importance"
    );
    assert.ok(
      cssContent.includes(".founder-card__image--empty"),
      "Must style .founder-card__image--empty container"
    );
    assert.ok(
      cssContent.includes("@media (max-width: 900px)") || cssContent.includes("@media (max-width: 1024px)"),
      "Must include responsive media query for smaller screens"
    );
  });

  it("Media mapping logic simulation: non-duplication and isolated slots", () => {
    function mapFounders(items) {
      const item1 = items.find((m) => Number(m.displayOrder) === 1) || (items[0] && Number(items[0].displayOrder) !== 2 ? items[0] : null);
      const item2 = items.find((m) => Number(m.displayOrder) === 2) || (items[1] && items[1] !== item1 ? items[1] : null);

      return {
        founder1: item1 ? item1.url : null,
        founder2: item2 ? item2.url : null,
      };
    }

    // Case 0: No uploaded photos
    const case0 = mapFounders([]);
    assert.equal(case0.founder1, null, "Founder 1 should be empty");
    assert.equal(case0.founder2, null, "Founder 2 should be empty");

    // Case 1: Only Founder 1 uploaded
    const f1Only = [{ id: "m1", displayOrder: 1, url: "https://r2.ethos.in/founder1.webp" }];
    const case1 = mapFounders(f1Only);
    assert.equal(case1.founder1, "https://r2.ethos.in/founder1.webp");
    assert.equal(case1.founder2, null, "Founder 2 MUST remain null / empty state, not duplicated");

    // Case 2: Only Founder 2 uploaded
    const f2Only = [{ id: "m2", displayOrder: 2, url: "https://r2.ethos.in/founder2.webp" }];
    const case2 = mapFounders(f2Only);
    assert.equal(case2.founder1, null, "Founder 1 MUST remain null / empty state");
    assert.equal(case2.founder2, "https://r2.ethos.in/founder2.webp");

    // Case 3: Both Founder 1 and Founder 2 uploaded
    const both = [
      { id: "m1", displayOrder: 1, url: "https://r2.ethos.in/f1.webp" },
      { id: "m2", displayOrder: 2, url: "https://r2.ethos.in/f2.webp" },
    ];
    const case3 = mapFounders(both);
    assert.equal(case3.founder1, "https://r2.ethos.in/f1.webp");
    assert.equal(case3.founder2, "https://r2.ethos.in/f2.webp");
  });
});
