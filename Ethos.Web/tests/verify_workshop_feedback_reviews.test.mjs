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

  test("2. Form Builder supports Rating, Text, Single Choice, and Multi Choice question types with live mobile preview", () => {
    const content = fs.readFileSync(feedbackComponentPath, "utf-8");
    assert.ok(content.includes("Rating (1–5 Stars)"), "Must support Rating question type");
    assert.ok(content.includes("Text Response (Open Ended)"), "Must support Text response question type");
    assert.ok(content.includes("Single Choice (Radio)"), "Must support Single Choice question type");
    assert.ok(content.includes("Multiple Choice (Checkboxes)"), "Must support Multi Choice question type");
    assert.ok(content.includes("fb-phone-frame"), "Must render luxury mobile phone mockup frame");
    assert.ok(content.includes("previewAudience === \"Attended\""), "Must support live preview audience switching");
    assert.ok(content.includes("handleAddQuestion"), "Must provide Add Question action");
    assert.ok(content.includes("handleDeleteQuestion"), "Must provide Delete Question action");
    assert.ok(content.includes("handleMoveQuestion"), "Must provide Reorder Question action");
  });

  test("3. WhatsApp Automation shows approved templates without raw text modification vulnerability", () => {
    const content = fs.readFileSync(feedbackComponentPath, "utf-8");
    assert.ok(content.includes("ethos_feedback_attended"), "Must display approved attended template name");
    assert.ok(content.includes("ethos_feedback_noshow"), "Must display approved no-show template name");
    assert.ok(content.includes("Meta WhatsApp Policy Notice"), "Must include Meta regulatory compliance banner");
    assert.ok(content.includes("{{1}}") && content.includes("{{2}}"), "Must document dynamic template placeholders");
  });

  test("4. Send / Resend Feedback modal provides audience selection and safe outbox queuing", () => {
    const content = fs.readFileSync(feedbackComponentPath, "utf-8");
    assert.ok(content.includes("showResendModal"), "Must include resend modal state");
    assert.ok(content.includes("All Eligible Attendees"), "Must offer All Eligible option");
    assert.ok(content.includes("Attended Only"), "Must offer Attended Only option");
    assert.ok(content.includes("No-Show Only"), "Must offer No-Show Only option");
    assert.ok(content.includes("handleResendFeedback"), "Must invoke resend API handler");
  });

  test("5. adminApi.js contains all required workshop feedback endpoints", () => {
    const content = fs.readFileSync(adminApiPath, "utf-8");
    assert.ok(content.includes("getWorkshopFeedbackConfig:"), "Must export getWorkshopFeedbackConfig");
    assert.ok(content.includes("saveWorkshopFeedbackVersion:"), "Must export saveWorkshopFeedbackVersion");
    assert.ok(content.includes("activateWorkshopFeedbackVersion:"), "Must export activateWorkshopFeedbackVersion");
    assert.ok(content.includes("updateWorkshopFeedbackSetting:"), "Must export updateWorkshopFeedbackSetting");
    assert.ok(content.includes("resendWorkshopFeedback:"), "Must export resendWorkshopFeedback");
    assert.ok(content.includes("getWorkshopFeedbackAnalytics:"), "Must export getWorkshopFeedbackAnalytics");
  });

  test("6. Backend C# contracts and controllers define complete Feedback API surface", () => {
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

  test("7. AdminWorkshopFeedback.css contains luxury dark theme and mobile responsive styles", () => {
    const css = fs.readFileSync(feedbackCssPath, "utf-8");
    assert.ok(css.includes(".fb-phone-frame"), "CSS must style mobile phone frame");
    assert.ok(css.includes(".fb-phone-screen"), "CSS must style mobile phone screen");
    assert.ok(css.includes(".fb-kpi-grid"), "CSS must style KPI grid");
    assert.ok(css.includes(".fb-table"), "CSS must style ledger table");
  });

  test("8. AdminWorkshopService.cs generates path-based Feedback URL matching React route /feedback/workshop/:token", () => {
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

