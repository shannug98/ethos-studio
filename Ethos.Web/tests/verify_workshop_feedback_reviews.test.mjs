import { test, describe } from "node:test";
import assert from "node:assert";
import fs from "node:fs";
import path from "node:path";

describe("Workshop Feedback & Reviews Comprehensive Verification", () => {
  const feedbackComponentPath = path.resolve("src/pages/admin/workshops/AdminWorkshopFeedback.jsx");
  const feedbackCssPath = path.resolve("src/pages/admin/workshops/AdminWorkshopFeedback.css");
  const adminApiPath = path.resolve("src/services/adminApi.js");
  const contractsPath = path.resolve("../Ethos.Api/Contracts/Feedback/FeedbackContracts.cs");
  const servicePath = path.resolve("../Ethos.Api/Application/Admin/AdminWorkshopService.cs");
  const controllerPath = path.resolve("../Ethos.Api/Controllers/Admin/AdminWorkshopsController.cs");

  test("1. AdminWorkshopFeedback.jsx includes all 4 primary subpage tabs", () => {
    const content = fs.readFileSync(feedbackComponentPath, "utf-8");
    assert.ok(content.includes('activeTab === "builder"'), "Must include Feedback Form Builder tab");
    assert.ok(content.includes('activeTab === "automation"'), "Must include WhatsApp Automation tab");
    assert.ok(content.includes('activeTab === "analytics"'), "Must include Responses & Analytics tab");
    assert.ok(content.includes('activeTab === "recipients"'), "Must include Attendee Ledger & Tokens tab");
  });

  test("2. Form Builder provides dedicated Attended and No-Show forms without per-question audience dropdown", () => {
    const content = fs.readFileSync(feedbackComponentPath, "utf-8");
    assert.ok(content.includes("🙋 ATTENDED FORM"), "Must provide Attended Form selector");
    assert.ok(content.includes("🚫 NO-SHOW FORM"), "Must provide No-Show Form selector");
    assert.ok(content.includes("DEFAULT_ATTENDED_QUESTIONS"), "Must define default Attended question set");
    assert.ok(content.includes("DEFAULT_NOSHOW_QUESTIONS"), "Must define default No-Show question set");
    assert.ok(
      !content.includes("<label className=\"fb-form-label\">Target Audience Filter</label>"),
      "Must NOT render per-question Target Audience Filter dropdown in question cards"
    );
    assert.ok(content.includes("fb-phone-frame"), "Must render luxury mobile phone mockup frame");
    assert.ok(content.includes("handleAddQuestion"), "Must provide Add Question action");
    assert.ok(content.includes("handleDeleteQuestion"), "Must provide Delete Question action");
    assert.ok(content.includes("handleMoveQuestion"), "Must provide Reorder Question action");
  });

  test("3. Responses & Analytics supports audience-segregated review and question breakdown", () => {
    const content = fs.readFileSync(feedbackComponentPath, "utf-8");
    assert.ok(content.includes("analyticsAudienceFilter"), "Must have audience filter state for analytics");
    assert.ok(content.includes("All Responses"), "Must offer All Responses filter pill");
    assert.ok(content.includes("🙋 Attended"), "Must offer Attended filter pill");
    assert.ok(content.includes("🚫 No-Show"), "Must offer No-Show filter pill");
    assert.ok(content.includes("filteredSubmissions"), "Must compute filtered submissions by audience");
    assert.ok(content.includes("filteredQuestionAnalytics"), "Must compute filtered question analytics by audience");
  });

  test("4. WhatsApp Automation shows approved templates without raw text modification vulnerability", () => {
    const content = fs.readFileSync(feedbackComponentPath, "utf-8");
    assert.ok(content.includes("ethos_feedback_attended"), "Must display approved attended template name");
    assert.ok(content.includes("ethos_feedback_noshow"), "Must display approved no-show template name");
    assert.ok(content.includes("Meta WhatsApp Policy Notice"), "Must include Meta regulatory compliance banner");
    assert.ok(content.includes("{{1}}") && content.includes("{{2}}"), "Must document dynamic template placeholders");
  });

  test("5. Send / Resend Feedback modal is explicitly configured as a manual recovery tool with dynamic counts and ledger actions", () => {
    const content = fs.readFileSync(feedbackComponentPath, "utf-8");
    assert.ok(content.includes("showResendModal"), "Must include resend modal state");
    assert.ok(content.includes("Bulk Manual Feedback Send") || content.includes("Manual Feedback Resend Tool") || content.includes("Manual Resend"), "Must label as manual recovery tool");
    assert.ok(content.includes("Both (Attended & No-Show)") || content.includes("Both (Attended &amp; No-Show)"), "Must offer Both option");
    assert.ok(content.includes("Attended Only"), "Must offer Attended Only option");
    assert.ok(content.includes("No-Show Only"), "Must offer No-Show Only option");
    assert.ok(content.includes("eligibleBothCount"), "Must calculate dynamic eligible count for Both");
    assert.ok(content.includes("eligibleAttendedCount"), "Must calculate dynamic eligible count for Attended");
    assert.ok(content.includes("eligibleNoShowCount"), "Must calculate dynamic eligible count for No-Show");
    assert.ok(content.includes("fb-resend-confirmation-box"), "Must render explicit confirmation block with recipient counts");
    assert.ok(content.includes("handleIndividualSend"), "Must provide individual attendee send/resend action");
    assert.ok(content.includes("handleResendFeedback"), "Must invoke resend API handler");
  });

  test("6. adminApi.js contains all required workshop feedback endpoints with defensive serialization", () => {
    const content = fs.readFileSync(adminApiPath, "utf-8").replace(/\r\n/g, "\n");
    assert.ok(content.includes("getWorkshopFeedbackConfig:"), "Must export getWorkshopFeedbackConfig");
    assert.ok(content.includes("saveWorkshopFeedbackVersion:"), "Must export saveWorkshopFeedbackVersion");
    assert.ok(content.includes("activateWorkshopFeedbackVersion:"), "Must export activateWorkshopFeedbackVersion");
    assert.ok(content.includes("updateWorkshopFeedbackSetting:"), "Must export updateWorkshopFeedbackSetting");
    assert.ok(content.includes("resendWorkshopFeedback:"), "Must export resendWorkshopFeedback");
    assert.ok(content.includes("getWorkshopFeedbackAnalytics:"), "Must export getWorkshopFeedbackAnalytics");
    assert.ok(
      content.includes('typeof body === "object"') && content.includes("body = JSON.stringify(body)"),
      "adminRequest must defensively stringify object request bodies"
    );
  });

  test("7. Backend C# contracts and controllers define complete Feedback API surface", () => {
    const contracts = fs.readFileSync(contractsPath, "utf-8");
    assert.ok(contracts.includes("AdminWorkshopFeedbackConfigResponse"), "Contracts must define Config response");
    assert.ok(contracts.includes("AdminSaveFeedbackVersionRequest"), "Contracts must define Save Version request");
    assert.ok(contracts.includes("AdminResendFeedbackRequest"), "Contracts must define Resend request");
    assert.ok(contracts.includes("AdminWorkshopFeedbackAnalyticsResponse"), "Contracts must define Analytics response");

    const controller = fs.readFileSync(controllerPath, "utf-8");
    assert.ok(controller.includes('workshops/{workshopId:guid}/feedback/config'), "Controller must route config endpoint");
    assert.ok(controller.includes('workshops/{workshopId:guid}/feedback/versions'), "Controller must route versions endpoint");
    assert.ok(controller.includes('workshops/{workshopId:guid}/feedback/resend'), "Controller must route resend endpoint");
    assert.ok(controller.includes('workshops/{workshopId:guid}/feedback/analytics'), "Controller must route analytics endpoint");
  });

  test("8. AdminWorkshopFeedback.css contains dual-audience selector and luxury responsive styles", () => {
    const css = fs.readFileSync(feedbackCssPath, "utf-8");
    assert.ok(css.includes(".fb-audience-selector-row"), "CSS must style audience selector row");
    assert.ok(css.includes(".fb-audience-card"), "CSS must style audience cards");
    assert.ok(css.includes(".fb-phone-frame"), "CSS must style mobile phone frame");
    assert.ok(css.includes(".fb-kpi-grid"), "CSS must style KPI grid");
    assert.ok(css.includes(".fb-analytics-filter-row"), "CSS must style analytics filter row");
  });

  test("9. AdminWorkshopService.cs generates path-based Feedback URL matching React route /feedback/workshop/:token", () => {
    const serviceContent = fs.readFileSync(servicePath, "utf-8");
    assert.ok(
      serviceContent.includes('feedbackUrl = $"https://ethosdancestudio.com/feedback/workshop/{rawToken}";'),
      "AdminWorkshopService must construct path-based feedback URL"
    );
    assert.ok(
      !serviceContent.includes('feedbackUrl = $"https://ethosdancestudio.com/feedback/workshop?token='),
      "AdminWorkshopService must not construct query-parameter feedback URL"
    );
  });
});
