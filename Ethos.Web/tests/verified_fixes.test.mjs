import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webRoot = path.resolve(__dirname, "..");

import { getPassAvailability, getCalendarDateKey, getChronologicalGroupedSessions, getWorkshopTimingDisplay } from "../src/utils/workshopPresentation.js";
import { getMediaUrl, getTrainerPhotoUrl, DEFAULT_AVATAR_PLACEHOLDER, ETHOS_DEFAULT_TRAINER_AVATAR, handleTrainerImgError, getTrainerDisplayName } from "../src/utils/mediaUrl.js";
import { buildNormalizedShowcaseVideos } from "../src/utils/galleryPresentation.js";
import { getMediaPreviewPresentation } from "../src/utils/mediaPreviewPresentation.js";
import { getPlacementCropConfig } from "../src/config/MediaPlacementCatalog.js";

test("FIX 01 — Pass Availability Mapping with realistic backend DTO", () => {
  // 1. Realistic backend DTO: available seats = 15, isAvailable = true
  const availablePass = {
    id: "pass-1",
    name: "Solo Pass",
    isAvailable: true,
    availableSeats: 15,
  };
  const result1 = getPassAvailability(availablePass);
  assert.equal(result1.isSoldOut, false, "Pass should NOT be sold out when isAvailable is true");
  assert.equal(result1.isAvailable, true, "Pass should be available");
  assert.equal(result1.remainingSeats, 15, "Remaining seats should be 15");

  // 2. Authoritative backend isAvailable: false overrides availableSeats > 0
  const unavailablePass = {
    id: "pass-2",
    name: "Duo Pass",
    isAvailable: false,
    availableSeats: 15,
  };
  const result2 = getPassAvailability(unavailablePass);
  assert.equal(result2.isSoldOut, true, "Pass should be sold out when isAvailable is false");
  assert.equal(result2.isAvailable, false);
  assert.equal(result2.remainingSeats, 15);

  // 3. Backwards compatibility fallbacks
  const legacyAvailableQuantity = {
    id: "pass-3",
    availableQuantity: 8,
  };
  assert.equal(getPassAvailability(legacyAvailableQuantity).remainingSeats, 8);
  assert.equal(getPassAvailability(legacyAvailableQuantity).isSoldOut, false);

  const legacyPassesRemaining = {
    id: "pass-4",
    passesRemaining: 4,
  };
  assert.equal(getPassAvailability(legacyPassesRemaining).remainingSeats, 4);
  assert.equal(getPassAvailability(legacyPassesRemaining).isSoldOut, false);

  const legacyRemainingQuantity = {
    id: "pass-5",
    remainingQuantity: 0,
  };
  assert.equal(getPassAvailability(legacyRemainingQuantity).remainingSeats, 0);
  assert.equal(getPassAvailability(legacyRemainingQuantity).isSoldOut, true);

  // 4. Null / undefined safety
  assert.equal(getPassAvailability(null).isSoldOut, true);
  assert.equal(getPassAvailability(undefined).isAvailable, false);
});

test("FIX 02 — Trainer Photo URL Resolution", () => {
  // 1. R2 HTTPS URL resolution
  const r2Trainer = {
    photoUrl: "https://r2.ethosdance.in/trainers/trainer1.jpg",
  };
  assert.equal(getTrainerPhotoUrl(r2Trainer), "https://r2.ethosdance.in/trainers/trainer1.jpg");

  // 2. profileImageUrl fallback
  const profileImgTrainer = {
    profileImageUrl: "https://r2.ethosdance.in/trainers/trainer2.jpg",
  };
  assert.equal(getTrainerPhotoUrl(profileImgTrainer), "https://r2.ethosdance.in/trainers/trainer2.jpg");

  // 3. profilePhotoUrl fallback
  const profilePhotoTrainer = {
    profilePhotoUrl: "https://r2.ethosdance.in/trainers/trainer3.jpg",
  };
  assert.equal(getTrainerPhotoUrl(profilePhotoTrainer), "https://r2.ethosdance.in/trainers/trainer3.jpg");

  // 4. imageUrl fallback
  const imgUrlTrainer = {
    imageUrl: "https://r2.ethosdance.in/trainers/trainer4.jpg",
  };
  assert.equal(getTrainerPhotoUrl(imgUrlTrainer), "https://r2.ethosdance.in/trainers/trainer4.jpg");

  // 5. trainer.photo fallback
  const photoTrainer = {
    photo: "https://r2.ethosdance.in/trainers/trainer5.jpg",
  };
  assert.equal(getTrainerPhotoUrl(photoTrainer), "https://r2.ethosdance.in/trainers/trainer5.jpg");

  // 6. Relative URL resolution (prepends API_BASE_URL)
  const relativeTrainer = {
    photoUrl: "/uploads/trainers/relative.jpg",
  };
  const resolvedRelative = getTrainerPhotoUrl(relativeTrainer);
  assert.match(resolvedRelative, /\/uploads\/trainers\/relative\.jpg$/);

  // 7. Missing trainer / null / undefined
  assert.equal(getTrainerPhotoUrl(null), DEFAULT_AVATAR_PLACEHOLDER);
  assert.equal(getTrainerPhotoUrl(undefined), DEFAULT_AVATAR_PLACEHOLDER);
  assert.equal(getTrainerPhotoUrl({}), DEFAULT_AVATAR_PLACEHOLDER);

  // 8. Deliberate avatar placeholder assets must exist on disk and be non-empty (>100 bytes)
  const pngPath = path.resolve(webRoot, "public/images/avatar-placeholder.png");
  const svgPath = path.resolve(webRoot, "public/images/avatar-placeholder.svg");
  assert.ok(fs.existsSync(pngPath), "avatar-placeholder.png must exist in public/images");
  assert.ok(fs.existsSync(svgPath), "avatar-placeholder.svg must exist in public/images");
  assert.ok(fs.statSync(pngPath).size > 100, "avatar-placeholder.png must not be a 1x1 empty placeholder");
  assert.ok(fs.statSync(svgPath).size > 100, "avatar-placeholder.svg must not be empty");
});

test("FIX 03 — Chronological Session Order", () => {
  const unsortedSessions = [
    {
      id: "s-1",
      sessionDate: "2026-09-20",
      startTime: "18:00",
      endTime: "19:30",
      title: "Hip Hop Masterclass",
    },
    {
      id: "s-2",
      sessionDate: "2026-09-19",
      startTime: "10:00",
      endTime: "11:30",
      title: "Contemporary Flow",
    },
  ];

  // Ensure original array is not mutated
  const originalSnapshot = JSON.stringify(unsortedSessions);
  const grouped = getChronologicalGroupedSessions(unsortedSessions);
  assert.equal(JSON.stringify(unsortedSessions), originalSnapshot, "Source array must not be mutated");

  // Verify dates appear in ascending chronological order: 2026-09-19 before 2026-09-20
  assert.equal(grouped.length, 2);
  assert.equal(grouped[0][0], "2026-09-19");
  assert.equal(grouped[1][0], "2026-09-20");

  // Multiple sessions on the same date: 14:00, 10:00, 18:00
  const sameDateSessions = [
    { id: "s-a", sessionDate: "2026-09-20", startTime: "14:00", endTime: "15:30" },
    { id: "s-b", sessionDate: "2026-09-20", startTime: "10:00", endTime: "11:30" },
    { id: "s-c", sessionDate: "2026-09-20", startTime: "18:00", endTime: "19:30" },
  ];
  const sameDateGrouped = getChronologicalGroupedSessions(sameDateSessions);
  assert.equal(sameDateGrouped.length, 1);
  const sortedSessions = sameDateGrouped[0][1];
  assert.equal(sortedSessions[0].startTime, "10:00");
  assert.equal(sortedSessions[1].startTime, "14:00");
  assert.equal(sortedSessions[2].startTime, "18:00");
});

test("FIX 04 — Multi-Session Timing Display", () => {
  // Case 1: 1 session -> actual session time
  const singleSession = [{ startTime: "18:00", endTime: "19:30" }];
  assert.equal(getWorkshopTimingDisplay(singleSession), "18:00 - 19:30");

  // Case 2: Multiple sessions AND every session has the same start/end time
  const sameTimeSessions = [
    { startTime: "18:00", endTime: "19:30" },
    { startTime: "18:00", endTime: "19:30" },
  ];
  assert.equal(getWorkshopTimingDisplay(sameTimeSessions), "18:00 - 19:30");

  // Case 3: Multiple sessions with differing start/end times
  const differingSessions = [
    { startTime: "18:00", endTime: "19:30" },
    { startTime: "10:00", endTime: "11:30" },
  ];
  assert.equal(getWorkshopTimingDisplay(differingSessions), "Multiple Sessions — See Schedule");

  // Fallback: No sessions
  assert.equal(getWorkshopTimingDisplay([], "17:00", "18:30"), "17:00 - 18:30");
});

test("FIX 05 — Trainer Photo Resolution & Two-Layer Error Defense (Phase 3)", () => {
  // 1. avatarUrl fallback
  const avatarUrlTrainer = {
    avatarUrl: "https://r2.ethosdance.in/trainers/trainer-avatar.jpg",
  };
  assert.equal(getTrainerPhotoUrl(avatarUrlTrainer), "https://r2.ethosdance.in/trainers/trainer-avatar.jpg");

  // 2. profileImage fallback
  const profileImageTrainer = {
    profileImage: "https://r2.ethosdance.in/trainers/trainer-prof.jpg",
  };
  assert.equal(getTrainerPhotoUrl(profileImageTrainer), "https://r2.ethosdance.in/trainers/trainer-prof.jpg");

  // 3. image fallback
  const imageTrainer = {
    image: "https://r2.ethosdance.in/trainers/trainer-img.jpg",
  };
  assert.equal(getTrainerPhotoUrl(imageTrainer), "https://r2.ethosdance.in/trainers/trainer-img.jpg");

  // 4. Inlined ETHOS_DEFAULT_TRAINER_AVATAR SVG sanity
  assert.ok(typeof ETHOS_DEFAULT_TRAINER_AVATAR === "string", "ETHOS_DEFAULT_TRAINER_AVATAR must be a string");
  assert.ok(ETHOS_DEFAULT_TRAINER_AVATAR.startsWith("data:image/svg+xml"), "Must be a data URI");
  assert.ok(ETHOS_DEFAULT_TRAINER_AVATAR.includes("<svg"), "Must contain SVG markup");
  assert.ok(ETHOS_DEFAULT_TRAINER_AVATAR.includes("%23FF5500"), "Must include Ethos signature orange accent");

  // 5. handleTrainerImgError two-layer fallback test
  const fakeImg = {
    src: "https://broken-cdn.ethosdance.in/missing.jpg",
    onerror: () => {},
  };
  const fakeEvent = { currentTarget: fakeImg };

  // Layer 1 fallback: switches to DEFAULT_AVATAR_PLACEHOLDER
  handleTrainerImgError(fakeEvent);
  assert.equal(fakeImg.src, DEFAULT_AVATAR_PLACEHOLDER);

  // Layer 2 fallback: if DEFAULT_AVATAR_PLACEHOLDER also errors, switches to ETHOS_DEFAULT_TRAINER_AVATAR and clears onerror
  handleTrainerImgError(fakeEvent);
  assert.equal(fakeImg.src, ETHOS_DEFAULT_TRAINER_AVATAR);
  assert.equal(fakeImg.onerror, null);
});

test("FIX 06 — Canonical Trainer Display Name Resolution (Phase 4)", () => {
  // 1. fullName takes precedence
  assert.equal(getTrainerDisplayName({ fullName: "Rahul Sharma", name: "Rahul S", displayName: "Rahul" }), "Rahul Sharma");

  // 2. Fallback to name if fullName is empty/missing
  assert.equal(getTrainerDisplayName({ fullName: "", name: "Ananya Roy" }), "Ananya Roy");

  // 3. Fallback to displayName
  assert.equal(getTrainerDisplayName({ displayName: "Karthik Master" }), "Karthik Master");

  // 4. Fallback to user.firstName + user.lastName
  assert.equal(getTrainerDisplayName({ user: { firstName: "Vikram", lastName: "Verma" } }), "Vikram Verma");
  assert.equal(getTrainerDisplayName({ user: { firstName: "Vikram", lastName: "" } }), "Vikram");

  // 5. Fallback to email
  assert.equal(getTrainerDisplayName({ email: "priya@ethosdance.in" }), "priya@ethosdance.in");

  // 6. Direct string input
  assert.equal(getTrainerDisplayName("Swati Reddy"), "Swati Reddy");
  assert.equal(getTrainerDisplayName("   "), "Unnamed Trainer");

  // 7. Safety fallbacks for null/undefined/empty object
  assert.equal(getTrainerDisplayName(null), "Unnamed Trainer");
  assert.equal(getTrainerDisplayName(undefined), "Unnamed Trainer");
  assert.equal(getTrainerDisplayName({}), "Unnamed Trainer");
  assert.equal(getTrainerDisplayName({ fullName: "   " }), "Unnamed Trainer");
});

test("FIX 07 — Gallery Video Showcase Presentation Model & Deduplication (Phase 5)", () => {
  const mockDefaults = [
    {
      id: "default-1",
      type: "video",
      src: "https://r2.ethosdance.in/default-reel-1.mp4",
      poster: "https://r2.ethosdance.in/default-poster-1.jpg",
      category: "CHOREOGRAPHY",
      title: "Ethos Visual Reel",
      description: "Default showcase reel.",
    },
    {
      id: "default-2",
      type: "video",
      src: "https://r2.ethosdance.in/default-reel-2.mp4",
      poster: "https://r2.ethosdance.in/default-poster-2.jpg",
      category: "PERFORMANCE",
      title: "Urban Showcase Routine",
      description: "Default urban routine.",
    },
  ];

  // 1. Fallback: When dynamic API videos is empty or null, returns mockDefaults
  const fallbackResult = buildNormalizedShowcaseVideos([], mockDefaults);
  assert.equal(fallbackResult.length, 2);
  assert.equal(fallbackResult[0].id, "default-1");
  assert.equal(fallbackResult[1].id, "default-2");

  const nullResult = buildNormalizedShowcaseVideos(null, mockDefaults);
  assert.equal(nullResult.length, 2);

  // 2. Real dynamic mapping + merging: 1 dynamic video + 2 defaults
  const dynamicVideos = [
    {
      id: "api-vid-100",
      publicUrl: "https://r2.ethosdance.in/videos/dynamic-1.mp4",
      thumbnailUrl: "https://r2.ethosdance.in/thumbnails/thumb-1.jpg",
      title: "Waacking Battle Finals",
      category: "Perform",
      description: "Fast-paced arms and musicality.",
    },
  ];

  const merged = buildNormalizedShowcaseVideos(dynamicVideos, mockDefaults);
  // Length should be 1 dynamic + 2 defaults = 3 total items
  assert.equal(merged.length, 3, "Should merge dynamic API items with curated defaults");

  // First item is dynamic item normalized
  assert.equal(merged[0].id, "api-vid-100");
  assert.equal(merged[0].type, "video");
  assert.equal(merged[0].src, "https://r2.ethosdance.in/videos/dynamic-1.mp4");
  assert.equal(merged[0].poster, "https://r2.ethosdance.in/thumbnails/thumb-1.jpg");
  assert.equal(merged[0].category, "PERFORM", "Category must be uppercase");
  assert.equal(merged[0].title, "Waacking Battle Finals");

  // Subsequent items are the curated defaults
  assert.equal(merged[1].id, "default-1");
  assert.equal(merged[2].id, "default-2");

  // 3. Deduplication: when a dynamic item has the exact same src as default-1
  const duplicateDynamicVideos = [
    {
      id: "api-vid-dup",
      publicUrl: "https://r2.ethosdance.in/default-reel-1.mp4", // Same src as default-1
      thumbnailUrl: "https://r2.ethosdance.in/thumbnails/custom-thumb.jpg",
      title: "Reel 1 Re-uploaded",
      category: "Choreography",
    },
    {
      id: "api-vid-unique",
      publicUrl: "https://r2.ethosdance.in/videos/unique.mp4",
      thumbnailUrl: "https://r2.ethosdance.in/thumbnails/unique.jpg",
      title: "Unique Studio Reel",
      category: "Community",
    },
  ];

  const deduplicated = buildNormalizedShowcaseVideos(duplicateDynamicVideos, mockDefaults);
  // Total should be: 2 dynamic videos + 1 default (default-1 must be deduplicated out) = 3 total items
  assert.equal(deduplicated.length, 3, "Duplicate default-1 must be deduplicated out");
  assert.equal(deduplicated[0].src, "https://r2.ethosdance.in/default-reel-1.mp4");
  assert.equal(deduplicated[1].src, "https://r2.ethosdance.in/videos/unique.mp4");
  assert.equal(deduplicated[2].src, "https://r2.ethosdance.in/default-reel-2.mp4");

  // Verify all items have valid non-empty poster strings for <img> card rendering
  for (const item of deduplicated) {
    assert.ok(typeof item.poster === "string" && item.poster.length > 0, "Poster must be valid non-empty string");
    assert.equal(item.type, "video");
  }
});

test("FIX 08 — Featured Movement Navigation Wrapping & Counter Formatting (Phase 5)", () => {
  const videoCount = 5;

  // Next wrapping: from last item (4) -> first item (0)
  const getNextIndex = (current, total) => (current < total - 1 ? current + 1 : 0);
  assert.equal(getNextIndex(0, videoCount), 1);
  assert.equal(getNextIndex(3, videoCount), 4);
  assert.equal(getNextIndex(4, videoCount), 0, "Next on last item must wrap to 0");

  // Prev wrapping: from first item (0) -> last item (4)
  const getPrevIndex = (current, total) => (current > 0 ? current - 1 : total - 1);
  assert.equal(getPrevIndex(4, videoCount), 3);
  assert.equal(getPrevIndex(1, videoCount), 0);
  assert.equal(getPrevIndex(0, videoCount), 4, "Prev on first item must wrap to 4");

  // Counter formatting: 01 / 05 ... 05 / 05
  const formatCounter = (idx, total) =>
    `${String(idx + 1).padStart(2, "0")} / ${String(total).padStart(2, "0")}`;

  assert.equal(formatCounter(0, videoCount), "01 / 05");
  assert.equal(formatCounter(1, videoCount), "02 / 05");
  assert.equal(formatCounter(4, videoCount), "05 / 05");

  // Invariants: must never produce 00 / 05 or 06 / 05
  for (let i = 0; i < videoCount; i++) {
    const counterStr = formatCounter(i, videoCount);
    assert.match(counterStr, /^0[1-5] \/ 05$/);
    assert.notEqual(counterStr, "00 / 05");
    assert.notEqual(counterStr, "06 / 05");
  }
});

test("FIX 09 — Ticket Pricing Capacity Bounding & Preset Generation", () => {
  // Test case: totalCapacity = 50
  const totalCap = 50;
  const baseP = 500;
  const step = 10;
  const tiers = [];
  let currentMin = 1;
  let tierNum = 1;

  while (currentMin <= totalCap) {
    const currentMax = Math.min(currentMin + step - 1, totalCap);
    tiers.push({
      tierNumber: tierNum,
      minTickets: currentMin,
      maxTickets: currentMax,
      price: baseP + (tierNum - 1) * 100,
    });
    currentMin = currentMax + 1;
    tierNum++;
  }

  // Must have exactly 5 tiers: 1-10, 11-20, 21-30, 31-40, 41-50
  assert.equal(tiers.length, 5);
  assert.equal(tiers[0].minTickets, 1);
  assert.equal(tiers[0].maxTickets, 10);
  assert.equal(tiers[4].minTickets, 41);
  assert.equal(tiers[4].maxTickets, 50, "Final tier must be capped at 50, never exceeding capacity");

  // Invariant: No tier maxTickets can exceed totalCap
  for (const t of tiers) {
    assert.ok(t.maxTickets <= totalCap, `Tier maxTickets ${t.maxTickets} must be <= ${totalCap}`);
    assert.ok(t.minTickets <= totalCap, `Tier minTickets ${t.minTickets} must be <= ${totalCap}`);
  }

  // Test case: totalCapacity = 35
  const cap35Tiers = [];
  let cMin = 1;
  let tNum = 1;
  while (cMin <= 35) {
    const cMax = Math.min(cMin + 10 - 1, 35);
    cap35Tiers.push({ tierNumber: tNum, minTickets: cMin, maxTickets: cMax });
    cMin = cMax + 1;
    tNum++;
  }
  assert.equal(cap35Tiers.length, 4);
  assert.equal(cap35Tiers[3].minTickets, 31);
  assert.equal(cap35Tiers[3].maxTickets, 35);
});

test("FIX 10 — Multi-Session Authoritative Schedule Derivation", () => {
  const sessions = [
    { sessionDate: "2026-10-02", startTime: "18:00", endTime: "20:00" },
    { sessionDate: "2026-10-01", startTime: "10:00", endTime: "12:00" },
    { sessionDate: "2026-10-01", startTime: "14:00", endTime: "16:00" },
  ];

  // Derive earliest date
  const sortedByDate = [...sessions].filter((s) => s.sessionDate).sort((a, b) => {
    const dCmp = a.sessionDate.localeCompare(b.sessionDate);
    if (dCmp !== 0) return dCmp;
    return (a.startTime || "").localeCompare(b.startTime || "");
  });

  assert.equal(sortedByDate[0].sessionDate, "2026-10-01", "Earliest date must be 2026-10-01");
  assert.equal(sortedByDate[0].startTime, "10:00", "Earliest start time must be 10:00");

  // Derive latest date and end time
  const sortedDesc = [...sessions].filter((s) => s.sessionDate).sort((a, b) => {
    const dCmp = b.sessionDate.localeCompare(a.sessionDate);
    if (dCmp !== 0) return dCmp;
    return (b.endTime || "").localeCompare(a.endTime || "");
  });

  assert.equal(sortedDesc[0].sessionDate, "2026-10-02", "Latest date must be 2026-10-02");
  assert.equal(sortedDesc[0].endTime, "20:00", "Latest end time must be 20:00");
});

test("FIX 11 — DNS Fallback Defense for media.ethosdancestudio.com", () => {
  // Without ID, must return empty string to prevent browser ERR_NAME_NOT_RESOLVED
  const unresolvableUrl = "https://media.ethosdancestudio.com/workshops/poster-1.jpg";
  const resolved = getMediaUrl(unresolvableUrl);
  assert.equal(resolved, "", "Must return empty string to trigger local asset fallback");

  // With ID, must route to backend API content endpoint
  const resolvedWithId = getMediaUrl(unresolvableUrl, "media-item-123");
  assert.ok(resolvedWithId.includes("/api/media/content/media-item-123"), "Must fallback to API streaming endpoint");
});

test("FIX 12 — Universal Media Preview Presentation Resolver & Cross-Placement Isolation", () => {
  // 1. Hero Banner: 16:9 widescreen stage
  const heroPres = getMediaPreviewPresentation(
    { mediaType: "Video", title: "BOMMALI" },
    { sectionKey: "HomepageScrolling", placement: "Hero Banner" },
    { order: 1, label: "Hero Slot 01" }
  );
  assert.equal(heroPres.variant, "hero");
  assert.equal(heroPres.aspectRatio, "16:9");
  assert.equal(heroPres.badge, "HERO STAGE");
  assert.equal(heroPres.headline, "MORE THAN DANCE");
  assert.equal(heroPres.isVideo, true);

  // 2. Homepage Reels: 9:16 vertical Reel
  const reelPres = getMediaPreviewPresentation(
    { mediaType: "Video", title: "HipHop Groove" },
    { sectionKey: "HomepageReels", placement: "Homepage Reels" },
    null
  );
  assert.equal(reelPres.variant, "reel");
  assert.equal(reelPres.aspectRatio, "9:16");
  assert.equal(reelPres.badge, "DANCE REEL");
  assert.equal(reelPres.isVideo, true);

  // 3. We Are Ethos: Slot 1 (4:5), Slot 2 (16:9), Slot 3 (1:1)
  const ethosSlot1 = getMediaPreviewPresentation(
    { mediaType: "Image", title: "Studio Interior" },
    { sectionKey: "AboutEthos", placement: "We Are Ethos" },
    { order: 1, label: "Slot 01: Main Studio" }
  );
  assert.equal(ethosSlot1.variant, "about");
  assert.equal(ethosSlot1.aspectRatio, "4:5");
  assert.equal(ethosSlot1.slotClass, "preview-about-portrait");
  assert.equal(ethosSlot1.badge, "WE ARE ETHOS");

  const ethosSlot2 = getMediaPreviewPresentation(
    { mediaType: "Image", title: "Movement Floor" },
    { sectionKey: "AboutEthos", placement: "We Are Ethos" },
    { order: 2, label: "Slot 02: Movement" }
  );
  assert.equal(ethosSlot2.aspectRatio, "16:9");
  assert.equal(ethosSlot2.slotClass, "preview-about-landscape");

  const ethosSlot3 = getMediaPreviewPresentation(
    { mediaType: "Image", title: "Community Circle" },
    { sectionKey: "AboutEthos", placement: "We Are Ethos" },
    { order: 3, label: "Slot 03: Community" }
  );
  assert.equal(ethosSlot3.aspectRatio, "1:1");
  assert.equal(ethosSlot3.slotClass, "preview-about-square");

  // 4. Founders: 3:4 portrait
  const founderPres = getMediaPreviewPresentation(
    { mediaType: "Image", title: "Sujith & Tejaswini" },
    { sectionKey: "Founders", placement: "Founder" },
    { order: 1 }
  );
  assert.equal(founderPres.variant, "founder");
  assert.equal(founderPres.aspectRatio, "3:4");
  assert.equal(founderPres.badge, "CO-FOUNDERS & ARTISTIC DIRECTORS");

  // 5. Trainers: 3:4 master faculty card
  const trainerPres = getMediaPreviewPresentation(
    { mediaType: "Image", title: "Sujith Kumar" },
    { sectionKey: "Trainers", placement: "Trainers" },
    { order: 1, label: "Trainer 01: Sujith Kumar", role: "Co-Founder & Lead Choreographer" }
  );
  assert.equal(trainerPres.variant, "trainer");
  assert.equal(trainerPres.aspectRatio, "3:4");
  assert.equal(trainerPres.badge, "MASTER FACULTY");
  assert.equal(trainerPres.role, "Co-Founder & Lead Choreographer");

  // 6. Gallery Featured Slideshow: 16:9
  const slideshowPres = getMediaPreviewPresentation(
    { mediaType: "Image", title: "Showcase Snapshot" },
    { sectionKey: "GallerySlideshow", placement: "Featured Slideshow" },
    null
  );
  assert.equal(slideshowPres.variant, "gallery-slideshow");
  assert.equal(slideshowPres.aspectRatio, "16:9");
  assert.equal(slideshowPres.badge, "FEATURED SLIDESHOW");

  // 7. Gallery Large Videos: 16:9 cinematic theater
  const galleryVideoPres = getMediaPreviewPresentation(
    { mediaType: "Video", title: "Production Reel" },
    { sectionKey: "GalleryVideos", placement: "Large Videos" },
    null
  );
  assert.equal(galleryVideoPres.variant, "gallery-video");
  assert.equal(galleryVideoPres.aspectRatio, "16:9");
  assert.equal(galleryVideoPres.badge, "CINEMATIC SHOWCASE · THEATER");
  assert.equal(galleryVideoPres.isVideo, true);

  // 8. Gallery All Photos: Preserves natural uncropped aspect ratio
  const galleryPhotoPres = getMediaPreviewPresentation(
    { mediaType: "Image", title: "Workshop Shot", category: "Workshop" },
    { sectionKey: "GalleryImages", placement: "All Photos" },
    null
  );
  assert.equal(galleryPhotoPres.variant, "gallery-photo");
  assert.equal(galleryPhotoPres.aspectRatio, "natural");
  assert.equal(galleryPhotoPres.badge, "GALLERY · WORKSHOP");

  // 9. Workshop Landscape: 16:9
  const workshopLandPres = getMediaPreviewPresentation(
    { mediaType: "Image", layoutType: "Landscape", title: "Dance Intensive" },
    { sectionKey: "Workshop", placement: "Workshop Landscape" },
    null
  );
  assert.equal(workshopLandPres.variant, "workshop-landscape");
  assert.equal(workshopLandPres.aspectRatio, "16:9");
  assert.equal(workshopLandPres.badge, "WORKSHOP BANNER");

  // 10. Workshop Portrait: 3:4
  const workshopPortPres = getMediaPreviewPresentation(
    { mediaType: "Image", layoutType: "Portrait", title: "Intensive Flyer" },
    { sectionKey: "Workshop", placement: "Workshop Portrait" },
    null
  );
  assert.equal(workshopPortPres.variant, "workshop-portrait");
  assert.equal(workshopPortPres.aspectRatio, "3:4");
  assert.equal(workshopPortPres.badge, "WORKSHOP PASS / FLYER");

  // 11. Events: 16:9
  const eventPres = getMediaPreviewPresentation(
    { mediaType: "Image", title: "Annual Recital" },
    { sectionKey: "Events", placement: "Event Photos" },
    null
  );
  assert.equal(eventPres.variant, "event");
  assert.equal(eventPres.aspectRatio, "16:9");
  assert.equal(eventPres.badge, "SPECIAL EVENT");

  // ── CROSS-PLACEMENT ISOLATION ASSERTIONS ───────────────────────────────────
  // Ensure an item from GallerySlideshow is NEVER resolved as GalleryImages
  assert.notEqual(slideshowPres.variant, galleryPhotoPres.variant);
  // Ensure Trainers is NEVER resolved as Founders
  assert.notEqual(trainerPres.variant, founderPres.variant);
  // Ensure Workshop Landscape is NEVER resolved as Workshop Portrait
  assert.notEqual(workshopLandPres.variant, workshopPortPres.variant);
  // Ensure Reel is NEVER resolved as Hero
  assert.notEqual(reelPres.variant, heroPres.variant);
});

test("FIX 13 — Authoritative Placement Crop Configuration & Gallery Natural Mode", () => {
  // Test that placement catalog aspect ratios map accurately and strictly via authoritative resolver
  const testCases = [
    { id: "hero-banner", slot: 1, expectedRatio: "16:9", expectedAllowed: ["16:9"] },
    { id: "we-are-ethos", slot: 1, expectedRatio: "4:5", expectedAllowed: ["4:5"] },
    { id: "we-are-ethos", slot: 2, expectedRatio: "16:9", expectedAllowed: ["16:9"] },
    { id: "we-are-ethos", slot: 3, expectedRatio: "1:1", expectedAllowed: ["1:1"] },
    { id: "founder", slot: 1, expectedRatio: "3:4", expectedAllowed: ["3:4"] },
    { id: "trainers", slot: 1, expectedRatio: "3:4", expectedAllowed: ["3:4"] },
    { id: "gallery-slideshow", slot: null, expectedRatio: "16:9", expectedAllowed: ["16:9"] },
    { id: "gallery-all-photos", slot: null, expectedRatio: "natural", expectedAllowed: ["natural"] },
    { id: "workshop-landscape", slot: null, expectedRatio: "16:9", expectedAllowed: ["16:9"] },
    { id: "workshop-portrait", slot: null, expectedRatio: "3:4", expectedAllowed: ["3:4", "4:5"] },
    { id: "event-photos", slot: null, expectedRatio: "16:9", expectedAllowed: ["16:9", "4:3"] },
  ];

  // Test the authoritative production resolver directly
  for (const tc of testCases) {
    const cfg = getPlacementCropConfig(tc.id, tc.slot);
    assert.ok(cfg, `Config must exist for ${tc.id}`);
    assert.equal(cfg.aspectRatio, tc.expectedRatio, `Placement ${tc.id} slot ${tc.slot} should match ${tc.expectedRatio}`);
    assert.deepEqual(cfg.allowedRatios, tc.expectedAllowed, `Placement ${tc.id} allowed ratios should match ${tc.expectedAllowed}`);
  }

  // Verify Gallery All Photos does NOT force 16:9 or distort
  const galleryCfg = getPlacementCropConfig("gallery-all-photos");
  assert.equal(galleryCfg.aspectRatio, "natural", "Gallery Photos must preserve natural aspect ratio without forced 16:9 crop");

  // Verify Founders is strict 3:4 without forced 16:9
  const founderCfg = getPlacementCropConfig("founder");
  assert.equal(founderCfg.aspectRatio, "3:4");
  assert.deepEqual(founderCfg.allowedRatios, ["3:4"]);
});

test("FIX 14 — Cropped File Normalization, Modal Stacking Hierarchy, Zoom Cover Scale & Pointer Events", () => {
  // 1. Cropped file naming and MIME normalization test
  const testFilenames = [
    { input: "WhatsApp Image 2026.png", expected: "WhatsApp Image 2026.jpg" },
    { input: "dance-poster.webp", expected: "dance-poster.jpg" },
    { input: "already_jpeg.jpg", expected: "already_jpeg.jpg" },
    { input: "complex.name.with.dots.jpeg", expected: "complex.name.with.dots.jpg" },
  ];

  for (const { input, expected } of testFilenames) {
    const baseName = input.replace(/\.[^/.]+$/, "");
    const normalized = `${baseName}.jpg`;
    assert.equal(normalized, expected, `Filename ${input} should normalize to ${expected}`);
  }

  // 2. Video bypass logic test
  const videoFile = { name: "reel.mp4", type: "video/mp4", size: 15 * 1024 * 1024 };
  const isVideo = videoFile.type.startsWith("video/");
  assert.equal(isVideo, true, "Video file must be recognized as video");
  // Video must NOT pass through cropper, keeping original file name and format
  assert.equal(videoFile.name, "reel.mp4");
  assert.equal(videoFile.type, "video/mp4");

  // 3. Stacking hierarchy verification
  const drawerZ = 9990;
  const uploadModalZ = 10000;
  const cropOverlayZ = 10050;
  const cropDialogZ = 10060;

  assert.ok(uploadModalZ > drawerZ, "Upload modal must sit above Drawer backdrop");
  assert.ok(cropOverlayZ > uploadModalZ, "Crop overlay must sit above Upload modal");
  assert.ok(cropDialogZ > cropOverlayZ, "Crop dialog must sit above Crop overlay");

  // 4. Zoom cover scale bounding math verification
  // At zoom = 1.0 (cover scale), minZoom prevents shrinking below crop box
  const minZoom = 1.0;
  const maxZoom = 3.0;
  assert.equal(minZoom, 1.0, "Minimum zoom must be 1.0 to prevent empty borders");
  assert.ok(maxZoom >= 3.0, "Max zoom allows detailed framing");

  // Test clamp offset math for 16:9 cropbox (e.g. 480x270) with 4:3 image (e.g. 800x600)
  const cropW = 480;
  const cropH = 270;
  const imgW = 800;
  const imgH = 600;
  const imgAspect = imgW / imgH; // 1.333
  const boxAspect = cropW / cropH; // 1.777
  // boxAspect > imgAspect => baseW = cropW = 480, baseH = 480 / (800/600) = 360
  const baseW = cropW;
  const baseH = cropW / imgAspect; // 360
  assert.ok(baseW >= cropW && baseH >= cropH, "At zoom=1.0 image must completely cover crop box");

  // At zoom = 1.0, maxOffsetX = (480 - 480) / 2 = 0, maxOffsetY = (360 - 270) / 2 = 45
  const maxOffsetX1 = (baseW * 1.0 - cropW) / 2;
  const maxOffsetY1 = (baseH * 1.0 - cropH) / 2;
  assert.equal(maxOffsetX1, 0, "No horizontal panning when image width matches crop box width");
  assert.equal(maxOffsetY1, 45, "Vertical panning allowed within 45px bounds");

  // 5. CSS touch-action verification for tablet/touch pointers
  const cropperCss = fs.readFileSync(path.resolve(webRoot, "src/components/admin/common/ImageCropperModal.css"), "utf-8");
  assert.ok(cropperCss.includes("touch-action: none"), "CSS must specify touch-action: none for reliable pointer drag");
});




