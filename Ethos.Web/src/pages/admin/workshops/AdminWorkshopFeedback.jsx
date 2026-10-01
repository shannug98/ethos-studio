import React, { useState, useEffect, useCallback, useMemo } from "react";
import { useOutletContext } from "react-router-dom";
import { adminApi } from "../../../services/adminApi";
import "./AdminWorkshopFeedback.css";

export default function AdminWorkshopFeedback() {
  const { workshop } = useOutletContext();
  const workshopId = workshop.Id || workshop.id;

  // Active Tab: "builder" | "automation" | "analytics" | "recipients"
  const [activeTab, setActiveTab] = useState("builder");

  // Config & Form State
  const [config, setConfig] = useState(null);
  const [analytics, setAnalytics] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [successMsg, setSuccessMsg] = useState(null);

  // Editable Form Questions
  const [questions, setQuestions] = useState([]);
  const [previewAudience, setPreviewAudience] = useState("Attended"); // "Attended" | "NoShow"

  // Resend Feedback Modal
  const [showResendModal, setShowResendModal] = useState(false);
  const [resendAudience, setResendAudience] = useState("All"); // "All" | "Attended" | "NoShow"
  const [resending, setResending] = useState(false);
  const [resendResult, setResendResult] = useState(null);

  // Search filter for recipient ledger
  const [recipientSearch, setRecipientSearch] = useState("");
  const [copiedTokenBookingId, setCopiedTokenBookingId] = useState(null);

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

      if (configData?.ActiveQuestions || configData?.activeQuestions) {
        const rawQs = configData.ActiveQuestions || configData.activeQuestions;
        setQuestions(
          rawQs.map((q) => ({
            id: q.Id || q.id,
            questionKey: q.QuestionKey || q.questionKey || "",
            promptText: q.PromptText || q.promptText || "",
            questionType: q.QuestionType || q.questionType || 1,
            targetAudience: q.TargetAudience || q.targetAudience || 3,
            optionsJson: q.OptionsJson || q.optionsJson || "",
            choices: q.Choices || q.choices || ["Option 1", "Option 2"],
            isRequired: q.IsRequired ?? q.isRequired ?? true,
            sortOrder: q.SortOrder || q.sortOrder || 1,
          }))
        );
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

  // Question Management Handlers
  const handleAddQuestion = () => {
    const newQ = {
      id: "temp_" + Date.now(),
      questionKey: `q_${questions.length + 1}_${Math.random().toString(36).substring(2, 6)}`,
      promptText: "New Feedback Question",
      questionType: 1, // Rating1To5
      targetAudience: 3, // Both
      optionsJson: "",
      choices: ["Choice 1", "Choice 2"],
      isRequired: true,
      sortOrder: questions.length + 1,
    };
    setQuestions([...questions, newQ]);
  };

  const handleUpdateQuestion = (index, field, value) => {
    const updated = [...questions];
    updated[index] = { ...updated[index], [field]: value };
    setQuestions(updated);
  };

  const handleDeleteQuestion = (index) => {
    if (questions.length <= 1) {
      alert("At least one feedback question is required.");
      return;
    }
    const updated = questions.filter((_, i) => i !== index);
    setQuestions(updated);
  };

  const handleMoveQuestion = (index, direction) => {
    const targetIdx = index + direction;
    if (targetIdx < 0 || targetIdx >= questions.length) return;
    const updated = [...questions];
    const temp = updated[index];
    updated[index] = updated[targetIdx];
    updated[targetIdx] = temp;
    setQuestions(updated);
  };

  const handleAddChoice = (qIndex) => {
    const q = questions[qIndex];
    const newChoices = [...(q.choices || []), `Option ${(q.choices?.length || 0) + 1}`];
    handleUpdateQuestion(qIndex, "choices", newChoices);
  };

  const handleUpdateChoice = (qIndex, cIndex, text) => {
    const q = questions[qIndex];
    const newChoices = [...(q.choices || [])];
    newChoices[cIndex] = text;
    handleUpdateQuestion(qIndex, "choices", newChoices);
  };

  const handleRemoveChoice = (qIndex, cIndex) => {
    const q = questions[qIndex];
    const newChoices = (q.choices || []).filter((_, i) => i !== cIndex);
    handleUpdateQuestion(qIndex, "choices", newChoices);
  };

  // Save Form Version
  const handleSaveFormVersion = async () => {
    setSaving(true);
    setError(null);
    setSuccessMsg(null);
    try {
      const payload = {
        questions: questions.map((q, idx) => ({
          id: String(q.id).startsWith("temp_") ? null : q.id,
          questionKey: q.questionKey || `q_${idx + 1}`,
          promptText: q.promptText,
          questionType: Number(q.questionType),
          targetAudience: Number(q.targetAudience),
          optionsJson: q.choices && q.choices.length > 0 ? JSON.stringify(q.choices) : null,
          choices: q.choices,
          isRequired: Boolean(q.isRequired),
          sortOrder: idx + 1,
        })),
      };

      const res = await adminApi.saveWorkshopFeedbackVersion(workshopId, payload);
      setConfig(res);
      setSuccessMsg("Feedback form version saved successfully!");
      setTimeout(() => setSuccessMsg(null), 4000);
    } catch (err) {
      setError(err?.message || "Failed to save feedback form version.");
    } finally {
      setSaving(false);
    }
  };

  // Resend Feedback Handler
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

  // Filtered Questions for Mobile Preview based on previewAudience
  const previewFilteredQuestions = useMemo(() => {
    return questions.filter((q) => {
      const aud = Number(q.targetAudience);
      if (aud === 3) return true; // Both
      if (previewAudience === "Attended" && aud === 1) return true;
      if (previewAudience === "NoShow" && aud === 2) return true;
      return false;
    });
  }, [questions, previewAudience]);

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

  const metrics = config?.Metrics || config?.metrics || {};
  const automation = config?.Automation || config?.automation || {};
  const isFeedbackEnabled = Boolean(config?.isFeedbackEnabled ?? config?.IsFeedbackEnabled ?? true);

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
                background: isFeedbackEnabled ? "#ecfdf5" : "#f1f5f9",
                color: isFeedbackEnabled ? "#15803d" : "#64748b",
                border: isFeedbackEnabled ? "1px solid #86efac" : "1px solid #cbd5e1",
                fontWeight: "750",
              }}
            >
              {isFeedbackEnabled ? "● Feedback Active" : "○ Disabled"}
            </span>
          </h1>
          <p className="fb-subpage-subtitle">
            Configure dynamic attendee feedback questionnaires, monitor star ratings &amp; reviews, and oversee
            automated MSG91 WhatsApp post-workshop campaigns.
          </p>
        </div>

        <div className="fb-header-actions">
          <button
            type="button"
            className="fb-btn-secondary"
            onClick={() => setShowResendModal(true)}
          >
            📲 Send / Resend WhatsApp
          </button>
          {activeTab === "builder" && (
            <button
              type="button"
              className="fb-btn-primary"
              onClick={handleSaveFormVersion}
              disabled={saving}
            >
              {saving ? "Saving Version..." : "💾 Save Form Version"}
            </button>
          )}
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
          <div className="fb-kpi-value" style={{ fontSize: "16px", display: "flex", gap: "10px", alignItems: "center" }}>
            <span style={{ color: "#1d4ed8" }}>✓ {metrics.EligibleAttendedCount ?? metrics.eligibleAttendedCount ?? 0} Attended</span>
            <span style={{ color: "#94a3b8" }}>|</span>
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
          <span className="fb-tab-badge">{questions.length} questions</span>
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

      {/* TAB 1: FORM BUILDER & LIVE MOBILE PREVIEW */}
      {activeTab === "builder" && (
        <div className="fb-builder-layout">
          {/* Left Column: Form Questions Editor */}
          <div>
            <div className="fb-card">
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "16px", flexWrap: "wrap", gap: "10px" }}>
                <div>
                  <h3 className="fb-card-title">Attendee Experience Form Designer</h3>
                  <p className="fb-card-desc" style={{ margin: 0 }}>
                    Configure the exact questionnaire participants interact with after clicking their unique WhatsApp feedback link.
                  </p>
                </div>
                <div style={{ fontSize: "12px", background: "#f1f5f9", padding: "4px 10px", borderRadius: "6px", color: "#475569", fontWeight: "700" }}>
                  Active Version: v{config?.ActiveVersionNumber ?? config?.activeVersionNumber ?? 1}
                </div>
              </div>

              {/* Questions List */}
              <div className="fb-questions-container">
                {questions.map((q, idx) => (
                  <div key={q.id || idx} className="fb-question-card">
                    <div className="fb-question-top">
                      <div className="fb-question-number-pill">
                        <span>#{idx + 1}</span>
                        <span>{q.questionType === 1 ? "⭐ Star Rating" : q.questionType === 2 ? "🔘 Single Choice" : q.questionType === 4 ? "☑ Multiple Choice" : "📝 Text Response"}</span>
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
                          disabled={idx === questions.length - 1}
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
                        placeholder="e.g. How would you rate the workshop choreography?"
                      />
                    </div>

                    {/* Question Parameters: Type & Target Audience */}
                    <div className="fb-grid-2col">
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

                      <div className="fb-form-group">
                        <label className="fb-form-label">Target Audience Filter</label>
                        <select
                          className="fb-select"
                          value={q.targetAudience}
                          onChange={(e) => handleUpdateQuestion(idx, "targetAudience", Number(e.target.value))}
                        >
                          <option value={3}>🌐 Both (All Attendees)</option>
                          <option value={1}>✓ Attended Only</option>
                          <option value={2}>✕ No-Show Only</option>
                        </select>
                      </div>
                    </div>

                    {/* Optional Choices Editor for Single/Multi Choice */}
                    {(Number(q.questionType) === 2 || Number(q.questionType) === 4) && (
                      <div className="fb-form-group" style={{ background: "#f8fafc", padding: "12px", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
                        <label className="fb-form-label">Answer Options / Choices</label>
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
                  <div style={{ fontSize: "18px", fontWeight: "800", marginBottom: "4px" }}>+ Add Question</div>
                  <div style={{ fontSize: "12px", color: "#64748b" }}>Rating, short text, or choice question</div>
                </div>
              </div>
            </div>
          </div>

          {/* Right Column: Live Mobile Attendee Preview */}
          <div className="fb-preview-sticky">
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "10px" }}>
              <span style={{ fontSize: "13px", fontWeight: "750", color: "#334155" }}>📱 Live Attendee Mobile Preview</span>
              {/* Audience Preview Toggle */}
              <div style={{ display: "flex", background: "#f1f5f9", padding: "2px", borderRadius: "6px" }}>
                <button
                  type="button"
                  onClick={() => setPreviewAudience("Attended")}
                  style={{
                    padding: "3px 8px",
                    fontSize: "11px",
                    fontWeight: "700",
                    border: "none",
                    borderRadius: "4px",
                    background: previewAudience === "Attended" ? "#ffffff" : "none",
                    color: previewAudience === "Attended" ? "#1d4ed8" : "#64748b",
                    boxShadow: previewAudience === "Attended" ? "0 1px 2px rgba(0,0,0,0.08)" : "none",
                    cursor: "pointer",
                  }}
                >
                  Attended
                </button>
                <button
                  type="button"
                  onClick={() => setPreviewAudience("NoShow")}
                  style={{
                    padding: "3px 8px",
                    fontSize: "11px",
                    fontWeight: "700",
                    border: "none",
                    borderRadius: "4px",
                    background: previewAudience === "NoShow" ? "#ffffff" : "none",
                    color: previewAudience === "NoShow" ? "#b91c1c" : "#64748b",
                    boxShadow: previewAudience === "NoShow" ? "0 1px 2px rgba(0,0,0,0.08)" : "none",
                    cursor: "pointer",
                  }}
                >
                  No-Show
                </button>
              </div>
            </div>

            <div className="fb-phone-frame">
              <div className="fb-phone-notch"></div>
              <div className="fb-phone-screen">
                <div className="fb-preview-banner">
                  <div className="fb-preview-studio-tag">ETHOS DANCE STUDIO</div>
                  <h4 className="fb-preview-ws-title">{workshop?.Title || workshop?.title || "Masterclass Workshop"}</h4>
                  <div className="fb-preview-ws-meta">
                    {previewAudience === "Attended" ? "How was your experience?" : "We missed you!"}
                  </div>
                </div>

                <div className="fb-preview-questions">
                  {previewFilteredQuestions.map((q, idx) => (
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
                  Dispatches verified post-workshop feedback requests to attendees via the durable WhatsApp Outbox.
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
                <div style={{ fontSize: "11px", fontWeight: "700", color: "#64748b", textTransform: "uppercase", letterSpacing: "0.05em" }}>Dispatch Window</div>
                <div style={{ fontSize: "14px", fontWeight: "750", color: "#0f172a", marginTop: "4px" }}>2 Hours Post-Event</div>
                <div style={{ fontSize: "11.5px", color: "#64748b", marginTop: "2px" }}>Auto-queued by worker daemon</div>
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
                    <td style={{ fontFamily: "monospace", fontWeight: "700", color: "#7c3aed" }}>Button Payload (URL Suffix)</td>
                    <td>tokenEntity.RawToken</td>
                    <td style={{ color: "#64748b" }}>Cryptographic HMAC token for authenticated 1-click feedback</td>
                    <td style={{ fontFamily: "monospace", fontSize: "12px", color: "#475569" }}>https://ethosdancestudio.com/feedback?token=&lt;secure-token&gt;</td>
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
                  👉 [Button] Give Feedback &rarr; https://ethosdancestudio.com/feedback?token=&lt;token&gt;
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
          {/* Rating Breakdown & Summary */}
          <div className="fb-card">
            <h3 className="fb-card-title">Star Ratings Distribution</h3>
            <p className="fb-card-desc">Comprehensive breakdown of all verified participant ratings.</p>

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
          {analytics?.QuestionAnalytics?.length > 0 && (
            <div className="fb-card">
              <h3 className="fb-card-title">Question-Level Breakdown</h3>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))", gap: "16px", marginTop: "16px" }}>
                {analytics.QuestionAnalytics.map((qa, idx) => (
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
            <h3 className="fb-card-title">Individual Participant Reviews</h3>
            <p className="fb-card-desc">Verified feedback submissions with answers.</p>

            {(analytics?.Submissions?.length || 0) === 0 ? (
              <div style={{ textAlign: "center", padding: "30px 20px", color: "#64748b" }}>
                💬 No written reviews submitted yet.
              </div>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
                {analytics.Submissions.map((sub) => (
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

      {/* RESEND FEEDBACK MODAL */}
      {showResendModal && (
        <div className="fb-modal-overlay">
          <div className="fb-modal-card">
            <div className="fb-modal-header">
              <h3>📲 Send / Resend WhatsApp Feedback</h3>
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
                  <p style={{ fontSize: "13.5px", color: "#475569", margin: "0 0 16px 0", lineHeight: "1.5" }}>
                    Select target audience to queue approved WhatsApp feedback requests into the MSG91 outbox.
                  </p>

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
