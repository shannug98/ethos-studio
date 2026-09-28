import { describe, it } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webSrcDir = path.resolve(__dirname, "../src");

describe("Homepage Workshop 3-Slot Grid Layout Verification", () => {
  const workshopsPath = path.join(webSrcDir, "components/home/Workshops.jsx");
  const cssPath = path.join(webSrcDir, "styles/workshops.css");

  it("Workshops.jsx exists and defines fixed 3 smallSlots", () => {
    assert.ok(fs.existsSync(workshopsPath), "Workshops.jsx must exist");
    const content = fs.readFileSync(workshopsPath, "utf-8");

    // Must define smallSlots with 3 fixed slots
    assert.ok(
      content.includes("const smallSlots = [") &&
      content.includes("upcomingList[0] || null") &&
      content.includes("upcomingList[1] || null") &&
      content.includes("upcomingList[2] || null"),
      "Workshops.jsx must define fixed 3-slot array: upcomingList[0..2] || null"
    );

    // Must NOT have inline grid-template-columns overriding the 3-column layout
    assert.ok(
      !content.includes('gridTemplateColumns: upcomingList.length === 1'),
      "Workshops.jsx must not dynamically collapse gridTemplateColumns on desktop"
    );

    // Must render empty slot placeholder with workshop-card--empty
    assert.ok(
      content.includes("workshop-card--empty"),
      "Workshops.jsx must render workshop-card--empty placeholder for empty slots"
    );
  });

  it("workshops.css defines fixed 3-column grid and empty slot placeholder styles", () => {
    assert.ok(fs.existsSync(cssPath), "workshops.css must exist");
    const cssContent = fs.readFileSync(cssPath, "utf-8");

    // Check .workshops__list grid-template-columns
    assert.ok(
      cssContent.includes("grid-template-columns: repeat(3, minmax(0, 1fr))"),
      "workshops.css must define fixed 3-column repeat(3, minmax(0, 1fr)) grid"
    );

    // Check .workshop-card--empty
    assert.ok(
      cssContent.includes(".workshop-card--empty"),
      "workshops.css must define .workshop-card--empty"
    );

    // Check .workshop-card-media--empty
    assert.ok(
      cssContent.includes(".workshop-card-media--empty"),
      "workshops.css must define .workshop-card-media--empty"
    );
  });

  it("Slot behavior simulation: 0, 1, 2, 3, 4 workshops correctly maps slots without fake data", () => {
    function deriveSlots(workshops) {
      if (!workshops || workshops.length === 0) {
        return { emptyState: true, featured: null, smallSlots: [] };
      }
      const displayPool = workshops.slice(0, 4);
      const featured = displayPool[0] || null;
      const upcomingList = displayPool.slice(1, 4);
      const smallSlots = [
        upcomingList[0] || null,
        upcomingList[1] || null,
        upcomingList[2] || null,
      ];
      return { emptyState: false, featured, smallSlots };
    }

    const w1 = { id: 1, title: "Workshop 1" };
    const w2 = { id: 2, title: "Workshop 2" };
    const w3 = { id: 3, title: "Workshop 3" };
    const w4 = { id: 4, title: "Workshop 4" };

    // Case 0: 0 workshops
    const case0 = deriveSlots([]);
    assert.equal(case0.emptyState, true);
    assert.equal(case0.featured, null);
    assert.deepEqual(case0.smallSlots, []);

    // Case 1: 1 workshop
    const case1 = deriveSlots([w1]);
    assert.equal(case1.emptyState, false);
    assert.equal(case1.featured.id, 1);
    assert.equal(case1.smallSlots.length, 3);
    assert.equal(case1.smallSlots[0], null, "Slot 1 must be EMPTY");
    assert.equal(case1.smallSlots[1], null, "Slot 2 must be EMPTY");
    assert.equal(case1.smallSlots[2], null, "Slot 3 must be EMPTY");

    // Case 2: 2 workshops
    const case2 = deriveSlots([w1, w2]);
    assert.equal(case2.emptyState, false);
    assert.equal(case2.featured.id, 1);
    assert.equal(case2.smallSlots.length, 3);
    assert.equal(case2.smallSlots[0].id, 2, "Slot 1 must be Workshop 2");
    assert.equal(case2.smallSlots[1], null, "Slot 2 must be EMPTY");
    assert.equal(case2.smallSlots[2], null, "Slot 3 must be EMPTY");

    // Case 3: 3 workshops
    const case3 = deriveSlots([w1, w2, w3]);
    assert.equal(case3.emptyState, false);
    assert.equal(case3.featured.id, 1);
    assert.equal(case3.smallSlots.length, 3);
    assert.equal(case3.smallSlots[0].id, 2, "Slot 1 must be Workshop 2");
    assert.equal(case3.smallSlots[1].id, 3, "Slot 2 must be Workshop 3");
    assert.equal(case3.smallSlots[2], null, "Slot 3 must be EMPTY");

    // Case 4: 4 workshops
    const case4 = deriveSlots([w1, w2, w3, w4]);
    assert.equal(case4.emptyState, false);
    assert.equal(case4.featured.id, 1);
    assert.equal(case4.smallSlots.length, 3);
    assert.equal(case4.smallSlots[0].id, 2, "Slot 1 must be Workshop 2");
    assert.equal(case4.smallSlots[1].id, 3, "Slot 2 must be Workshop 3");
    assert.equal(case4.smallSlots[2].id, 4, "Slot 3 must be Workshop 4");
  });
});
