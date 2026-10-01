import React, { useState, useEffect, useCallback, useMemo } from "react";
import { useOutletContext } from "react-router-dom";
import { adminApi } from "../../../services/adminApi";
import "./AdminWorkshopFeedback.css";

// Authoritative Default Feedback Question Sets
const DEFAULT_ATTENDED_QUESTIONS = [
  {
    id: "def_att_1",
    questionKey: "overall_rating",
    promptText: "How was your overall experience?",
    questionType: 1, // Rating1To5
    targetAudience: 1, // Attended
    choices: [],
    isRequired: true,
    sortOrder: 1,
  },
  {
    id: "def_att_2",
    questionKey: "teaching_rating",
    promptText: "How would you rate the instructor's teaching & choreography?",
    questionType: 1, // Rating1To5
    targetAudience: 1, // Attended
    choices: [],
    isRequired: true,
    sortOrder: 2,
  },
  {
    id: "def_att_3",
    questionKey: "venue_rating",
    promptText: "How was the studio venue, floor, and sound system?",
    questionType: 1, // Rating1To5
    targetAudience: 1, // Attended
    choices: [],
    isRequired: true,
    sortOrder: 3,
  },
  {
    id: "def_att_4",
    questionKey: "enjoy_most",
    promptText: "What did you enjoy most about this workshop?",
    questionType: 3, // Text
    targetAudience: 1, // Attended
    choices: [],
    isRequired: false,
    sortOrder: 4,
  },
  {
    id: "def_att_5",
    questionKey: "suggestions",
    promptText: "Any suggestions to help us make the next workshop even better?",
    questionType: 3, // Text
    targetAudience: 1, // Attended
    choices: [],
    isRequired: false,
    sortOrder: 5,
  },
];

const DEFAULT_NOSHOW_QUESTIONS = [
  {
    id: "def_noshow_1",
    questionKey: "noshow_reason",
    promptText: "We missed you! What prevented you from attending?",
    questionType: 2, // SingleChoice
    targetAudience: 2, // NoShow
    choices: [
      "Schedule Conflict",
      "Personal / Health Reason",
      "Transportation / Travel",
      "Other",
    ],
    isRequired: true,
    sortOrder: 1,
  },
  {
    id: "def_noshow_2",
    questionKey: "booking_experience",
    promptText: "How was your booking and communication experience?",
    questionType: 1, // Rating1To5
    targetAudience: 2, // NoShow
    choices: [],
    isRequired: true,
    sortOrder: 2,
  },
  {
    id: "def_noshow_3",
    questionKey: "future_interest",
    promptText: "Would you like to attend future Ethos workshops?",
    questionType: 2, // SingleChoice
    targetAudience: 2, // NoShow
    choices: ["Yes, definitely", "Maybe / Depends on the topic", "No"],
    isRequired: true,
    sortOrder: 3,
  },
  {
    id: "def_noshow_4",
    questionKey: "noshow_feedback",
    promptText: "Any other feedback or suggestions for the Ethos team?",
    questionType: 3, // Text
    targetAudience: 2, // NoShow
    choices: [],
    isRequired: false,
    sortOrder: 4,
  },
];

export default function AdminWorkshopFeedback() {
  const { workshop } = useOutletContext();
  const workshopId = workshop.Id || workshop.id;

  // Active Tab: "builder" | "automation" | "analytics" | "recipients"
  const [activeTab, setActiveTab] = useState("builder");

  // Form Builder Sub-View: "Attended" | "NoShow"
  const [formSubTab, setFormSubTab] = useState("Attended");

  // Config & Form State
  const [config, setConfig] = useState(null);
  const [analytics, setAnalytics] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [successMsg, setSuccessMsg] = useState(null);

  // Audience-Segregated Questions
  const [attendedQuestions, setAttendedQuestions] = useState(DEFAULT_ATTENDED_QUESTIONS);
  const [noShowQuestions, setNoShowQuestions] = useState(DEFAULT_NOSHOW_QUESTIONS);

  // Responses & Analytics Audience Filter: "All" | "Attended" | "NoShow"
  const [analyticsAudienceFilter, setAnalyticsAudienceFilter] = useState("All");

  // Resend Feedback Modal (Exceptional Recovery Tool)
  const [showResendModal, setShowResendModal] = useState(false);
  const [resendAudience, setResendAudience] = useState("All"); // "All" | "Attended" | "NoShow"
  const [resending, setResending] = useState(false);
  const [resendResult, setResendResult] = useState(null);

  // Search filter for recipient ledger
  const [recipientSearch, setRecipientSearch] = useState("");
  const [copiedTokenBookingId, setCopiedTokenBookingId] = useState(null);

  // Helper to normalize choices
  const parseChoices = (q) => {
    if (Array.isArray(q.choices) && q.choices.length > 0) return q.choices;
    if (Array.isArray(q.Choices) && q.Choices.length > 0) return q.Choices;
    const json = q.optionsJson || q.OptionsJson;
    if (json) {
      try {
        const parsed = JSON.parse(json);
        if (Array.isArray(parsed) && parsed.length > 0) return parsed;
      } catch {
        return json.split(",").map((s) => s.trim()).filter(Boolean);
      }
    }
    return ["Option 1", "Option 2"];
  };

  // Load Configuration & Analytics
  const loadConfig = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [configData, analyticsData] = await Promise.all([
        adminApi.getWorkshopFeedbackConfig(workshopId),
        adminApi.getWorkshopFeedbackAnalytics(workshopId).catch(() => null),
      ]);
      setConfig(configData);
      setAnalytics(analyticsData);

      const rawQs = configData?.ActiveQuestions || configData?.activeQuestions || [];
      if (rawQs.length > 0) {
        const attended = [];
        const noshow = [];

        rawQs.forEach((q) => {
          const aud = Number(q.TargetAudience ?? q.targetAudience ?? 1);
          const normalized = {
            id: q.Id || q.id,
            questionKey: q.QuestionKey || q.questionKey || "",
            promptText: q.PromptText || q.promptText || "",
            questionType: Number(q.QuestionType ?? q.questionType ?? 1),
            targetAudience: aud,
            optionsJson: q.OptionsJson || q.optionsJson || "",
            choices: parseChoices(q),
            isRequired: Boolean(q.IsRequired ?? q.isRequired ?? true),
            sortOrder: q.SortOrder || q.sortOrder || 1,
          };

          if (aud === 1) {
            attended.push({ ...normalized, targetAudience: 1 });
          } else if (aud === 2) {
            noshow.push({ ...normalized, targetAudience: 2 });
          } else if (aud === 3) {
            // "Both" legacy questions: duplicate into both forms with specific audience
            attended.push({ ...normalized, targetAudience: 1 });
            noshow.push({ ...normalized, targetAudience: 2 });
          }
        });

        // If either set is empty, supply default questions
        setAttendedQuestions(attended.length > 0 ? attended : DEFAULT_ATTENDED_QUESTIONS);
        setNoShowQuestions(noshow.length > 0 ? noshow : DEFAULT_NOSHOW_QUESTIONS);
      } else {
        setAttendedQuestions(DEFAULT_ATTENDED_QUESTIONS);
        setNoShowQuestions(DEFAULT_NOSHOW_QUESTIONS);
      }
    } catch (err) {
      setError(err?.message || "Failed to load workshop feedback configuration.");
    } finally {
      setLoading(false);
    }
  }, [workshopId]);

  useEffect(() => {
    loadConfig();
  }, [loadConfig]);

  // Active Questions pointer based on formSubTab
  const activeQuestions = formSubTab === "Attended" ? attendedQuestions : noShowQuestions;
  const setActiveQuestions = formSubTab === "Attended" ? setAttendedQuestions : setNoShowQuestions;

  // Question Management Handlers for Current Active Form View
  const handleAddQuestion = () => {
    const audNum = formSubTab === "Attended" ? 1 : 2;
    const newQ = {
      id: "temp_" + Date.now(),
      questionKey: `q_${formSubTab.toLowerCase()}_${activeQuestions.length + 1}_${Math.random().toString(36).substring(2, 6)}`,
      promptText: formSubTab === "Attended" ? "How was your experience with..." : "Tell us what could have helped...",
      questionType: 1, // Rating1To5
      targetAudience: audNum,
      optionsJson: "",
      choices: ["Option 1", "Option 2"],
      isRequired: true,
      sortOrder: activeQuestions.length + 1,
    };
    setActiveQuestions([...activeQuestions, newQ]);
  };

  const handleUpdateQuestion = (index, field, value) => {
    const updated = [...activeQuestions];
    updated[index] = { ...updated[index], [field]: value };
    setActiveQuestions(updated);
  };

  const handleDeleteQuestion = (index) => {
    if (activeQuestions.length <= 1) {
      alert(`At least one question is required for the ${formSubTab} Form.`);
      return;
    }
    const updated = activeQuestions.filter((_, i) => i !== index);
    setActiveQuestions(updated);
  };

  const handleMoveQuestion = (index, direction) => {
    const targetIdx = index + direction;
    if (targetIdx < 0 || targetIdx >= activeQuestions.length) return;
    const updated = [...activeQuestions];
    const temp = updated[index];
    updated[index] = updated[targetIdx];
    updated[targetIdx] = temp;
    setActiveQuestions(updated);
  };

  const handleAddChoice = (qIndex) => {
    const q = activeQuestions[qIndex];
    const newChoices = [...(q.choices || []), `Option ${(q.choices?.length || 0) + 1}`];
    handleUpdateQuestion(qIndex, "choices", newChoices);
  };

  const handleUpdateChoice = (qIndex, cIndex, text) => {
    const q = activeQuestions[qIndex];
    const newChoices = [...(q.choices || [])];
    newChoices[cIndex] = text;
    handleUpdateQuestion(qIndex, "choices", newChoices);
  };

  const handleRemoveChoice = (qIndex, cIndex) => {
    const q = activeQuestions[qIndex];
    const newChoices = (q.choices || []).filter((_, i) => i !== cIndex);
    handleUpdateQuestion(qIndex, "choices", newChoices);
  };

  // Atomic Save Form Version (Combines Attended & No-Show questions)
  const handleSaveFormVersion = async () => {
    setSaving(true);
    setError(null);
    setSuccessMsg(null);
    try {
      let sortOrderCounter = 1;

      const formattedAttended = attendedQuestions.map((q) => ({
        id: String(q.id).startsWith("temp_") || String(q.id).startsWith("def_") ? null : q.id,
        questionKey: q.questionKey || `att_q_${sortOrderCounter}`,
        promptText: q.promptText.trim(),
        questionType: Number(q.questionType),
        targetAudience: 1, // Strictly Attended
        optionsJson: q.choices && q.choices.length > 0 ? JSON.stringify(q.choices) : null,
        choices: q.choices,
        isRequired: Boolean(q.isRequired),
        sortOrder: sortOrderCounter++,
      }));

      const formattedNoShow = noShowQuestions.map((q) => ({
        id: String(q.id).startsWith("temp_") || String(q.id).startsWith("def_") ? null : q.id,
        questionKey: q.questionKey || `noshow_q_${sortOrderCounter}`,
        promptText: q.promptText.trim(),
        questionType: Number(q.questionType),
        targetAudience: 2, // Strictly No-Show
        optionsJson: q.choices && q.choices.length > 0 ? JSON.stringify(q.choices) : null,
        choices: q.choices,
        isRequired: Boolean(q.isRequired),
        sortOrder: sortOrderCounter++,
      }));

      const payload = {
        questions: [...formattedAttended, ...formattedNoShow],
      };

      const res = await adminApi.saveWorkshopFeedbackVersion(workshopId, payload);
      setConfig(res);
      setSuccessMsg("Attended & No-Show feedback forms saved successfully!");
      setTimeout(() => setSuccessMsg(null), 4000);
      await loadConfig();
    } catch (err) {
      setError(err?.message || "Failed to save feedback form versions.");
    } finally {
      setSaving(false);
    }
  };

  // Resend Feedback Handler (Recovery Tool)
  const handleResendFeedback = async () => {
    setResending(true);
    setResendResult(null);
    try {
      const payload = { audience: resendAudience };
      const res = await adminApi.resendWorkshopFeedback(workshopId, payload);
      setResendResult(res);
      await loadConfig();
    } catch (err) {
      alert(err?.message || "Failed to trigger feedback resend.");
    } finally {
      setResending(false);
    }
  };

  // Copy Feedback URL Helper
  const handleCopyFeedbackUrl = (url, bookingId) => {
    if (!url) return;
    navigator.clipboard.writeText(url);
    setCopiedTokenBookingId(bookingId);
    setTimeout(() => setCopiedTokenBookingId(null), 3000);
  };

  // Filtered Recipients for Ledger
  const filteredRecipients = useMemo(() => {
    const list = config?.Recipients || config?.recipients || [];
    if (!recipientSearch) return list;
    const s = recipientSearch.toLowerCase();
    return list.filter(
      (r) =>
        (r.AttendeeName || r.attendeeName || "").toLowerCase().includes(s) ||
        (r.BookingReference || r.bookingReference || "").toLowerCase().includes(s) ||
        (r.AttendeePhone || r.attendeePhone || "").includes(s)
    );
  }, [config, recipientSearch]);

  // Filtered Analytics Submissions based on analyticsAudienceFilter
  const filteredSubmissions = useMemo(() => {
    const subs = analytics?.Submissions || [];
    if (analyticsAudienceFilter === "All") return subs;
    return subs.filter(
      (s) => (s.AudienceType || "").toLowerCase() === analyticsAudienceFilter.toLowerCase()
    );
  }, [analytics, analyticsAudienceFilter]);

  // Filtered Question Analytics based on analyticsAudienceFilter
  const filteredQuestionAnalytics = useMemo(() => {
    const qas = analytics?.QuestionAnalytics || [];
    if (analyticsAudienceFilter === "All") return qas;
    return qas.filter((qa) => {
      const aud = Number(qa.TargetAudience);
      if (analyticsAudienceFilter === "Attended") return aud === 1 || aud === 3;
      if (analyticsAudienceFilter === "NoShow") return aud === 2 || aud === 3;
      return true;
    });
  }, [analytics, analyticsAudienceFilter]);

  const metrics = config?.Metrics || config?.metrics || {};
  const isFeedbackEnabled = Boolean(config?.isFeedbackEnabled ?? config?.IsFeedbackEnabled ?? true);
  const isLocked = Boolean(config?.IsLocked ?? config?.isLocked ?? false);
  const activeVersionNumber = config?.ActiveVersionNumber ?? config?.activeVersionNumber ?? 1;

  return (
    <div className="workshop-feedback-page">
      {/* Top Header */}
      <div className="fb-subpage-header">
        <div className="fb-header-info">
          <h1 className="fb-subpage-title">
            <span>⭐ Workshop Feedback &amp; Reviews</span>
            <span
              style={{
                fontSize: "12px",
                padding: "3px 10px",
                borderRadius: "20px",
                background: isLocked ? "#eff6ff" : isFeedbackEnabled ? "#ecfdf5" : "#f1f5f9",
                color: isLocked ? "#1d4ed8" : isFeedbackEnabled ? "#15803d" : "#64748b",
                border: isLocked ? "1px solid #bfdbfe" : isFeedbackEnabled ? "1px solid #86efac" : "1px solid #cbd5e1",
                fontWeight: "750",
              }}
            >
              {isLocked ? "🔒 Locked & Active" : isFeedbackEnabled ? "● Feedback Active (Editable)" : "○ Disabled"}
            </span>
          </h1>
          <p className="fb-subpage-subtitle">
            Dedicated dual-audience feedback management. Attended and No-Show participants automatically receive their tailored questionnaire via WhatsApp after the workshop finishes.
          </p>
        </div>

        <div className="fb-header-actions">
          <button
            type="button"
            className="fb-btn-secondary"
            onClick={() => setShowResendModal(true)}
            title="Manual recovery tool in case an attendee needs a link resent"
          >
            📲 Manual Resend
          </button>
          {activeTab === "builder" && (
            <button
              type="button"
              className="fb-btn-primary"
              onClick={handleSaveFormVersion}
              disabled={saving}
            >
              {saving ? "Saving Forms..." : "💾 Save / Publish Forms"}
            </button>
          )}
        </div>
      </div>

      {/* Lifecycle Notice Banner */}
      <div className="fb-lifecycle-banner">
        <div style={{ display: "flex", alignItems: "center", gap: "10px", flexWrap: "wrap" }}>
          <span style={{ fontSize: "18px" }}>⚡</span>
          <div>
            <strong style={{ color: "#0f172a", fontSize: "13.5px" }}>
              {isLocked
                ? `Form Version v${activeVersionNumber} is Locked & Active for this Workshop.`
                : `Draft Form (v${activeVersionNumber}) — Fully Editable before Workshop Starts.`}
            </strong>
            <div style={{ fontSize: "12px", color: "#64748b", marginTop: "2px" }}>
              {isLocked
                ? "Workshop has commenced. Historical form structure is frozen. New edits will create a new published version without altering past submissions."
                : "Forms automatically lock 15 minutes before the first session. Following workshop completion, the system automatically routes the Attended Form to checked-in attendees and the No-Show Form to absent registrants."}
            </div>
          </div>
        </div>
      </div>

      {/* Success / Error Banners */}
      {successMsg && (
        <div
          style={{
            background: "#ecfdf5",
            border: "1px solid #86efac",
            color: "#166534",
            padding: "12px 16px",
            borderRadius: "8px",
            marginBottom: "20px",
            fontSize: "13.5px",
            fontWeight: "600",
          }}
        >
          ✓ {successMsg}
        </div>
      )}

      {error && (
        <div
          style={{
            background: "#fef2f2",
            border: "1px solid #fecaca",
            color: "#991b1b",
            padding: "12px 16px",
            borderRadius: "8px",
            marginBottom: "20px",
            fontSize: "13.5px",
            fontWeight: "600",
          }}
        >
          ⚠ {error}
        </div>
      )}

      {/* KPI Banner */}
      <div className="fb-kpi-grid">
        <div className="fb-kpi-card kpi-rating">
          <div className="fb-kpi-label">Average Star Rating</div>
          <div className="fb-kpi-value">
            <span style={{ color: "#d97706" }}>★</span>
            <span>{metrics.AverageRating ?? metrics.averageRating ?? "0.0"}</span>
            <span className="fb-kpi-sub">/ 5.0</span>
          </div>
        </div>

        <div className="fb-kpi-card kpi-responses">
          <div className="fb-kpi-label">Submitted Reviews</div>
          <div className="fb-kpi-value">
            <span>{metrics.SubmittedCount ?? metrics.submittedCount ?? 0}</span>
            <span className="fb-kpi-sub">reviews</span>
          </div>
        </div>

        <div className="fb-kpi-card kpi-rate">
          <div className="fb-kpi-label">Response Rate</div>
          <div className="fb-kpi-value">
            <span>{metrics.ResponseRate ?? metrics.responseRate ?? 0}%</span>
            <span className="fb-kpi-sub">of eligible</span>
          </div>
        </div>

        <div className="fb-kpi-card kpi-attendees">
          <div className="fb-kpi-label">Eligible Attendees</div>
          <div className="fb-kpi-value" style={{ fontSize: "15px", display: "flex", gap: "10px", alignItems: "center" }}>
            <span style={{ color: "#1d4ed8" }}>✓ {metrics.EligibleAttendedCount ?? metrics.eligibleAttendedCount ?? 0} Attended</span>
            <span style={{ color: "#cbd5e1" }}>|</span>
            <span style={{ color: "#b91c1c" }}>✕ {metrics.EligibleNoShowCount ?? metrics.eligibleNoShowCount ?? 0} No-Show</span>
          </div>
        </div>
      </div>

      {/* Tab Navigation */}
      <div className="fb-tabs-nav">
        <button
          type="button"
          className={`fb-tab-btn ${activeTab === "builder" ? "active" : ""}`}
          onClick={() => setActiveTab("builder")}
        >
          📝 Feedback Form Builder
          <span className="fb-tab-badge">
            {attendedQuestions.length + noShowQuestions.length} questions
          </span>
        </button>

        <button
          type="button"
          className={`fb-tab-btn ${activeTab === "automation" ? "active" : ""}`}
          onClick={() => setActiveTab("automation")}
        >
          ⚙️ WhatsApp Automation
        </button>

        <button
          type="button"
          className={`fb-tab-btn ${activeTab === "analytics" ? "active" : ""}`}
          onClick={() => setActiveTab("analytics")}
        >
          📊 Responses &amp; Analytics
          <span className="fb-tab-badge">{metrics.SubmittedCount ?? metrics.submittedCount ?? 0}</span>
        </button>

        <button
          type="button"
          className={`fb-tab-btn ${activeTab === "recipients" ? "active" : ""}`}
          onClick={() => setActiveTab("recipients")}
        >
          📋 Attendee Ledger &amp; Tokens
          <span className="fb-tab-badge">{(config?.Recipients || config?.recipients || []).length}</span>
        </button>
      </div>

      {/* TAB 1: DUAL-AUDIENCE FORM BUILDER & LIVE MOBILE PREVIEW */}
      {activeTab === "builder" && (
        <div className="fb-builder-layout">
          {/* Left Column: Dual Form Designer */}
          <div>
            {/* Audience Form Selector Cards */}
            <div className="fb-audience-selector-row">
              <button
                type="button"
                className={`fb-audience-card ${formSubTab === "Attended" ? "selected-attended" : ""}`}
                onClick={() => setFormSubTab("Attended")}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "4px" }}>
                  <div style={{ fontSize: "14px", fontWeight: "800", color: "#1d4ed8" }}>
                    🙋 ATTENDED FORM
                  </div>
                  <span className="fb-audience-count-pill attended">
                    {attendedQuestions.length} Questions
                  </span>
                </div>
                <div style={{ fontSize: "12px", color: "#64748b" }}>
                  Sent automatically to attendees who checked in at the studio.
                </div>
              </button>

              <button
                type="button"
                className={`fb-audience-card ${formSubTab === "NoShow" ? "selected-noshow" : ""}`}
                onClick={() => setFormSubTab("NoShow")}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "4px" }}>
                  <div style={{ fontSize: "14px", fontWeight: "800", color: "#b91c1c" }}>
                    🚫 NO-SHOW FORM
                  </div>
                  <span className="fb-audience-count-pill noshow">
                    {noShowQuestions.length} Questions
                  </span>
                </div>
                <div style={{ fontSize: "12px", color: "#64748b" }}>
                  Sent automatically to registered attendees who missed the workshop.
                </div>
              </button>
            </div>

            {/* Questions Editor for Selected Form */}
            <div className="fb-card">
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "16px", flexWrap: "wrap", gap: "10px" }}>
                <div>
                  <h3 className="fb-card-title">
                    {formSubTab === "Attended" ? "🙋 Attended Questionnaire Designer" : "🚫 No-Show Questionnaire Designer"}
                  </h3>
                  <p className="fb-card-desc" style={{ margin: 0 }}>
                    {formSubTab === "Attended"
                      ? "Customize questions regarding teaching quality, choreography, studio venue, and overall experience."
                      : "Customize questions exploring reasons for absence, booking satisfaction, and future workshop interest."}
                  </p>
                </div>
                <div style={{ fontSize: "12px", background: "#f1f5f9", padding: "4px 10px", borderRadius: "6px", color: "#475569", fontWeight: "700" }}>
                  Active Version: v{activeVersionNumber}
                </div>
              </div>

              {/* Questions List */}
              <div className="fb-questions-container">
                {activeQuestions.map((q, idx) => (
                  <div key={q.id || idx} className="fb-question-card">
                    <div className="fb-question-top">
                      <div className="fb-question-number-pill">
                        <span>#{idx + 1}</span>
                        <span>
                          {q.questionType === 1
                            ? "⭐ Star Rating"
                            : q.questionType === 2
                            ? "🔘 Single Choice"
                            : q.questionType === 4
                            ? "☑ Multiple Choice"
                            : "📝 Text Response"}
                        </span>
                      </div>

                      <div className="fb-question-controls">
                        <button
                          type="button"
                          className="fb-ctrl-btn"
                          onClick={() => handleMoveQuestion(idx, -1)}
                          disabled={idx === 0}
                          title="Move Up"
                        >
                          ▲
                        </button>
                        <button
                          type="button"
                          className="fb-ctrl-btn"
                          onClick={() => handleMoveQuestion(idx, 1)}
                          disabled={idx === activeQuestions.length - 1}
                          title="Move Down"
                        >
                          ▼
                        </button>
                        <button
                          type="button"
                          className="fb-ctrl-btn btn-delete"
                          onClick={() => handleDeleteQuestion(idx)}
                          title="Delete Question"
                        >
                          ✕
                        </button>
                      </div>
                    </div>

                    {/* Question Prompt */}
                    <div className="fb-form-group">
                      <label className="fb-form-label">Question Prompt</label>
                      <input
                        type="text"
                        className="fb-input"
                        value={q.promptText}
                        onChange={(e) => handleUpdateQuestion(idx, "promptText", e.target.value)}
                        placeholder="e.g. How was your overall experience?"
                      />
                    </div>

                    {/* Response Type */}
                    <div className="fb-form-group">
                      <label className="fb-form-label">Response Type</label>
                      <select
                        className="fb-select"
                        value={q.questionType}
                        onChange={(e) => handleUpdateQuestion(idx, "questionType", Number(e.target.value))}
                      >
                        <option value={1}>⭐ Rating (1–5 Stars)</option>
                        <option value={3}>📝 Text Response (Open Ended)</option>
                        <option value={2}>🔘 Single Choice (Radio)</option>
                        <option value={4}>☑ Multiple Choice (Checkboxes)</option>
                      </select>
                    </div>

                    {/* Choices Editor for Single/Multi Choice */}
                    {(Number(q.questionType) === 2 || Number(q.questionType) === 4) && (
                      <div className="fb-form-group" style={{ background: "#f8fafc", padding: "12px", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
                        <label className="fb-form-label">Answer Choices / Options</label>
                        <div className="fb-choices-list">
                          {(q.choices || []).map((c, cIdx) => (
                            <div key={cIdx} className="fb-choice-row">
                              <input
                                type="text"
                                className="fb-input"
                                value={c}
                                onChange={(e) => handleUpdateChoice(idx, cIdx, e.target.value)}
                                placeholder={`Option ${cIdx + 1}`}
                              />
                              <button
                                type="button"
                                className="fb-choice-remove"
                                onClick={() => handleRemoveChoice(idx, cIdx)}
                                title="Remove Choice"
                              >
                                ✕
                              </button>
                            </div>
                          ))}
                          <button
                            type="button"
                            className="fb-add-choice-btn"
                            onClick={() => handleAddChoice(idx)}
                          >
                            + Add Option
                          </button>
                        </div>
                      </div>
                    )}

                    {/* Required Toggle */}
                    <div style={{ display: "flex", alignItems: "center", gap: "8px", marginTop: "10px" }}>
                      <label style={{ display: "inline-flex", alignItems: "center", gap: "6px", fontSize: "12.5px", color: "#475569", cursor: "pointer" }}>
                        <input
                          type="checkbox"
                          checked={q.isRequired}
                          onChange={(e) => handleUpdateQuestion(idx, "isRequired", e.target.checked)}
                          style={{ accentColor: "#ff5500" }}
                        />
                        <span>Required Question</span>
                      </label>
                    </div>
                  </div>
                ))}

                {/* Add Question Button */}
                <div className="fb-add-question-box" onClick={handleAddQuestion}>
                  <div style={{ fontSize: "16px", fontWeight: "800", marginBottom: "3px" }}>
                    + Add Question to {formSubTab} Form
                  </div>
                  <div style={{ fontSize: "12px", color: "#64748b" }}>
                    Rating, open text, or single/multiple choice question
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Right Column: Live Mobile Attendee Preview */}
          <div className="fb-preview-sticky">
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "10px" }}>
              <span style={{ fontSize: "13px", fontWeight: "750", color: "#334155" }}>
                📱 Live {formSubTab} Preview
              </span>
              <span
                style={{
                  fontSize: "11px",
                  fontWeight: "700",
                  padding: "2px 8px",
                  borderRadius: "12px",
                  background: formSubTab === "Attended" ? "#dbeafe" : "#fee2e2",
                  color: formSubTab === "Attended" ? "#1d4ed8" : "#b91c1c",
                }}
              >
                {formSubTab === "Attended" ? "🙋 Attended View" : "🚫 No-Show View"}
              </span>
            </div>

            <div className="fb-phone-frame">
              <div className="fb-phone-notch"></div>
              <div className="fb-phone-screen">
                <div className="fb-preview-banner">
                  <div className="fb-preview-studio-tag">ETHOS DANCE STUDIO</div>
                  <h4 className="fb-preview-ws-title">{workshop?.Title || workshop?.title || "Masterclass Workshop"}</h4>
                  <div className="fb-preview-ws-meta">
                    {formSubTab === "Attended" ? "How was your experience?" : "We missed you!"}
                  </div>
                </div>

                <div className="fb-preview-questions">
                  {activeQuestions.map((q, idx) => (
                    <div key={q.id || idx} className="fb-mock-q">
                      <div className="fb-mock-q-title">
                        {q.promptText || "Untitled Question"}
                        {q.isRequired && <span style={{ color: "#ef4444", marginLeft: "4px" }}>*</span>}
                      </div>

                      {/* Type 1: Star Rating */}
                      {Number(q.questionType) === 1 && (
                        <div className="fb-mock-stars">
                          <span>★</span>
                          <span>★</span>
                          <span>★</span>
                          <span>★</span>
                          <span>★</span>
                        </div>
                      )}

                      {/* Type 3: Text */}
                      {Number(q.questionType) === 3 && (
                        <textarea
                          className="fb-mock-textarea"
                          placeholder="Type your response here..."
                          disabled
                        />
                      )}

                      {/* Type 2: Single Choice */}
                      {Number(q.questionType) === 2 && (
                        <div className="fb-mock-radio-group">
                          {(q.choices || []).map((c, cIdx) => (
                            <div key={cIdx} className="fb-mock-radio-item">
                              <input type="radio" name={`mock_radio_${idx}`} disabled />
                              <span>{c}</span>
                            </div>
                          ))}
                        </div>
                      )}

                      {/* Type 4: Multiple Choice */}
                      {Number(q.questionType) === 4 && (
                        <div className="fb-mock-radio-group">
                          {(q.choices || []).map((c, cIdx) => (
                            <div key={cIdx} className="fb-mock-radio-item">
                              <input type="checkbox" disabled />
                              <span>{c}</span>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  ))}

                  <div className="fb-mock-submit-btn">Submit Feedback</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: WHATSAPP AUTOMATION */}
      {activeTab === "automation" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "24px" }}>
          {/* Automation Status Card */}
          <div className="fb-card">
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "12px", marginBottom: "16px" }}>
              <div>
                <h3 className="fb-card-title">Automated MSG91 WhatsApp Feedback Campaign</h3>
                <p className="fb-card-desc" style={{ margin: 0 }}>
                  Automatically dispatches verified post-workshop feedback requests to attendees via the durable WhatsApp Outbox.
                </p>
              </div>
              <span
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: "6px",
                  background: isFeedbackEnabled ? "#ecfdf5" : "#f1f5f9",
                  color: isFeedbackEnabled ? "#15803d" : "#64748b",
                  border: isFeedbackEnabled ? "1px solid #86efac" : "1px solid #cbd5e1",
                  padding: "6px 12px",
                  borderRadius: "20px",
                  fontSize: "12.5px",
                  fontWeight: "750",
                }}
              >
                <span style={{ width: "8px", height: "8px", borderRadius: "50%", background: isFeedbackEnabled ? "#22c55e" : "#94a3b8" }}></span>
                {isFeedbackEnabled ? "Automation Active" : "Automation Paused"}
              </span>
            </div>

            {/* Campaign Parameters */}
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "14px", marginTop: "16px" }}>
              <div style={{ background: "#f8fafc", padding: "14px 16px", borderRadius: "10px", border: "1px solid #e2e8f0" }}>
                <div style={{ fontSize: "11px", fontWeight: "700", color: "#64748b", textTransform: "uppercase", letterSpacing: "0.05em" }}>Attended Template</div>
                <div style={{ fontSize: "14px", fontWeight: "750", color: "#0f172a", marginTop: "4px", fontFamily: "monospace" }}>ethos_feedback_attended</div>
                <div style={{ fontSize: "11.5px", color: "#15803d", marginTop: "2px", fontWeight: "600" }}>● Meta Approved</div>
              </div>

              <div style={{ background: "#f8fafc", padding: "14px 16px", borderRadius: "10px", border: "1px solid #e2e8f0" }}>
                <div style={{ fontSize: "11px", fontWeight: "700", color: "#64748b", textTransform: "uppercase", letterSpacing: "0.05em" }}>No-Show Template</div>
                <div style={{ fontSize: "14px", fontWeight: "750", color: "#0f172a", marginTop: "4px", fontFamily: "monospace" }}>ethos_feedback_noshow</div>
                <div style={{ fontSize: "11.5px", color: "#15803d", marginTop: "2px", fontWeight: "600" }}>● Meta Approved</div>
              </div>

              <div style={{ background: "#f8fafc", padding: "14px 16px", borderRadius: "10px", border: "1px solid #e2e8f0" }}>
                <div style={{ fontSize: "11px", fontWeight: "700", color: "#64748b", textTransform: "uppercase", letterSpacing: "0.05em" }}>Trigger Condition</div>
                <div style={{ fontSize: "14px", fontWeight: "750", color: "#0f172a", marginTop: "4px" }}>Workshop Concludes</div>
                <div style={{ fontSize: "11.5px", color: "#64748b", marginTop: "2px" }}>Evaluated via session end-time</div>
              </div>

              <div style={{ background: "#f8fafc", padding: "14px 16px", borderRadius: "10px", border: "1px solid #e2e8f0" }}>
                <div style={{ fontSize: "11px", fontWeight: "700", color: "#64748b", textTransform: "uppercase", letterSpacing: "0.05em" }}>Automated Dispatch</div>
                <div style={{ fontSize: "14px", fontWeight: "750", color: "#0f172a", marginTop: "4px" }}>Instant Background Trigger</div>
                <div style={{ fontSize: "11.5px", color: "#64748b", marginTop: "2px" }}>Audience resolved automatically</div>
              </div>
            </div>
          </div>

          {/* Dynamic Template Variables Table */}
          <div className="fb-card">
            <h3 className="fb-card-title">Dynamic Template Parameters &amp; Mapping</h3>
            <p className="fb-card-desc">
              Values injected into the Meta-approved WhatsApp template at dispatch time.
            </p>

            <div className="fb-table-container">
              <table className="fb-table">
                <thead>
                  <tr>
                    <th>Placeholder</th>
                    <th>Source Field</th>
                    <th>Description</th>
                    <th>Sample Value</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td style={{ fontFamily: "monospace", fontWeight: "700", color: "#2563eb" }}>{"{{1}}"} (Attendee Name)</td>
                    <td>booking.GuestName / user.FullName</td>
                    <td style={{ color: "#64748b" }}>Recipient attendee's full name</td>
                    <td style={{ fontWeight: "600" }}>Rahul</td>
                  </tr>
                  <tr>
                    <td style={{ fontFamily: "monospace", fontWeight: "700", color: "#2563eb" }}>{"{{2}}"} (Workshop Title)</td>
                    <td>workshop.Title</td>
                    <td style={{ color: "#64748b" }}>Title of the attended workshop</td>
                    <td style={{ fontWeight: "600" }}>{workshop?.Title || workshop?.title || "Naari Naari By Sreekanth"}</td>
                  </tr>
                  <tr>
                    <td style={{ fontFamily: "monospace", fontWeight: "700", color: "#7c3aed" }}>Button Payload (Path)</td>
                    <td>tokenEntity.RawToken</td>
                    <td style={{ color: "#64748b" }}>Cryptographic HMAC token for authenticated 1-click feedback</td>
                    <td style={{ fontFamily: "monospace", fontSize: "12px", color: "#475569" }}>https://ethosdancestudio.com/feedback/workshop/&lt;secure-token&gt;</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          {/* Message Previews: Spec vs Rendered */}
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))", gap: "20px" }}>
            <div className="fb-card" style={{ margin: 0 }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
                <h3 className="fb-card-title" style={{ margin: 0 }}>Template Specification (Raw)</h3>
                <span style={{ fontSize: "11px", background: "#f1f5f9", color: "#475569", padding: "2px 8px", borderRadius: "4px", fontWeight: "700" }}>
                  MSG91 Payload
                </span>
              </div>
              <div style={{ background: "#f8fafc", border: "1px solid #e2e8f0", borderRadius: "10px", padding: "16px", color: "#334155", fontSize: "13px", lineHeight: "1.6", fontFamily: "monospace" }}>
                <p style={{ margin: "0 0 10px 0" }}>Hey {"{{1}}"}! 👋</p>
                <p style={{ margin: "0 0 10px 0" }}>Thank you for dancing with us at {"{{2}}"}!</p>
                <p style={{ margin: "0 0 10px 0" }}>We would love to hear your thoughts and experience to help us keep improving. It only takes 30 seconds:</p>
                <div style={{ margin: "0 0 10px 0", padding: "8px 12px", background: "#e2e8f0", borderRadius: "6px", color: "#1e293b", fontWeight: "700" }}>
                  👉 [Button] Give Feedback &rarr; https://ethosdancestudio.com/feedback/workshop/&lt;token&gt;
                </div>
                <p style={{ margin: 0 }}>
                  See you on the dance floor soon! ✨<br />
                  Team ETHOS Dance Studio
                </p>
              </div>
            </div>

            <div className="fb-card" style={{ margin: 0 }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
                <h3 className="fb-card-title" style={{ margin: 0 }}>Attendee Experience (Live Render)</h3>
                <span style={{ fontSize: "11px", background: "#dcfce7", color: "#166534", padding: "2px 8px", borderRadius: "4px", fontWeight: "700" }}>
                  What Attendee Receives
                </span>
              </div>
              <div style={{ background: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: "10px", padding: "18px", color: "#166534", fontSize: "13px", lineHeight: "1.6" }}>
                <p style={{ margin: "0 0 10px 0" }}><strong>Hey Rahul! 👋</strong></p>
                <p style={{ margin: "0 0 10px 0" }}>
                  Thank you for dancing with us at <strong>{workshop?.Title || workshop?.title || "Naari Naari By Sreekanth"}</strong>!
                </p>
                <p style={{ margin: "0 0 10px 0" }}>
                  We would love to hear your thoughts and experience to help us keep improving. It only takes 30 seconds:
                </p>
                <div style={{ margin: "0 0 12px 0", padding: "10px 14px", background: "#22c55e", color: "#ffffff", borderRadius: "8px", textAlign: "center", fontWeight: "750", cursor: "default", boxShadow: "0 2px 6px rgba(34, 197, 94, 0.3)" }}>
                  ⭐ Give Feedback
                </div>
                <p style={{ margin: 0, fontSize: "12px", color: "#15803d" }}>
                  See you on the dance floor soon! ✨<br />
                  <strong>Team ETHOS Dance Studio</strong>
                </p>
              </div>
            </div>
          </div>

          {/* Regulatory Notice */}
          <div style={{ background: "#f8fafc", border: "1px solid #e2e8f0", borderRadius: "10px", padding: "16px 20px", display: "flex", alignItems: "flex-start", gap: "12px" }}>
            <span style={{ fontSize: "20px" }}>🔒</span>
            <div style={{ fontSize: "12.5px", color: "#475569", lineHeight: "1.5" }}>
              <strong style={{ color: "#1e293b" }}>Meta WhatsApp Policy Notice:</strong> WhatsApp message wording, language templates, and call-to-action button bindings are approved via Meta Business Manager. Modifications to the questionnaire attendees see after tapping the link are controlled right here in the <strong>Feedback Form Builder</strong> tab.
            </div>
          </div>
        </div>
      )}

      {/* TAB 3: RESPONSES & ANALYTICS */}
      {activeTab === "analytics" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "24px" }}>
          {/* Audience Filter Pills for Analytics */}
          <div className="fb-analytics-filter-row">
            <span style={{ fontSize: "13px", fontWeight: "700", color: "#475569" }}>Audience Segment:</span>
            <div className="fb-filter-pills">
              <button
                type="button"
                className={`fb-filter-pill ${analyticsAudienceFilter === "All" ? "active" : ""}`}
                onClick={() => setAnalyticsAudienceFilter("All")}
              >
                All Responses ({analytics?.Submissions?.length || 0})
              </button>
              <button
                type="button"
                className={`fb-filter-pill attended ${analyticsAudienceFilter === "Attended" ? "active" : ""}`}
                onClick={() => setAnalyticsAudienceFilter("Attended")}
              >
                🙋 Attended ({analytics?.Submissions?.filter((s) => s.AudienceType === "Attended").length || 0})
              </button>
              <button
                type="button"
                className={`fb-filter-pill noshow ${analyticsAudienceFilter === "NoShow" ? "active" : ""}`}
                onClick={() => setAnalyticsAudienceFilter("NoShow")}
              >
                🚫 No-Show ({analytics?.Submissions?.filter((s) => s.AudienceType === "NoShow").length || 0})
              </button>
            </div>
          </div>

          {/* Rating Breakdown & Summary */}
          <div className="fb-card">
            <h3 className="fb-card-title">Star Ratings Distribution</h3>
            <p className="fb-card-desc">Comprehensive breakdown of participant feedback ratings.</p>

            <div className="fb-rating-bars">
              {[5, 4, 3, 2, 1].map((stars) => {
                const countKey = stars === 5 ? "FiveStars" : stars === 4 ? "FourStars" : stars === 3 ? "ThreeStars" : stars === 2 ? "TwoStars" : "OneStar";
                const altKey = stars === 5 ? "fiveStars" : stars === 4 ? "fourStars" : stars === 3 ? "threeStars" : stars === 2 ? "twoStars" : "oneStar";
                const count = metrics[countKey] ?? metrics[altKey] ?? 0;
                const total = metrics.SubmittedCount ?? metrics.submittedCount ?? 0;
                const pct = total > 0 ? Math.round((count / total) * 100) : 0;

                return (
                  <div key={stars} className="fb-rating-row">
                    <div className="fb-rating-star-label">
                      <span>{stars}</span>
                      <span style={{ color: "#d97706" }}>★</span>
                    </div>
                    <div className="fb-progress-track">
                      <div className="fb-progress-fill" style={{ width: `${pct}%` }}></div>
                    </div>
                    <div className="fb-rating-count">{count}</div>
                  </div>
                );
              })}
            </div>
          </div>

          {/* Question-Level Analytics */}
          {filteredQuestionAnalytics.length > 0 && (
            <div className="fb-card">
              <h3 className="fb-card-title">
                Question-Level Breakdown ({analyticsAudienceFilter === "All" ? "All Questions" : `${analyticsAudienceFilter} Questions`})
              </h3>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))", gap: "16px", marginTop: "16px" }}>
                {filteredQuestionAnalytics.map((qa, idx) => (
                  <div key={qa.QuestionId || idx} style={{ background: "#f8fafc", padding: "16px", borderRadius: "10px", border: "1px solid #e2e8f0" }}>
                    <div style={{ fontSize: "13px", fontWeight: "750", color: "#0f172a", marginBottom: "8px" }}>
                      #{idx + 1}. {qa.PromptText}
                    </div>
                    {qa.AverageRating != null && (
                      <div style={{ fontSize: "14px", fontWeight: "800", color: "#d97706" }}>
                        ★ {qa.AverageRating} / 5.0 avg score ({qa.TotalAnswers} responses)
                      </div>
                    )}
                    {qa.ChoiceCounts && Object.keys(qa.ChoiceCounts).length > 0 && (
                      <div style={{ display: "flex", flexDirection: "column", gap: "6px", marginTop: "8px" }}>
                        {Object.entries(qa.ChoiceCounts).map(([opt, cnt]) => (
                          <div key={opt} style={{ display: "flex", justifyContent: "space-between", fontSize: "12px", color: "#334155" }}>
                            <span>{opt}</span>
                            <strong>{cnt} ({Math.round((cnt / (qa.TotalAnswers || 1)) * 100)}%)</strong>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Individual Submissions Feed */}
          <div className="fb-card">
            <h3 className="fb-card-title">
              Individual Participant Reviews ({filteredSubmissions.length})
            </h3>
            <p className="fb-card-desc">Verified feedback submissions segmented by audience.</p>

            {filteredSubmissions.length === 0 ? (
              <div style={{ textAlign: "center", padding: "30px 20px", color: "#64748b" }}>
                💬 No written reviews found for the selected audience filter.
              </div>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
                {filteredSubmissions.map((sub) => (
                  <div key={sub.FeedbackId} style={{ background: "#ffffff", border: "1px solid #e2e8f0", borderRadius: "10px", padding: "16px" }}>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "8px" }}>
                      <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                        <span style={{ fontWeight: "750", color: "#0f172a", fontSize: "13.5px" }}>{sub.StudentNameMasked}</span>
                        <span className={sub.AudienceType === "Attended" ? "fb-pill-attended" : "fb-pill-noshow"}>
                          {sub.AudienceType}
                        </span>
                      </div>
                      <div style={{ color: "#d97706", fontSize: "15px" }}>
                        {"★".repeat(sub.Rating)}
                        <span style={{ color: "#cbd5e1" }}>{"★".repeat(Math.max(0, 5 - sub.Rating))}</span>
                      </div>
                    </div>

                    {sub.Comment && (
                      <p style={{ fontSize: "13px", color: "#334155", margin: "0 0 10px 0", lineHeight: "1.5" }}>
                        "{sub.Comment}"
                      </p>
                    )}

                    {sub.Answers && sub.Answers.length > 0 && (
                      <div style={{ display: "flex", flexWrap: "wrap", gap: "8px", marginTop: "8px" }}>
                        {sub.Answers.map((ans, aIdx) => (
                          <div key={aIdx} style={{ fontSize: "11.5px", background: "#f1f5f9", padding: "4px 8px", borderRadius: "6px", color: "#475569" }}>
                            <strong>{ans.PromptText}:</strong> {ans.TextValue || (ans.NumericValue ? `★ ${ans.NumericValue}` : "N/A")}
                          </div>
                        ))}
                      </div>
                    )}

                    <div style={{ fontSize: "11px", color: "#94a3b8", marginTop: "8px" }}>{sub.FormattedDate}</div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {/* TAB 4: RECIPIENT LEDGER & SECURE TOKENS */}
      {activeTab === "recipients" && (
        <div className="fb-card">
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "16px", flexWrap: "wrap", gap: "12px" }}>
            <div>
              <h3 className="fb-card-title">Attendee Ledger &amp; Token Dispatch Status</h3>
              <p className="fb-card-desc" style={{ margin: 0 }}>
                Verified attendee list, attendance classification, outbox status, and authenticated 1-click feedback links.
              </p>
            </div>
            <input
              type="text"
              placeholder="Search by name, ref, or phone..."
              value={recipientSearch}
              onChange={(e) => setRecipientSearch(e.target.value)}
              className="fb-input"
              style={{ maxWidth: "280px" }}
            />
          </div>

          <div className="fb-table-container">
            <table className="fb-table">
              <thead>
                <tr>
                  <th>Booking Ref</th>
                  <th>Attendee Name</th>
                  <th>Attendance</th>
                  <th>Delivery Status</th>
                  <th>Submitted Rating</th>
                  <th>Feedback Link</th>
                </tr>
              </thead>
              <tbody>
                {filteredRecipients.map((r) => {
                  const bId = r.BookingId || r.bookingId;
                  const ref = r.BookingReference || r.bookingReference;
                  const name = r.AttendeeName || r.attendeeName;
                  const aud = r.AudienceType || r.audienceType;
                  const status = (r.DeliveryStatus || r.deliveryStatus || "NotQueued").toLowerCase();
                  const rating = r.SubmittedRating ?? r.submittedRating;
                  const url = r.FeedbackUrl || r.feedbackUrl;

                  return (
                    <tr key={bId}>
                      <td style={{ fontFamily: "monospace", fontWeight: "700" }}>{ref}</td>
                      <td style={{ fontWeight: "650" }}>{name}</td>
                      <td>
                        <span className={aud === "Attended" ? "fb-pill-attended" : "fb-pill-noshow"}>
                          {aud}
                        </span>
                      </td>
                      <td>
                        <span className={`fb-pill-status ${status}`}>
                          {status === "submitted" ? "✓ Submitted" : status === "sent" ? "● Sent" : status === "queued" ? "⏳ Queued" : "○ Not Queued"}
                        </span>
                      </td>
                      <td>
                        {rating ? (
                          <span style={{ color: "#d97706", fontWeight: "750" }}>★ {rating} / 5</span>
                        ) : (
                          <span style={{ color: "#94a3b8" }}>—</span>
                        )}
                      </td>
                      <td>
                        {url ? (
                          <button
                            type="button"
                            className="fb-btn-secondary"
                            style={{ padding: "4px 8px", fontSize: "11.5px" }}
                            onClick={() => handleCopyFeedbackUrl(url, bId)}
                          >
                            {copiedTokenBookingId === bId ? "✓ Copied Link" : "📋 Copy Link"}
                          </button>
                        ) : (
                          <span style={{ color: "#94a3b8" }}>—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* RESEND FEEDBACK MODAL (EXCEPTIONAL RECOVERY TOOL) */}
      {showResendModal && (
        <div className="fb-modal-overlay">
          <div className="fb-modal-card">
            <div className="fb-modal-header">
              <h3>📲 Manual Feedback Resend Tool</h3>
              <button
                type="button"
                style={{ background: "none", border: "none", fontSize: "18px", cursor: "pointer", color: "#64748b" }}
                onClick={() => {
                  setShowResendModal(false);
                  setResendResult(null);
                }}
              >
                ✕
              </button>
            </div>

            <div className="fb-modal-body">
              {resendResult ? (
                <div style={{ background: "#ecfdf5", border: "1px solid #86efac", color: "#166534", padding: "16px", borderRadius: "10px", textAlign: "center" }}>
                  <div style={{ fontSize: "24px", marginBottom: "8px" }}>✓</div>
                  <h4 style={{ margin: "0 0 6px 0", fontSize: "16px", fontWeight: "800" }}>Messages Queued Successfully</h4>
                  <p style={{ margin: 0, fontSize: "13px" }}>{resendResult.Message || resendResult.message}</p>
                </div>
              ) : (
                <>
                  <div style={{ background: "#fff7ed", border: "1px solid #fed7aa", padding: "12px 14px", borderRadius: "8px", marginBottom: "16px", fontSize: "12.5px", color: "#9a3412", lineHeight: "1.5" }}>
                    <strong>Recovery Notice:</strong> Automated WhatsApp feedback messages are dispatched on schedule upon workshop completion. Use this tool only if an attendee did not receive their message or requested a link resend.
                  </div>

                  <div className="fb-form-group">
                    <label className="fb-form-label">Target Audience</label>
                    <div style={{ display: "flex", flexDirection: "column", gap: "10px" }}>
                      <label style={{ display: "flex", alignItems: "center", gap: "10px", padding: "10px 14px", border: "1px solid #cbd5e1", borderRadius: "8px", cursor: "pointer", background: resendAudience === "All" ? "#eff6ff" : "#ffffff", borderColor: resendAudience === "All" ? "#2563eb" : "#cbd5e1" }}>
                        <input
                          type="radio"
                          name="resend_aud"
                          checked={resendAudience === "All"}
                          onChange={() => setResendAudience("All")}
                        />
                        <div>
                          <div style={{ fontWeight: "750", fontSize: "13.5px", color: "#0f172a" }}>All Eligible Attendees</div>
                          <div style={{ fontSize: "12px", color: "#64748b" }}>Attended + No-Show attendees without submitted feedback</div>
                        </div>
                      </label>

                      <label style={{ display: "flex", alignItems: "center", gap: "10px", padding: "10px 14px", border: "1px solid #cbd5e1", borderRadius: "8px", cursor: "pointer", background: resendAudience === "Attended" ? "#eff6ff" : "#ffffff", borderColor: resendAudience === "Attended" ? "#2563eb" : "#cbd5e1" }}>
                        <input
                          type="radio"
                          name="resend_aud"
                          checked={resendAudience === "Attended"}
                          onChange={() => setResendAudience("Attended")}
                        />
                        <div>
                          <div style={{ fontWeight: "750", fontSize: "13.5px", color: "#0f172a" }}>Attended Only</div>
                          <div style={{ fontSize: "12px", color: "#64748b" }}>Attendees checked in at the studio</div>
                        </div>
                      </label>

                      <label style={{ display: "flex", alignItems: "center", gap: "10px", padding: "10px 14px", border: "1px solid #cbd5e1", borderRadius: "8px", cursor: "pointer", background: resendAudience === "NoShow" ? "#eff6ff" : "#ffffff", borderColor: resendAudience === "NoShow" ? "#2563eb" : "#cbd5e1" }}>
                        <input
                          type="radio"
                          name="resend_aud"
                          checked={resendAudience === "NoShow"}
                          onChange={() => setResendAudience("NoShow")}
                        />
                        <div>
                          <div style={{ fontWeight: "750", fontSize: "13.5px", color: "#0f172a" }}>No-Show Only</div>
                          <div style={{ fontSize: "12px", color: "#64748b" }}>Registered attendees who did not check in</div>
                        </div>
                      </label>
                    </div>
                  </div>
                </>
              )}
            </div>

            <div className="fb-modal-footer">
              <button
                type="button"
                className="fb-btn-secondary"
                onClick={() => {
                  setShowResendModal(false);
                  setResendResult(null);
                }}
              >
                Close
              </button>
              {!resendResult && (
                <button
                  type="button"
                  className="fb-btn-primary"
                  onClick={handleResendFeedback}
                  disabled={resending}
                >
                  {resending ? "Queuing..." : "Confirm & Send WhatsApp"}
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
