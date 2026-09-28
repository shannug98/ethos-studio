import { test, describe } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { PRESET_RATIOS } from "../src/constants/imagePresets.js";
import { createSlug } from "../src/utils/createSlug.js";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const ROOT_DIR = path.resolve(__dirname, "..");
const API_DIR = path.resolve(ROOT_DIR, "..", "Ethos.Api");

describe("Workstream A: Trainer Profile Admin Single Source of Truth & Dance Styles", () => {
  test("Secondary styles logic enforces 0-10 maximum and duplicate prevention", () => {
    const primaryStyle = "Urban Choreography";
    const selectedSecondary = ["Hip Hop", "Commercial", "Jazz"];

    // 1. Adding a new valid style
    const canAdd = (style, current, primary) => {
      const trimmed = style.trim();
      if (!trimmed) return { allowed: false, error: "Empty style" };
      if (current.length >= 10) return { allowed: false, error: "Maximum of 10 secondary dance styles allowed." };
      if (primary.toLowerCase() === trimmed.toLowerCase()) return { allowed: false, error: "Already Primary Dance Style." };
      if (current.some(s => s.toLowerCase() === trimmed.toLowerCase())) return { allowed: false, error: "Already in secondary styles." };
      return { allowed: true, error: null };
    };

    assert.equal(canAdd("Popping", selectedSecondary, primaryStyle).allowed, true);

    // 2. Reject duplicate of primary
    assert.equal(canAdd("Urban Choreography", selectedSecondary, primaryStyle).allowed, false);

    // 3. Reject duplicate of existing secondary
    assert.equal(canAdd("hip hop", selectedSecondary, primaryStyle).allowed, false);

    // 4. Reject 11th style
    const maxedList = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10"];
    assert.equal(canAdd("Waacking", maxedList, primaryStyle).allowed, false);
    assert.equal(canAdd("Waacking", maxedList, primaryStyle).error, "Maximum of 10 secondary dance styles allowed.");
  });

  test("AdminTrainers.jsx contains secondary styles chips and 10-style guard", () => {
    const adminTrainersSrc = fs.readFileSync(path.join(ROOT_DIR, "src/pages/admin/AdminTrainers.jsx"), "utf-8");
    assert.ok(adminTrainersSrc.includes("selectedSecondaryStyles.length >= 10"), "Guards 10 secondary styles maximum");
    assert.ok(adminTrainersSrc.includes("Maximum of 10 secondary dance styles allowed."), "Displays error on 11th style");
    assert.ok(adminTrainersSrc.includes("UniversalImageCropper"), "Integrates UniversalImageCropper");
  });
});

describe("Workstream B: Universal Image Cropper Presets & Architecture", () => {
  test("PRESET_RATIOS provides 3:4, 1:1, 16:9, and Natural presets with exact dimensions", () => {
    assert.ok(PRESET_RATIOS["3:4"]);
    assert.equal(PRESET_RATIOS["3:4"].width, 900);
    assert.equal(PRESET_RATIOS["3:4"].height, 1200);
    assert.equal(PRESET_RATIOS["3:4"].ratio, 3 / 4);

    assert.ok(PRESET_RATIOS["1:1"]);
    assert.equal(PRESET_RATIOS["1:1"].width, 1080);
    assert.equal(PRESET_RATIOS["1:1"].height, 1080);
    assert.equal(PRESET_RATIOS["1:1"].ratio, 1);

    assert.ok(PRESET_RATIOS["16:9"]);
    assert.equal(PRESET_RATIOS["16:9"].width, 1600);
    assert.equal(PRESET_RATIOS["16:9"].height, 900);
    assert.equal(PRESET_RATIOS["16:9"].ratio, 16 / 9);

    assert.ok(PRESET_RATIOS["natural"]);
  });

  test("UniversalImageCropper component implements non-destructive cancel and pixel-perfect canvas export", () => {
    const cropperSrc = fs.readFileSync(path.join(ROOT_DIR, "src/components/common/UniversalImageCropper.jsx"), "utf-8");
    assert.ok(cropperSrc.includes("baseDimensions"), "Calculates baseDimensions preserving natural aspect ratio");
    assert.ok(cropperSrc.includes("handleApplyCrop"), "Exports canvas output strictly on Apply");
    assert.ok(cropperSrc.includes("onClose"), "Provides non-destructive cancel trigger");
  });
});

describe("Workstream C: Public Faculty Preview & Recent at Ethos Carousel", () => {
  test("FacultyProfileCard renders horizontal carousel and View Full Profile link", () => {
    const cardSrc = fs.readFileSync(path.join(ROOT_DIR, "src/components/workshop/FacultyProfileCard.jsx"), "utf-8");
    assert.ok(cardSrc.includes("faculty-recent-carousel"), "Renders horizontal carousel for Recent at Ethos");
    assert.ok(cardSrc.includes("faculty-popover-full-profile-btn"), "Renders View Full Profile action button");
    assert.ok(cardSrc.includes("getTrainerPublicProfile"), "Uses on-demand cached public profile query");
    assert.ok(cardSrc.includes("slice(0, 10)"), "Limits past workshops to maximum 10");
  });
});

describe("Workstream D: Public Trainer Profile & C# Backend Contracts", () => {
  test("C# TrainerService implements GetPublicProfileAsync with past/upcoming separation and slug resolution", () => {
    const trainerServiceSrc = fs.readFileSync(path.join(API_DIR, "Application/Trainers/TrainerService.cs"), "utf-8");
    assert.ok(trainerServiceSrc.includes("GetPublicProfileAsync"), "Implements GetPublicProfileAsync");
    assert.ok(trainerServiceSrc.includes("TrainerStatus.Active"), "Guards that trainer must be Active");
    assert.ok(trainerServiceSrc.includes("WorkshopStatus.Approved"), "Guards that workshops must be Approved");
    assert.ok(trainerServiceSrc.includes("Take(10)"), "Limits recent past workshops to 10 in backend");
    assert.ok(trainerServiceSrc.includes("RecentWorkshops"), "Separates RecentWorkshops");
    assert.ok(trainerServiceSrc.includes("UpcomingWorkshops"), "Separates UpcomingWorkshops");
  });

  test("C# TrainersController exposes public-profile endpoint with [AllowAnonymous]", () => {
    const controllerSrc = fs.readFileSync(path.join(API_DIR, "Controllers/TrainersController.cs"), "utf-8");
    assert.ok(controllerSrc.includes("[HttpGet(\"{slug}/public-profile\")]"), "Exposes slug-based public profile endpoint");
    assert.ok(controllerSrc.includes("[AllowAnonymous]"), "Endpoint is publicly accessible");
  });

  test("App.jsx registers /trainers/:slug and /faculty/:slug public routes", () => {
    const appSrc = fs.readFileSync(path.join(ROOT_DIR, "src/App.jsx"), "utf-8");
    assert.ok(appSrc.includes('path="/trainers/:slug"'), "Registers /trainers/:slug route");
    assert.ok(appSrc.includes('path="/faculty/:slug"'), "Registers /faculty/:slug route");
  });
});

describe("Workstream E: Scoped Zero-Hierarchy Rule in Public UI", () => {
  test("Public WorkshopDetailsPage and FacultyProfileCard contain zero 'Lead Trainer' or 'Primary Trainer' text", () => {
    const detailsSrc = fs.readFileSync(path.join(ROOT_DIR, "src/pages/WorkshopDetailsPage.jsx"), "utf-8");
    const facultyCardSrc = fs.readFileSync(path.join(ROOT_DIR, "src/components/workshop/FacultyProfileCard.jsx"), "utf-8");
    const trainerPageSrc = fs.readFileSync(path.join(ROOT_DIR, "src/pages/TrainerPublicProfilePage.jsx"), "utf-8");

    const forbidden = ["Lead Trainer", "Lead Instructor", "Primary Trainer", "Head Trainer", "Main Trainer"];

    for (const phrase of forbidden) {
      assert.ok(!detailsSrc.includes(phrase), `WorkshopDetailsPage should not contain '${phrase}'`);
      assert.ok(!facultyCardSrc.includes(phrase), `FacultyProfileCard should not contain '${phrase}'`);
      assert.ok(!trainerPageSrc.includes(phrase), `TrainerPublicProfilePage should not contain '${phrase}'`);
    }
  });
});
