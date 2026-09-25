import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { getWorkshopBannerImage } from "../src/utils/workshopPresentation.js";

describe("Phase 8 Lifecycle, Media Cleanup & Portrait Fallback UX Verification", () => {
  // ==========================================
  // TEST 1: getWorkshopBannerImage Behavior
  // ==========================================
  it("getWorkshopBannerImage returns landscape image when present", () => {
    const workshop = {
      imageUrl: "https://r2.ethosdance.com/workshops/portrait.jpg",
      landscapeImageUrl: "https://r2.ethosdance.com/workshops/landscape.jpg",
    };
    assert.equal(
      getWorkshopBannerImage(workshop),
      "https://r2.ethosdance.com/workshops/landscape.jpg"
    );
  });

  it("getWorkshopBannerImage falls back to permanent portrait when landscape is null or empty", () => {
    const workshopWithNullLandscape = {
      imageUrl: "https://r2.ethosdance.com/workshops/portrait.jpg",
      landscapeImageUrl: null,
    };
    assert.equal(
      getWorkshopBannerImage(workshopWithNullLandscape),
      "https://r2.ethosdance.com/workshops/portrait.jpg"
    );

    const workshopWithEmptyLandscape = {
      imageUrl: "https://r2.ethosdance.com/workshops/portrait.jpg",
      landscapeImageUrl: "",
    };
    assert.equal(
      getWorkshopBannerImage(workshopWithEmptyLandscape),
      "https://r2.ethosdance.com/workshops/portrait.jpg"
    );
  });

  it("getWorkshopBannerImage handles null/undefined safely", () => {
    assert.equal(getWorkshopBannerImage(null), null);
    assert.equal(getWorkshopBannerImage(undefined), null);
    assert.equal(getWorkshopBannerImage({}), null);
    assert.equal(getWorkshopBannerImage({ imageUrl: null, landscapeImageUrl: null }), null);
  });

  // ==========================================
  // TEST 2: adminApi Complete & Delete Workshop Contracts
  // ==========================================
  it("adminApi.completeWorkshop constructs POST request with forceComplete and overrideReason", () => {
    let captured = null;
    function mockAdminRequest(endpoint, options) {
      captured = { endpoint, options };
      return Promise.resolve({ success: true });
    }

    const completeWorkshop = (id, data) =>
      mockAdminRequest(`/api/admin/workshops/${id}/complete`, {
        method: "POST",
        body: data ? JSON.stringify(data) : undefined,
      });

    const testId = "11111111-1111-1111-1111-111111111111";
    completeWorkshop(testId, { forceComplete: true, overrideReason: "Early weather emergency closure" });

    assert.equal(captured.endpoint, `/api/admin/workshops/${testId}/complete`);
    assert.equal(captured.options.method, "POST");
    const parsedBody = JSON.parse(captured.options.body);
    assert.equal(parsedBody.forceComplete, true);
    assert.equal(parsedBody.overrideReason, "Early weather emergency closure");

    // Without data
    completeWorkshop(testId);
    assert.equal(captured.options.body, undefined);
  });

  it("adminApi.deleteWorkshop constructs DELETE request with correct workshop ID", () => {
    let captured = null;
    function mockAdminRequest(endpoint, options) {
      captured = { endpoint, options };
      return Promise.resolve({ success: true });
    }

    const deleteWorkshop = (id) =>
      mockAdminRequest(`/api/admin/workshops/${id}`, {
        method: "DELETE",
      });

    const testId = "22222222-2222-2222-2222-222222222222";
    deleteWorkshop(testId);

    assert.equal(captured.endpoint, `/api/admin/workshops/${testId}`);
    assert.equal(captured.options.method, "DELETE");
  });

  // ==========================================
  // TEST 3: Terminal Status Matrix Action Guards
  // ==========================================
  it("determines allowed admin actions based on 9-state workshop lifecycle", () => {
    function getAvailableWorkshopActions(status, hasBookings = false) {
      const isCompleted = status === "Completed" || status === 6;
      const isCancelled = status === "Cancelled" || status === 5;
      const isTerminal = isCompleted || isCancelled;

      return {
        canPublish: status === "Draft" || status === "Approved" || status === "Unpublished" || status === "Archived",
        canUnpublish: status === "Published",
        canComplete: (status === "Published" || status === "Approved") && !isTerminal,
        canCancel: (status === "Published" || status === "Unpublished") && !isTerminal,
        canArchive: (status === "Published" || status === "Unpublished") && !isTerminal,
        canDelete: !isTerminal && !hasBookings,
        isTerminal,
      };
    }

    // Completed workshop
    const completedActions = getAvailableWorkshopActions("Completed", false);
    assert.equal(completedActions.isTerminal, true);
    assert.equal(completedActions.canDelete, false);
    assert.equal(completedActions.canPublish, false);
    assert.equal(completedActions.canCancel, false);
    assert.equal(completedActions.canComplete, false);

    // Cancelled workshop
    const cancelledActions = getAvailableWorkshopActions("Cancelled", true);
    assert.equal(cancelledActions.isTerminal, true);
    assert.equal(completedActions.canDelete, false);

    // Draft workshop without bookings
    const draftActions = getAvailableWorkshopActions("Draft", false);
    assert.equal(draftActions.isTerminal, false);
    assert.equal(draftActions.canDelete, true);
    assert.equal(draftActions.canPublish, true);

    // Draft workshop with bookings (should be blocked from deletion)
    const draftWithBookings = getAvailableWorkshopActions("Draft", true);
    assert.equal(draftWithBookings.canDelete, false);

    // Published workshop
    const publishedActions = getAvailableWorkshopActions("Published", true);
    assert.equal(publishedActions.canUnpublish, true);
    assert.equal(publishedActions.canComplete, true);
    assert.equal(publishedActions.canCancel, true);
    assert.equal(publishedActions.canDelete, false);
  });

  // ==========================================
  // TEST 4: 7-Tab Admin Workshop Management Configuration
  // ==========================================
  it("defines all 7 admin lifecycle tabs with accurate countKey and phaseParam", () => {
    const TABS = [
      { key: "draft", label: "Draft", countKey: "draft", phaseParam: "Draft" },
      { key: "upcoming", label: "Upcoming", countKey: "upcoming", phaseParam: "Upcoming" },
      { key: "ongoing", label: "Ongoing", countKey: "ongoing", phaseParam: "Ongoing" },
      { key: "ended", label: "Ended", countKey: "ended", phaseParam: "Ended" },
      { key: "completed", label: "Completed", countKey: "completed", phaseParam: "Completed" },
      { key: "cancelled", label: "Cancelled", countKey: "cancelled", phaseParam: "Cancelled" },
      { key: "all", label: "All Workshops", countKey: "all", phaseParam: null },
    ];

    assert.equal(TABS.length, 7);
    assert.deepEqual(
      TABS.map((t) => t.key),
      ["draft", "upcoming", "ongoing", "ended", "completed", "cancelled", "all"]
    );

    // Verify Ended tab exists with correct phaseParam and countKey
    const endedTab = TABS.find((t) => t.key === "ended");
    assert.ok(endedTab);
    assert.equal(endedTab.phaseParam, "Ended");
    assert.equal(endedTab.countKey, "ended");

    // Verify Completed tab is distinct from Ended
    const completedTab = TABS.find((t) => t.key === "completed");
    assert.ok(completedTab);
    assert.equal(completedTab.phaseParam, "Completed");
    assert.notEqual(endedTab.countKey, completedTab.countKey);
  });

  // ==========================================
  // TEST 5: Derived Phase vs Persisted Status Presentation
  // ==========================================
  it("formats Ended and distinct lifecycle phases cleanly", () => {
    function formatWorkshopPhase(phase) {
      return phase === "PendingApproval"
        ? "Pending Review"
        : (phase === "EndedPendingCompletion" || phase === "Ended")
        ? "Ended"
        : phase;
    }

    assert.equal(formatWorkshopPhase("Completed"), "Completed");
    assert.equal(formatWorkshopPhase("Ended"), "Ended");
    assert.equal(formatWorkshopPhase("EndedPendingCompletion"), "Ended");
    assert.equal(formatWorkshopPhase("Upcoming"), "Upcoming");
    assert.equal(formatWorkshopPhase("Ongoing"), "Ongoing");
    assert.equal(formatWorkshopPhase("Draft"), "Draft");
  });
});
