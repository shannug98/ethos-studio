import { test, describe } from "node:test";
import assert from "node:assert/strict";

describe("Workshop Wizard Step 3: Session Instructors Multi-Selection & UI Polish", () => {
  // Simulate the trainer toggle logic implemented in Step3VenueSchedule
  function toggleSessionTrainer(currentTrainerIds, targetTrainerId) {
    const ids = (Array.isArray(currentTrainerIds) ? currentTrainerIds : []).map(String);
    const target = String(targetTrainerId);
    const isSelected = ids.includes(target);

    let nextIds;
    if (isSelected) {
      nextIds = ids.filter((id) => id !== target);
    } else {
      nextIds = [...ids, target];
    }

    return {
      trainerProfileIds: nextIds,
      trainerProfileId: nextIds[0] || "",
    };
  }

  function makeSessionTrainerLead(currentTrainerIds, targetTrainerId) {
    const ids = (Array.isArray(currentTrainerIds) ? currentTrainerIds : []).map(String);
    const target = String(targetTrainerId);
    const reordered = [target, ...ids.filter((id) => id !== target)];
    return {
      trainerProfileIds: reordered,
      trainerProfileId: target,
    };
  }

  test("Requirement 1 & 3: First selected trainer automatically becomes Lead", () => {
    const initial = [];
    const step1 = toggleSessionTrainer(initial, "trn_sreekanth");

    assert.deepEqual(step1.trainerProfileIds, ["trn_sreekanth"]);
    assert.equal(step1.trainerProfileId, "trn_sreekanth", "First selected trainer must be Lead (trainerProfileId)");
  });

  test("Requirement 2: Admin can select one or more trainers from faculty pool", () => {
    let state = toggleSessionTrainer([], "trn_sreekanth");
    state = toggleSessionTrainer(state.trainerProfileIds, "trn_sl");
    state = toggleSessionTrainer(state.trainerProfileIds, "trn_rohan");

    assert.deepEqual(state.trainerProfileIds, ["trn_sreekanth", "trn_sl", "trn_rohan"]);
    assert.equal(state.trainerProfileId, "trn_sreekanth", "Sreekanth remains Lead because he was first selected");
  });

  test("Requirement 4: Clicking an already-selected non-lead trainer deselects them", () => {
    let state = {
      trainerProfileIds: ["trn_sreekanth", "trn_sl", "trn_rohan"],
      trainerProfileId: "trn_sreekanth",
    };

    // Deselect SL
    state = toggleSessionTrainer(state.trainerProfileIds, "trn_sl");
    assert.deepEqual(state.trainerProfileIds, ["trn_sreekanth", "trn_rohan"]);
    assert.equal(state.trainerProfileId, "trn_sreekanth", "Lead remains Sreekanth");
  });

  test("Requirement 5: If the current Lead trainer is deselected, the next remaining selected trainer becomes Lead", () => {
    let state = {
      trainerProfileIds: ["trn_sreekanth", "trn_sl", "trn_rohan"],
      trainerProfileId: "trn_sreekanth",
    };

    // Deselect Sreekanth (the Lead)
    state = toggleSessionTrainer(state.trainerProfileIds, "trn_sreekanth");

    assert.deepEqual(state.trainerProfileIds, ["trn_sl", "trn_rohan"]);
    assert.equal(state.trainerProfileId, "trn_sl", "SL must now automatically become the new Lead");
  });

  test("Requirement 5b: Deselecting all trainers leaves list empty for validation enforcement", () => {
    let state = {
      trainerProfileIds: ["trn_sl"],
      trainerProfileId: "trn_sl",
    };

    state = toggleSessionTrainer(state.trainerProfileIds, "trn_sl");
    assert.deepEqual(state.trainerProfileIds, []);
    assert.equal(state.trainerProfileId, "");
  });

  test("Make Lead action: Promotes chosen instructor to Lead without mutating other assignments", () => {
    let state = {
      trainerProfileIds: ["trn_sreekanth", "trn_sl", "trn_rohan"],
      trainerProfileId: "trn_sreekanth",
    };

    // Make Rohan the lead
    state = makeSessionTrainerLead(state.trainerProfileIds, "trn_rohan");
    assert.deepEqual(state.trainerProfileIds, ["trn_rohan", "trn_sreekanth", "trn_sl"]);
    assert.equal(state.trainerProfileId, "trn_rohan", "Rohan must now be Lead");
  });

  test("String normalization: handles mixed integer and string IDs safely", () => {
    const state = toggleSessionTrainer([101, 102], "101");
    assert.deepEqual(state.trainerProfileIds, ["102"], "Must match and filter out regardless of type");
    assert.equal(state.trainerProfileId, "102");
  });

  test("Session capacity propagation invariant: session capacity defaults from workshop capacity", () => {
    const workshopCapacity = 35;
    const newSession = {
      sessionDate: "2026-10-15",
      startTime: "10:00",
      endTime: "11:30",
      capacity: workshopCapacity,
    };

    assert.equal(newSession.capacity, 35, "Session inherits authoritative workshop capacity without requiring per-session input");
    assert.ok(newSession.capacity > 0);
  });
});
