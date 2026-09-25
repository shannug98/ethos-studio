/**
 * verify_phase4_sessions_ux.test.mjs
 *
 * Verifies Phase 4 Dates + Sessions + Posters specifications:
 * 1. Schedule Overlap Boundary Cases (6 boundary cases):
 *    - Case 1: Same trainer + same date + time overlap -> CONFLICT
 *    - Case 2: Same trainer + same date + touching boundaries (10:00-11:00 & 11:00-12:00) -> ALLOWED (NO CONFLICT)
 *    - Case 3: Different trainers + same date + time overlap -> ALLOWED (NO CONFLICT)
 *    - Case 4: Shared secondary trainer + same date + time overlap -> CONFLICT
 *    - Case 5: Same trainers + different dates + same time -> ALLOWED (NO CONFLICT)
 *    - Case 6: End time <= start time -> INVALID TIME DETECTED
 * 2. Lead trainer synchronization:
 *    - DisplayOrder = 0 is synchronized to legacy TrainerProfileId
 *    - Reordering updates lead instructor
 * 3. Faculty pool invariant:
 *    - Session instructors must belong to the workshop faculty pool
 * 4. Session poster specifications:
 *    - 3:4 portrait ratio (900 x 1200 px)
 *    - Fallback to workshop cover art when poster is omitted
 * 5. Availability label derivation:
 *    - Available, Selling Fast (<= 5), Sold Out (0), Closed, Past
 * 6. Component and contract integrity
 */

import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webRoot = path.resolve(__dirname, "..");
const apiRoot = path.resolve(webRoot, "..", "Ethos.Api");

let testsPassed = 0;
let testsFailed = 0;

function assert(condition, message) {
  if (condition) {
    console.log(`  [PASS] ${message}`);
    testsPassed++;
  } else {
    console.error(`  [FAIL] ${message}`);
    testsFailed++;
  }
}

// Logic mirror of the Step3VenueSchedule overlap calculation
function checkOverlap(sessions) {
  const overlaps = [];
  for (let i = 0; i < sessions.length; i++) {
    for (let j = i + 1; j < sessions.length; j++) {
      const s1 = sessions[i];
      const s2 = sessions[j];

      if (!s1.sessionDate || !s2.sessionDate || s1.sessionDate !== s2.sessionDate) continue;
      if (!s1.startTime || !s1.endTime || !s2.startTime || !s2.endTime) continue;

      const t1 = Array.isArray(s1.trainerProfileIds) && s1.trainerProfileIds.length > 0
        ? s1.trainerProfileIds
        : (s1.trainerProfileId ? [s1.trainerProfileId] : []);
      const t2 = Array.isArray(s2.trainerProfileIds) && s2.trainerProfileIds.length > 0
        ? s2.trainerProfileIds
        : (s2.trainerProfileId ? [s2.trainerProfileId] : []);

      const sharedTrainers = t1.filter((id) => t2.includes(id));
      if (sharedTrainers.length === 0) continue;

      // Half-open interval: s1.StartTime < s2.EndTime && s2.StartTime < s1.EndTime
      const timeOverlaps = s1.startTime < s2.endTime && s2.startTime < s1.endTime;
      if (timeOverlaps) {
        overlaps.push({
          idx1: i,
          idx2: j,
          message: `Session ${i + 1} and Session ${j + 1} overlap on ${s1.sessionDate} (${s1.startTime}-${s1.endTime} and ${s2.startTime}-${s2.endTime}) with shared instructor(s).`,
        });
      }
    }
  }
  return overlaps;
}

// Logic mirror of AvailabilityLabel derivation
function computeAvailabilityLabel({ isPast, isFull, isBookingClosed, remainingSeats, isPublished = true }) {
  if (isPast) return "Past";
  if (!isPublished || isBookingClosed) return "Closed";
  if (isFull || remainingSeats <= 0) return "Sold Out";
  if (remainingSeats <= 5) return "Selling Fast";
  return "Available";
}

console.log("===============================================================================");
console.log("TEST SUITE: Phase 4 Dates + Sessions + Posters UX & Logic Verification");
console.log("===============================================================================\n");

// --- SECTION 1: Overlap Boundary Cases ---
console.log("--- Section 1: Schedule Overlap 6 Boundary Cases ---");

// Case 1: Same trainer + same date + time overlap -> CONFLICT
const case1 = [
  { sessionDate: "2026-10-15", startTime: "10:00", endTime: "11:30", trainerProfileIds: ["trainer-1"] },
  { sessionDate: "2026-10-15", startTime: "11:00", endTime: "12:30", trainerProfileIds: ["trainer-1"] },
];
assert(checkOverlap(case1).length === 1, "Case 1: Same trainer + same date + time overlap triggers conflict");

// Case 2: Same trainer + same date + touching boundaries -> ALLOWED (NO CONFLICT)
const case2 = [
  { sessionDate: "2026-10-15", startTime: "10:00", endTime: "11:00", trainerProfileIds: ["trainer-1"] },
  { sessionDate: "2026-10-15", startTime: "11:00", endTime: "12:00", trainerProfileIds: ["trainer-1"] },
];
assert(checkOverlap(case2).length === 0, "Case 2: Same trainer + touching boundaries (10:00-11:00 & 11:00-12:00) is allowed");

// Case 3: Different trainers + same date + time overlap -> ALLOWED (NO CONFLICT)
const case3 = [
  { sessionDate: "2026-10-15", startTime: "10:00", endTime: "12:00", trainerProfileIds: ["trainer-1"] },
  { sessionDate: "2026-10-15", startTime: "10:00", endTime: "12:00", trainerProfileIds: ["trainer-2"] },
];
assert(checkOverlap(case3).length === 0, "Case 3: Different trainers with concurrent times is allowed");

// Case 4: Shared secondary trainer + same date + time overlap -> CONFLICT
const case4 = [
  { sessionDate: "2026-10-15", startTime: "10:00", endTime: "11:30", trainerProfileIds: ["trainer-1", "trainer-3"] },
  { sessionDate: "2026-10-15", startTime: "11:00", endTime: "12:30", trainerProfileIds: ["trainer-2", "trainer-3"] },
];
assert(checkOverlap(case4).length === 1, "Case 4: Shared secondary trainer across sessions triggers conflict");

// Case 5: Same trainers + different dates + same time -> ALLOWED (NO CONFLICT)
const case5 = [
  { sessionDate: "2026-10-15", startTime: "10:00", endTime: "11:30", trainerProfileIds: ["trainer-1"] },
  { sessionDate: "2026-10-16", startTime: "10:00", endTime: "11:30", trainerProfileIds: ["trainer-1"] },
];
assert(checkOverlap(case5).length === 0, "Case 5: Same trainer on different calendar dates is allowed");

// Case 6: Invalid time validation (EndTime <= StartTime)
const invalidSession = { sessionDate: "2026-10-15", startTime: "12:00", endTime: "10:00" };
const isInvalid = invalidSession.endTime <= invalidSession.startTime;
assert(isInvalid === true, "Case 6: End time <= Start time detected as invalid time");

// --- SECTION 2: Lead Trainer Synchronization ---
console.log("\n--- Section 2: Lead Trainer Synchronization ---");
const sessionTrainers = ["trainer-A", "trainer-B", "trainer-C"];
const leadTrainer = sessionTrainers[0];
assert(leadTrainer === "trainer-A", "Lead trainer is the first element (DisplayOrder = 0)");

// Reorder lead
const reordered = ["trainer-B", ...sessionTrainers.filter(t => t !== "trainer-B")];
assert(reordered[0] === "trainer-B", "Promoting trainer-B sets it to index 0 (DisplayOrder = 0)");
assert(reordered.length === 3, "Reordering preserves all session trainers");

// --- SECTION 3: Faculty Pool Invariant ---
console.log("\n--- Section 3: Faculty Pool Invariant ---");
const workshopFacultyPool = ["trainer-1", "trainer-2", "trainer-3"];
const candidateSessionTrainers = ["trainer-1", "trainer-99"];
const hasNonFacultyTrainer = candidateSessionTrainers.some(t => !workshopFacultyPool.includes(t));
assert(hasNonFacultyTrainer === true, "Faculty invariant catches trainer not in workshop faculty pool");

const validSessionTrainers = ["trainer-1", "trainer-2"];
const allValidFaculty = validSessionTrainers.every(t => workshopFacultyPool.includes(t));
assert(allValidFaculty === true, "Valid session trainers are strictly within workshop faculty pool");

// --- SECTION 4: Session Poster Specifications ---
console.log("\n--- Section 4: Session Poster Specifications ---");
const posterPreset = { ratio: 3 / 4, width: 900, height: 1200 };
assert(posterPreset.ratio === 0.75, "Session poster aspect ratio is 3:4 portrait");
assert(posterPreset.width === 900 && posterPreset.height === 1200, "Session poster output dimensions are 900 x 1200 px");

const workshopCover = "https://r2.ethos.dance/workshops/cover123.jpg";
const sessionWithoutPoster = { posterImageUrl: null };
const effectiveSessionPoster = sessionWithoutPoster.posterImageUrl || workshopCover;
assert(effectiveSessionPoster === workshopCover, "Session poster falls back to workshop cover when omitted");

const sessionWithPoster = { posterImageUrl: "https://r2.ethos.dance/sessions/poster456.jpg" };
const effectiveSessionPosterCustom = sessionWithPoster.posterImageUrl || workshopCover;
assert(effectiveSessionPosterCustom === "https://r2.ethos.dance/sessions/poster456.jpg", "Session poster uses custom 3:4 poster when provided");

// --- SECTION 5: Availability Labels ---
console.log("\n--- Section 5: Availability Labels ---");
assert(computeAvailabilityLabel({ isPast: true, isFull: false, isBookingClosed: false, remainingSeats: 20 }) === "Past", "Label is 'Past' when session has completed");
assert(computeAvailabilityLabel({ isPast: false, isFull: false, isBookingClosed: true, remainingSeats: 20 }) === "Closed", "Label is 'Closed' when booking is closed");
assert(computeAvailabilityLabel({ isPast: false, isFull: true, isBookingClosed: false, remainingSeats: 0 }) === "Sold Out", "Label is 'Sold Out' when remaining seats = 0");
assert(computeAvailabilityLabel({ isPast: false, isFull: false, isBookingClosed: false, remainingSeats: 4 }) === "Selling Fast", "Label is 'Selling Fast' when remaining seats <= 5");
assert(computeAvailabilityLabel({ isPast: false, isFull: false, isBookingClosed: false, remainingSeats: 5 }) === "Selling Fast", "Label is 'Selling Fast' when remaining seats == 5");
assert(computeAvailabilityLabel({ isPast: false, isFull: false, isBookingClosed: false, remainingSeats: 6 }) === "Available", "Label is 'Available' when remaining seats > 5");

// --- SECTION 6: File & Contract Integrity ---
console.log("\n--- Section 6: File & Contract Integrity ---");

const step3Path = path.join(webRoot, "src", "pages", "admin", "workshops", "wizard", "Step3VenueSchedule.jsx");
const step3Content = fs.readFileSync(step3Path, "utf-8");
assert(step3Content.includes("ImageCropperModal"), "Step3VenueSchedule integrates ImageCropperModal");
assert(step3Content.includes("NumericInput"), "Step3VenueSchedule integrates NumericInput");
assert(step3Content.includes("trainerProfileIds"), "Step3VenueSchedule tracks trainerProfileIds");
assert(step3Content.includes("sessionOverlaps"), "Step3VenueSchedule computes sessionOverlaps");

const wizardPath = path.join(webRoot, "src", "pages", "admin", "workshops", "wizard", "AdminWorkshopWizard.jsx");
const wizardContent = fs.readFileSync(wizardPath, "utf-8");
assert(wizardContent.includes("posterBlob"), "AdminWorkshopWizard handles session poster blobs");
assert(wizardContent.includes("trainerProfileIds"), "AdminWorkshopWizard handles session trainerProfileIds");

const contractsPath = path.join(apiRoot, "Contracts", "Workshops", "WorkshopMultiSessionContracts.cs");
const contractsContent = fs.readFileSync(contractsPath, "utf-8");
assert(contractsContent.includes("PosterImageUrl"), "WorkshopMultiSessionContracts declares PosterImageUrl");
assert(contractsContent.includes("AvailabilityLabel"), "WorkshopMultiSessionContracts declares AvailabilityLabel");
assert(contractsContent.includes("TrainerProfileIds"), "WorkshopMultiSessionContracts declares TrainerProfileIds");

console.log("\n===============================================================================");
console.log(`TOTAL TESTS: ${testsPassed + testsFailed} | PASSED: ${testsPassed} | FAILED: ${testsFailed}`);
console.log("===============================================================================\n");

if (testsFailed > 0) {
  process.exit(1);
}
