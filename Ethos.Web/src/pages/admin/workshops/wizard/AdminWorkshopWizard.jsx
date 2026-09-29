import React, { useState, useEffect, useRef } from "react";
import { useNavigate, useParams, Link } from "react-router-dom";
import {
  ChevronRight,
  ChevronLeft,
  Check,
  Save,
  ArrowLeft,
  AlertTriangle,
  Sparkles,
  FileText,
  Image as ImageIcon,
  MapPin,
  IndianRupee,
  CheckCircle2,
  AlertCircle,
  Ticket,
  Trash2,
  XCircle,
  RefreshCw,
} from "lucide-react";
import { adminApi } from "../../../../services/adminApi";
import { workshopsApi } from "../../../../services/workshopsApi";
import Step1Details, { DANCE_STYLE_PRESETS } from "./Step1Details";
import Step2Photos from "./Step2Photos";
import Step3VenueSchedule from "./Step3VenueSchedule";
import Step4TicketTypes from "./Step4TicketTypes";
import Step5TicketPricing from "./Step5TicketPricing";
import Step6Review from "./Step6Review";
import "./AdminWorkshopWizard.css";

const DRAFT_STORAGE_KEY = "ethos_admin_workshop_wizard_draft";

export const WIZARD_ACTION = {
  DRAFT: "draft",
  APPROVAL: "approval",
  PUBLISH: "publish",
};

const STEPS = [
  { id: 1, label: "Workshop Details", icon: FileText },
  { id: 2, label: "Photos", icon: ImageIcon },
  { id: 3, label: "Venue & Schedule", icon: MapPin },
  { id: 4, label: "Ticket Types", icon: Ticket },
  { id: 5, label: "Ticket Pricing", icon: IndianRupee },
  { id: 6, label: "Review & Publish", icon: Sparkles },
];

const INITIAL_FORM = {
  title: "",
  description: "",
  shortDescription: "",
  danceStyle: "Urban Choreography",
  customStyle: "",
  level: "Open Level",
  workshopDate: "",
  startTime: "",
  endTime: "",
  bookingCutoffTime: "",
  venue: "Ethos Main Studio A",
  venueAddress: "Plot 42, Road No 36, Jubilee Hills, Hyderabad",
  locationUrl: "",
  city: "Hyderabad",
  area: "Jubilee Hills",
  googlePlaceId: "ethos_main_a",
  latitude: 17.4319,
  longitude: 78.4073,
  price: 500,
  capacity: 35,
  trainerProfileId: "",
  trainerProfileIds: [],
  sessions: [],
  passTypes: [
    {
      name: "General Admission",
      description: "Standard workshop admission covering all sessions",
      price: 500,
      sessionsIncluded: null,
      totalQuantity: 50,
      salesStartUtc: "",
      salesEndUtc: "",
      displayOrder: 1,
      isActive: true,
      pricingTiers: [
        { tierNumber: 1, tierName: "Tier 1 (1–10)", minTickets: 1, maxTickets: 10, price: 500 },
        { tierNumber: 2, tierName: "Tier 2 (11–20)", minTickets: 11, maxTickets: 20, price: 600 },
        { tierNumber: 3, tierName: "Tier 3 (21+)", minTickets: 21, maxTickets: null, price: 700 },
      ],
    },
  ],
  imageUrl: "",
  landscapeImageUrl: "",
  contactPerson: "",
  contactNumber: "",
  publicVisibility: true,
  registrationType: "Standard",
  termsAndCancellationPolicy: "",
  status: 3, // 3: Approved, 1: Draft, 2: PendingApproval
  isEthosOriginal: false,
  pricingTiers: [
    { tierNumber: 1, tierName: "Early Bird Tier", minTickets: 1, maxTickets: 10, price: 500 },
    { tierNumber: 2, tierName: "Standard Tier", minTickets: 11, maxTickets: 20, price: 600 },
    { tierNumber: 3, tierName: "Peak Tier", minTickets: 21, maxTickets: 30, price: 700 },
    { tierNumber: 4, tierName: "Final Batch Tier", minTickets: 31, maxTickets: null, price: 800 },
  ],
};

export default function AdminWorkshopWizard() {
  const navigate = useNavigate();
  const { workshopId, id } = useParams();
  const effectiveId = workshopId || id;
  const isEdit = Boolean(effectiveId);

  const [currentStep, setCurrentStep] = useState(1);
  const [form, setForm] = useState(INITIAL_FORM);
  const [trainers, setTrainers] = useState([]);
  const [loadingTrainers, setLoadingTrainers] = useState(false);
  const [loadingWorkshop, setLoadingWorkshop] = useState(isEdit);
  const [loadError, setLoadError] = useState(null);
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState({});
  const [statusBanner, setStatusBanner] = useState(null);
  const [isLockedOut, setIsLockedOut] = useState(false);

  // Server-side Draft state
  const [serverDraft, setServerDraft] = useState(null);
  const [showResumeModal, setShowResumeModal] = useState(false);
  const [draftId, setDraftId] = useState(null);
  const [draftVersion, setDraftVersion] = useState(0);
  const [isAutosaving, setIsAutosaving] = useState(false);
  const [conflictModal, setConflictModal] = useState(null);

  const formRef = useRef(form);
  formRef.current = form;
  const draftVersionRef = useRef(draftVersion);
  draftVersionRef.current = draftVersion;
  const draftIdRef = useRef(draftId);
  draftIdRef.current = draftId;
  const currentStepRef = useRef(currentStep);
  currentStepRef.current = currentStep;
  const hasLoadedRef = useRef(false);
  const isSavingDraftRef = useRef(false);

  // Photo upload Blobs for R2 upload
  const [portraitBlob, setPortraitBlob] = useState(null);
  const [landscapeBlob, setLandscapeBlob] = useState(null);

  // Load trainers
  useEffect(() => {
    let mounted = true;
    async function loadTrainers() {
      setLoadingTrainers(true);
      try {
        const res = await adminApi.getTrainers("pageSize=100");
        const list = res?.items || (Array.isArray(res) ? res : []);
        if (mounted) setTrainers(list);
      } catch (err) {
        console.warn("Could not load trainers list:", err);
      } finally {
        if (mounted) setLoadingTrainers(false);
      }
    }
    loadTrainers();
    return () => {
      mounted = false;
    };
  }, []);

  // Load existing workshop if in edit mode, or recover draft from sessionStorage if create mode
  useEffect(() => {
    if (isEdit) {
      let mounted = true;
      async function loadWorkshopData() {
        setLoadingWorkshop(true);
        setLoadError(null);
        try {
          let ws = null;
          try {
            // Attempt admin API with a 2.5-second hard timeout
            const adminPromise = adminApi.getWorkshopById(effectiveId, { timeout: 2500 });
            let timeoutId = null;
            const timeoutPromise = new Promise((_, reject) => {
              timeoutId = setTimeout(() => reject(new Error("Admin lookup timeout")), 2500);
            });
            ws = await Promise.race([adminPromise, timeoutPromise]);
            if (timeoutId) clearTimeout(timeoutId);
          } catch (apiErr) {
            console.warn("adminApi.getWorkshopById failed or timed out, falling back to workshopsApi:", apiErr);
            try {
              ws = await workshopsApi.getWorkshopById(effectiveId);
            } catch (fallbackErr) {
              console.warn("workshopsApi.getWorkshopById failed, checking approved workshops list:", fallbackErr);
              try {
                const list = await workshopsApi.getApprovedWorkshops();
                ws = (list || []).find(
                  (item) =>
                    String(item.id).toLowerCase() === String(effectiveId).toLowerCase() ||
                    item.slug === effectiveId
                );
              } catch (listErr) {
                console.error("All workshop lookup fallbacks failed:", listErr);
              }
              if (!ws) {
                throw apiErr;
              }
            }
          }
          if (!mounted) return;
          if (!ws) {
            throw new Error("Workshop not found.");
          }

          // Check lockout: Ongoing or Completed
          const phase = ws.lifecyclePhase || ws.LifecyclePhase;
          if (phase === "Ongoing" || phase === "Completed") {
            setIsLockedOut(true);
            setStatusBanner({
              type: "warning",
              text: `This workshop is currently ${phase}. Core schedule and pricing edits are locked by server authority.`,
            });
          }

          const isPresetStyle = DANCE_STYLE_PRESETS.includes(ws.danceStyle);
          const defaultLeadTrainer = ws.trainerProfileId ||
            (Array.isArray(ws.trainers) && ws.trainers.length > 0 ? (ws.trainers[0].trainerProfileId || ws.trainers[0].id) : "");
          
          const leadTrainerIds = Array.isArray(ws.trainers) && ws.trainers.length > 0
            ? ws.trainers.map((t) => t.trainerProfileId || t.id).filter(Boolean)
            : (defaultLeadTrainer ? [defaultLeadTrainer] : []);

          if (Array.isArray(ws.trainers) && ws.trainers.length > 0) {
            setTrainers((prev) => {
              if (prev && prev.length > 0) return prev;
              return ws.trainers.map((t) => ({
                id: t.trainerProfileId || t.id,
                fullName: t.name || t.fullName || "Ethos Trainer",
                profilePhotoUrl: t.photoUrl || t.profilePhotoUrl,
                primaryDanceStyle: t.danceStyles || t.primaryDanceStyle,
              }));
            });
          }

          const mappedSessions = Array.isArray(ws.sessions) && ws.sessions.length > 0
            ? ws.sessions.map((s, idx) => {
                const sTrainers = Array.isArray(s.trainers) && s.trainers.length > 0
                  ? s.trainers.map((t) => t.trainerProfileId || t.id).filter(Boolean)
                  : (s.trainerProfileId ? [s.trainerProfileId] : (defaultLeadTrainer ? [defaultLeadTrainer] : []));
                
                const sessionLead = s.trainerProfileId || (sTrainers.length > 0 ? sTrainers[0] : defaultLeadTrainer);

                return {
                  id: s.id,
                  clientId: s.clientId || s.id || (typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : `sess_${idx}_${Date.now()}`),
                  sessionDate: s.sessionDate ? String(s.sessionDate).slice(0, 10) : "",
                  startTime: s.startTime ? String(s.startTime).slice(0, 5) : "18:00",
                  endTime: s.endTime ? String(s.endTime).slice(0, 5) : "19:30",
                  trainerProfileId: sessionLead || "",
                  trainerProfileIds: sTrainers,
                  title: s.title || `Session ${idx + 1}`,
                  description: s.description || "",
                  capacity: Number(s.capacity) || Number(ws.capacity) || 30,
                  bookedSeats: Number(s.bookedSeats) || 0,
                  bookingCutoffTime: s.bookingCutoffTime ? String(s.bookingCutoffTime).slice(0, 5) : "",
                  posterImageUrl: s.posterImageUrl || "",
                  displayOrder: s.displayOrder ?? idx + 1,
                  isActive: s.isActive !== false,
                };
              })
            : [];

          const mappedPassTypes = Array.isArray(ws.passTypes) && ws.passTypes.length > 0
            ? ws.passTypes.map((p, idx) => {
                const isSingle = Boolean(p.workshopSessionId) || (p.sessionsIncluded === 1);
                const isBundle = !isSingle && (p.sessionsIncluded != null && p.sessionsIncluded >= 2);
                const category = isSingle ? "SINGLE" : (isBundle ? "BUNDLE" : "ALL_ACCESS");
                const matchedSess = mappedSessions.find((s) => s.id === p.workshopSessionId) ||
                  (ws.sessions ? ws.sessions.find((s) => s.id === p.workshopSessionId) : null);

                return {
                  id: p.id,
                  category,
                  workshopSessionId: null,
                  targetSessionClientId: null,
                  name: p.name || "",
                  description: p.description || "",
                  price: p.currentPrice ?? p.price ?? 500,
                  sessionsIncluded: p.sessionsIncluded != null ? Number(p.sessionsIncluded) : (category === "ALL_ACCESS" ? null : 1),
                  totalQuantity: p.totalQuantity ?? 50,
                  salesStartUtc: p.salesStartUtc ? String(p.salesStartUtc).slice(0, 16) : "",
                  salesEndUtc: p.salesEndUtc ? String(p.salesEndUtc).slice(0, 16) : "",
                  displayOrder: p.displayOrder ?? idx + 1,
                  isActive: p.isActive !== false,
                  pricingTiers: Array.isArray(p.pricingTiers) && p.pricingTiers.length > 0
                    ? p.pricingTiers.map((t, tIdx) => ({
                        tierNumber: t.tierNumber || tIdx + 1,
                        tierName: t.tierName || `Tier ${tIdx + 1}`,
                        minTickets: t.minTickets ?? 1,
                        maxTickets: t.maxTickets != null ? t.maxTickets : null,
                        price: t.price ?? 500,
                      }))
                    : [
                      {
                        tierNumber: 1,
                        tierName: `${p.name || "Standard"} - Tier 1`,
                        minTickets: 1,
                        maxTickets: null,
                        price: p.price ?? 500,
                      },
                    ],
                };
              })
            : [];

          setForm({
            title: ws.title || "",
            description: ws.description || "",
            shortDescription: ws.shortDescription || "",
            danceStyle: isPresetStyle ? ws.danceStyle : "Other",
            customStyle: isPresetStyle ? "" : ws.danceStyle || "",
            level: ws.level || "Open Level",
            workshopDate: ws.workshopDate ? String(ws.workshopDate).slice(0, 10) : "",
            startTime: ws.startTime ? String(ws.startTime).slice(0, 5) : "18:00",
            endTime: ws.endTime ? String(ws.endTime).slice(0, 5) : "19:30",
            bookingCutoffTime: ws.bookingCutoffTime ? String(ws.bookingCutoffTime).slice(0, 5) : "",
            venue: ws.venue || "Ethos Main Studio",
            venueAddress: ws.venueAddress || "",
            locationUrl: ws.locationUrl || "",
            city: ws.city || "Hyderabad",
            area: ws.area || "",
            googlePlaceId: ws.googlePlaceId || "",
            latitude: ws.latitude || null,
            longitude: ws.longitude || null,
            price: ws.price ?? 500,
            capacity: ws.capacity ?? 35,
            trainerProfileId: defaultLeadTrainer || "",
            trainerProfileIds: leadTrainerIds,
            sessions: mappedSessions,
            passTypes: mappedPassTypes,
            imageUrl: ws.imageUrl || "",
            landscapeImageUrl: ws.landscapeImageUrl || "",
            contactPerson: ws.contactPerson || "",
            contactNumber: ws.contactNumber || "",
            publicVisibility: ws.publicVisibility ?? true,
            registrationType: ws.registrationType || "Standard",
            termsAndCancellationPolicy: ws.termsAndCancellationPolicy || "",
            status: ws.status === "Draft" ? 1 : ws.status === "PendingApproval" ? 2 : 3,
            isEthosOriginal: ws.isEthosOriginal ?? false,
          });
        } catch (err) {
          console.error("Failed to load workshop data:", err);
          if (mounted) {
            setLoadError(err?.message || "Workshop could not be loaded.");
            setStatusBanner({
              type: "error",
              text: err?.message || "Failed to load workshop data.",
            });
          }
        } finally {
          if (mounted) setLoadingWorkshop(false);
        }
      }
      loadWorkshopData();
      return () => {
        mounted = false;
      };
    }
  }, [isEdit, effectiveId]);

  // Check for existing server-side draft on mount
  useEffect(() => {
    let mounted = true;
    async function checkServerDraft() {
      try {
        const draft = await adminApi.getWorkshopDraft(effectiveId || null);
        if (mounted && draft && draft.draftJson) {
          setServerDraft(draft);
          setShowResumeModal(true);
        }
      } catch (err) {
        // Silently ignore if no draft exists
      }
    }
    checkServerDraft();
    return () => {
      mounted = false;
    };
  }, [effectiveId]);

  const performSaveDraft = async (manual = false) => {
    if (isSavingDraftRef.current || isLockedOut || loadingWorkshop || showResumeModal || conflictModal) return;
    isSavingDraftRef.current = true;
    setIsAutosaving(true);
    try {
      const payload = {
        workshopId: effectiveId || null,
        draftJson: JSON.stringify({
          ...formRef.current,
          currentStep: currentStepRef.current,
        }),
        expectedVersion: draftVersionRef.current,
      };
      const res = await adminApi.saveWorkshopDraft(payload);

      // Synchronously update refs to eliminate stale version closures and race conditions
      draftIdRef.current = res.id;
      draftVersionRef.current = res.version;

      setDraftId(res.id);
      setDraftVersion(res.version);
      try {
        sessionStorage.setItem(DRAFT_STORAGE_KEY, JSON.stringify(formRef.current));
      } catch {}

      if (manual) {
        setStatusBanner({
          type: "success",
          text: `Draft saved successfully at ${new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" })}. You can safely return to this draft anytime from the Draft tab.`,
        });
      }
    } catch (err) {
      if (err.status === 409 || err.code === "CONCURRENCY_CONFLICT" || (err.message && err.message.toLowerCase().includes("conflict"))) {
        if (err.serverVersion != null) {
          draftVersionRef.current = err.serverVersion;
          setDraftVersion(err.serverVersion);
        }
        setConflictModal({
          message: err.message || "This workshop draft was modified in another session or window.",
        });
      } else if (manual) {
        setStatusBanner({
          type: "error",
          text: err.message || "Failed to save draft.",
        });
      }
    } finally {
      setIsAutosaving(false);
      isSavingDraftRef.current = false;
    }
  };

  // Debounced autosave (1500ms)
  useEffect(() => {
    if (!hasLoadedRef.current) {
      if (!loadingWorkshop) {
        hasLoadedRef.current = true;
      }
      return;
    }
    if (loadingWorkshop || showResumeModal || isLockedOut || conflictModal || isSavingDraftRef.current) return;

    const timer = setTimeout(() => {
      performSaveDraft(false);
    }, 1500);

    return () => clearTimeout(timer);
  }, [form, loadingWorkshop, showResumeModal, isLockedOut, conflictModal]);

  const handleResumeDraft = () => {
    if (serverDraft && serverDraft.draftJson) {
      try {
        const parsed = JSON.parse(serverDraft.draftJson);
        setForm((prev) => ({ ...prev, ...parsed }));
        if (parsed.currentStep && parsed.currentStep >= 1 && parsed.currentStep <= STEPS.length) {
          setCurrentStep(parsed.currentStep);
        }
        draftIdRef.current = serverDraft.id;
        draftVersionRef.current = serverDraft.version;
        setDraftId(serverDraft.id);
        setDraftVersion(serverDraft.version);
        setStatusBanner({
          type: "success",
          text: `Draft restored from ${new Date(serverDraft.updatedAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}.`,
        });
      } catch (e) {
        console.error("Failed to parse draft JSON", e);
      }
    }
    setShowResumeModal(false);
  };

  const handleDiscardDraft = async () => {
    if (serverDraft?.id) {
      try {
        await adminApi.discardWorkshopDraft(serverDraft.id);
      } catch (err) {
        console.warn("Could not discard server draft:", err);
      }
    }
    sessionStorage.removeItem(DRAFT_STORAGE_KEY);
    setServerDraft(null);
    draftIdRef.current = null;
    draftVersionRef.current = 0;
    setDraftId(null);
    setDraftVersion(0);
    setShowResumeModal(false);
    setStatusBanner({
      type: "info",
      text: "Unsaved draft discarded.",
    });
  };

  const handleForceOverwrite = async () => {
    try {
      const latest = await adminApi.getWorkshopDraft(effectiveId || null);
      const newVersion = latest ? latest.version : 0;
      const res = await adminApi.saveWorkshopDraft({
        workshopId: effectiveId || null,
        draftJson: JSON.stringify({
          ...formRef.current,
          currentStep: currentStepRef.current,
        }),
        expectedVersion: newVersion,
      });
      draftIdRef.current = res.id;
      draftVersionRef.current = res.version;
      setDraftId(res.id);
      setDraftVersion(res.version);
      setConflictModal(null);
      setStatusBanner({
        type: "success",
        text: "Draft successfully overwritten.",
      });
    } catch (err) {
      alert("Failed to overwrite draft: " + err.message);
    }
  };

  const handleReloadServerVersion = async () => {
    try {
      const latest = await adminApi.getWorkshopDraft(effectiveId || null);
      if (latest && latest.draftJson) {
        const parsed = JSON.parse(latest.draftJson);
        setForm((prev) => ({ ...prev, ...parsed }));
        draftIdRef.current = latest.id;
        draftVersionRef.current = latest.version;
        setDraftId(latest.id);
        setDraftVersion(latest.version);
      }
      setConflictModal(null);
      setStatusBanner({
        type: "info",
        text: "Reloaded latest draft from server.",
      });
    } catch (err) {
      alert("Failed to reload draft: " + err.message);
    }
  };

  const handleFieldChange = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    if (errors[field]) {
      setErrors((prev) => ({ ...prev, [field]: null }));
    }
  };

  // Validation rules per step
  const validateStep = (stepNum) => {
    const errs = {};

    if (stepNum === 1) {
      if (!form.title || form.title.trim().length < 5) {
        errs.title = "Workshop title must be at least 5 characters.";
      }
      if (!form.trainerProfileId) {
        errs.trainerProfileId = "Please select at least one trainer.";
      }
      if (form.danceStyle === "Other" && !form.customStyle?.trim()) {
        errs.customStyle = "Please specify the custom dance style.";
      }
      if (!form.city || !form.city.trim()) {
        errs.city = "City is required.";
      }
      if (form.shortDescription && form.shortDescription.length > 160) {
        errs.shortDescription = "Short tagline cannot exceed 160 characters.";
      }
    }

    if (stepNum === 2) {
      if (!form.imageUrl) {
        errs.imageUrl = "Portrait cover image (3:4) is required.";
      }
    }

    if (stepNum === 3) {
      if (!form.venue || !form.venue.trim()) {
        errs.venue = "Venue name is required.";
      }
      if (form.locationUrl && form.locationUrl.trim()) {
        const trimmed = form.locationUrl.trim();
        try {
          const parsedUrl = new URL(trimmed);
          if (parsedUrl.protocol !== "https:") {
            errs.locationUrl = "Location link must be a secure URL starting with https://";
          }
        } catch (_) {
          errs.locationUrl = "Please enter a valid URL (starting with https://)";
        }
      }
      if (!Array.isArray(form.sessions) || form.sessions.length === 0) {
        errs.sessions = "Please configure at least one workshop date with sessions.";
      } else {
        for (let i = 0; i < form.sessions.length; i++) {
          const s1 = form.sessions[i];
          if (!s1.sessionDate) {
            errs.sessions = `Session #${i + 1} must belong to a valid date.`;
            break;
          }
          if (!s1.startTime || !s1.endTime) {
            errs.sessions = `Session "${s1.title || i + 1}" must have valid start and end times.`;
            break;
          }
          if (s1.endTime <= s1.startTime) {
            errs.sessions = `Session "${s1.title || i + 1}" end time must be after start time.`;
            break;
          }
          if (!s1.capacity || Number(s1.capacity) <= 0) {
            errs.sessions = `Session "${s1.title || i + 1}" capacity must be greater than zero.`;
            break;
          }

          // Same trainer overlap check
          for (let j = i + 1; j < form.sessions.length; j++) {
            const s2 = form.sessions[j];
            if (
              s1.sessionDate === s2.sessionDate &&
              s1.trainerProfileId &&
              s2.trainerProfileId &&
              s1.trainerProfileId === s2.trainerProfileId
            ) {
              if (s1.startTime < s2.endTime && s2.startTime < s1.endTime) {
                errs.sessions = `Trainer has overlapping sessions on ${s1.sessionDate}: "${s1.title || `Session ${i + 1}`}" and "${s2.title || `Session ${j + 1}`}".`;
                break;
              }
            }
          }
          if (errs.sessions) break;
        }
      }
    }

    if (stepNum === 4) {
      if (!Array.isArray(form.passTypes) || form.passTypes.length === 0) {
        errs.passTypes = "Please configure at least one ticket type before proceeding.";
      } else {
        for (let i = 0; i < form.passTypes.length; i++) {
          const pt = form.passTypes[i];
          if (!pt.name || !pt.name.trim()) {
            errs.passTypes = `Ticket Type #${i + 1} must have a valid name.`;
            break;
          }
          if (!pt.description || !pt.description.trim()) {
            errs.passTypes = `Ticket Description is required for "${pt.name || `Ticket #${i + 1}`}".`;
            break;
          }
          if (!pt.totalQuantity || pt.totalQuantity < 1) {
            errs.passTypes = `Ticket Type "${pt.name || i + 1}" capacity must be at least 1 ticket.`;
            break;
          }
          if (pt.category === "BUNDLE" || (pt.sessionsIncluded != null && pt.sessionsIncluded > 1)) {
            if (Number(pt.sessionsIncluded) < 2) {
              errs.passTypes = `Multi-Session Pass "${pt.name}" must include at least 2 sessions.`;
              break;
            }
          }
        }
      }
    }

    if (stepNum === 5) {
      if (!Array.isArray(form.passTypes) || form.passTypes.length === 0) {
        errs.pricing = "Please configure ticket types in Step 4 before configuring pricing.";
      } else {
        for (let i = 0; i < form.passTypes.length; i++) {
          const pt = form.passTypes[i];
          const tiers = pt.pricingTiers;
          if (!Array.isArray(tiers) || tiers.length === 0) {
            errs.pricing = `Ticket type "${pt.name}" must have at least one pricing tier.`;
            break;
          }
          if (tiers[0].minTickets !== 1) {
            errs.pricing = `Pricing for "${pt.name}" must start at Min Tickets = 1.`;
            break;
          }
          let hasError = false;
          for (let j = 0; j < tiers.length; j++) {
            if (tiers[j].price == null || tiers[j].price <= 0) {
              errs.pricing = `Price for "${pt.name}" Tier ${j + 1} must be greater than zero.`;
              hasError = true;
              break;
            }
            if (tiers[j].maxTickets != null && tiers[j].maxTickets < tiers[j].minTickets) {
              errs.pricing = `Tier ${j + 1} for "${pt.name}" max tickets (${tiers[j].maxTickets}) cannot be less than min tickets (${tiers[j].minTickets}).`;
              hasError = true;
              break;
            }
            if (j < tiers.length - 1 && tiers[j].maxTickets == null) {
              errs.pricing = `Only the final tier for "${pt.name}" can be open-ended (unlimited).`;
              hasError = true;
              break;
            }
            if (j > 0) {
              if (tiers[j].price < tiers[j - 1].price) {
                errs.pricing = `Non-decreasing price rule violated for "${pt.name}": Tier ${j + 1} (₹${tiers[j].price}) cannot be less than Tier ${j} (₹${tiers[j - 1].price}).`;
                hasError = true;
                break;
              }
              if (tiers[j - 1].maxTickets != null && tiers[j].minTickets !== tiers[j - 1].maxTickets + 1) {
                errs.pricing = `Discontinuity in pricing tiers for "${pt.name}": Tier ${j} ends at ${tiers[j - 1].maxTickets}, but Tier ${j + 1} starts at ${tiers[j].minTickets}.`;
                hasError = true;
                break;
              }
            }
          }
          if (hasError) break;
        }
      }
    }

    setErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handleNext = () => {
    if (validateStep(currentStep)) {
      setCurrentStep((prev) => Math.min(prev + 1, STEPS.length));
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
  };

  const handleBack = () => {
    setCurrentStep((prev) => Math.max(prev - 1, 1));
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  // Upload Blobs or Base64 data URLs to Cloudflare R2 / media storage before payload submission
  const uploadPendingPhotos = async () => {
    let finalImageUrl = form.imageUrl;
    let finalLandscapeUrl = form.landscapeImageUrl;

    const dataUrlToFile = async (dataUrl, filename) => {
      const res = await fetch(dataUrl);
      const blob = await res.blob();
      return new File([blob], filename, { type: blob.type || "image/jpeg" });
    };

    let pBlob = portraitBlob;
    if (!pBlob && form.imageUrl && form.imageUrl.startsWith("data:image/")) {
      try { pBlob = await dataUrlToFile(form.imageUrl, "workshop-portrait.jpg"); } catch (e) { console.warn("Blob conversion failed:", e); }
    }

    if (pBlob) {
      try {
        const fd = new FormData();
        fd.append("file", pBlob, "workshop-portrait.jpg");
        fd.append("folder", "workshops");
        const res = await adminApi.uploadMedia(fd);
        finalImageUrl = res.url || res.mediaUrl || finalImageUrl;
      } catch (err) {
        console.warn("Portrait media upload failed, proceeding with current URL:", err);
      }
    }

    let lBlob = landscapeBlob;
    if (!lBlob && form.landscapeImageUrl && form.landscapeImageUrl.startsWith("data:image/")) {
      try { lBlob = await dataUrlToFile(form.landscapeImageUrl, "workshop-landscape.jpg"); } catch (e) { console.warn("Blob conversion failed:", e); }
    }

    if (lBlob) {
      try {
        const fd = new FormData();
        fd.append("file", lBlob, "workshop-landscape.jpg");
        fd.append("folder", "workshops");
        const res = await adminApi.uploadMedia(fd);
        finalLandscapeUrl = res.url || res.mediaUrl || finalLandscapeUrl;
      } catch (err) {
        console.warn("Landscape media upload failed, proceeding with current URL:", err);
      }
    }

    // Session posters
    if (Array.isArray(form.sessions) && form.sessions.length > 0) {
      for (const s of form.sessions) {
        let sBlob = s.posterBlob;
        if (!sBlob && s.posterImageUrl && s.posterImageUrl.startsWith("data:image/")) {
          try {
            sBlob = await dataUrlToFile(s.posterImageUrl, `session-${s.title || "poster"}.jpg`);
          } catch (e) {
            console.warn("Session poster blob conversion failed:", e);
          }
        }
        if (sBlob) {
          try {
            const fd = new FormData();
            fd.append("file", sBlob, `session-${s.title || "poster"}.jpg`);
            fd.append("folder", "workshops/sessions");
            const res = await adminApi.uploadMedia(fd);
            s.posterImageUrl = res.url || res.mediaUrl || s.posterImageUrl;
          } catch (err) {
            console.warn("Session poster upload failed, proceeding:", err);
          }
        }
      }
    }

    return { finalImageUrl, finalLandscapeUrl };
  };

  // Payload builder
  const buildPayload = async (targetStatus) => {
    const { finalImageUrl, finalLandscapeUrl } = await uploadPendingPhotos();

    const actualStyle =
      form.danceStyle === "Other" && form.customStyle?.trim()
        ? form.customStyle.trim()
        : form.danceStyle;

    const formattedTiers = Array.isArray(form.pricingTiers) && form.pricingTiers.length > 0
      ? form.pricingTiers.map((t, idx) => ({
          tierNumber: idx + 1,
          tierName: t.tierName || `Tier ${idx + 1}`,
          minTickets: Number(t.minTickets) || 1,
          maxTickets: t.maxTickets != null && t.maxTickets !== "" ? Number(t.maxTickets) : null,
          price: Number(t.price) || 500,
        }))
      : undefined;

    const rawTrainerIds = Array.isArray(form.trainerProfileIds) ? form.trainerProfileIds : [];
    const validTrainerIds = rawTrainerIds.filter(
      (id) => id && typeof id === "string" && id !== "none" && id.trim().length >= 32
    );
    const leadTrainerId = form.trainerProfileId && form.trainerProfileId !== "none" && form.trainerProfileId.trim().length >= 32
      ? form.trainerProfileId.trim()
      : (validTrainerIds[0] || null);

    if (leadTrainerId && !validTrainerIds.includes(leadTrainerId)) {
      validTrainerIds.unshift(leadTrainerId);
    }

    const sessionClientToIdMap = new Map();
    const mappedSessions = Array.isArray(form.sessions) && form.sessions.length > 0
      ? form.sessions.map((s, idx) => {
          const rawSessionTrainers = Array.isArray(s.trainerProfileIds) ? s.trainerProfileIds : [];
          const validSessionTrainers = rawSessionTrainers.filter(
            (id) => id && typeof id === "string" && id !== "none" && id.trim().length >= 32
          );
          const sLeadTrainer = (s.trainerProfileId && s.trainerProfileId !== "none" && s.trainerProfileId.trim().length >= 32)
            ? s.trainerProfileId.trim()
            : (validSessionTrainers[0] || leadTrainerId || undefined);

          if (sLeadTrainer && !validSessionTrainers.includes(sLeadTrainer)) {
            validSessionTrainers.unshift(sLeadTrainer);
          }

          const assignedSessionId = (s.id && s.id.length >= 32)
            ? s.id
            : (typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : undefined);

          if (s.clientId && assignedSessionId) {
            sessionClientToIdMap.set(s.clientId, assignedSessionId);
          }
          if (s.id && assignedSessionId) {
            sessionClientToIdMap.set(s.id, assignedSessionId);
          }

          return {
            id: assignedSessionId,
            sessionDate: s.sessionDate,
            startTime: s.startTime?.length === 5 ? `${s.startTime}:00` : s.startTime,
            endTime: s.endTime?.length === 5 ? `${s.endTime}:00` : s.endTime,
            trainerProfileId: sLeadTrainer,
            trainerProfileIds: validSessionTrainers.length > 0 ? validSessionTrainers : (sLeadTrainer ? [sLeadTrainer] : undefined),
            title: s.title?.trim() || `Session ${idx + 1}`,
            description: s.description?.trim() || null,
            capacity: Number(s.capacity) || form.capacity || 30,
            bookingCutoffTime: s.bookingCutoffTime && s.bookingCutoffTime.trim() !== ""
              ? (s.bookingCutoffTime.length === 5 ? `${s.bookingCutoffTime}:00` : s.bookingCutoffTime)
              : null,
            posterImageUrl: s.posterImageUrl && !s.posterImageUrl.startsWith("data:image/") ? s.posterImageUrl.trim() : null,
            displayOrder: idx + 1,
            isActive: s.isActive !== false,
          };
        })
      : undefined;

    return {
      title: form.title.trim(),
      description: form.description?.trim() || "",
      shortDescription: form.shortDescription?.trim() || "",
      danceStyle: actualStyle,
      level: form.level,
      workshopDate: (() => {
        if (Array.isArray(form.sessions) && form.sessions.length > 0) {
          const sorted = [...form.sessions].filter((s) => s.sessionDate).sort((a, b) => {
            const dateCmp = (a.sessionDate || "").localeCompare(b.sessionDate || "");
            if (dateCmp !== 0) return dateCmp;
            return (a.startTime || "").localeCompare(b.startTime || "");
          });
          if (sorted.length > 0) return sorted[0].sessionDate;
        }
        return form.workshopDate;
      })(),
      startTime: (() => {
        if (Array.isArray(form.sessions) && form.sessions.length > 0) {
          const sorted = [...form.sessions].filter((s) => s.sessionDate && s.startTime).sort((a, b) => {
            const dateCmp = (a.sessionDate || "").localeCompare(b.sessionDate || "");
            if (dateCmp !== 0) return dateCmp;
            return (a.startTime || "").localeCompare(b.startTime || "");
          });
          if (sorted.length > 0) {
            const st = sorted[0].startTime;
            return st.length === 5 ? `${st}:00` : st;
          }
        }
        return form.startTime?.length === 5 ? `${form.startTime}:00` : (form.startTime || "10:00:00");
      })(),
      endTime: (() => {
        if (Array.isArray(form.sessions) && form.sessions.length > 0) {
          const sorted = [...form.sessions].filter((s) => s.sessionDate && s.endTime).sort((a, b) => {
            const dateCmp = (b.sessionDate || "").localeCompare(a.sessionDate || "");
            if (dateCmp !== 0) return dateCmp;
            return (b.endTime || "").localeCompare(a.endTime || "");
          });
          if (sorted.length > 0) {
            const et = sorted[0].endTime;
            return et.length === 5 ? `${et}:00` : et;
          }
        }
        return form.endTime?.length === 5 ? `${form.endTime}:00` : (form.endTime || "11:30:00");
      })(),
      bookingCutoffTime: form.bookingCutoffTime ? (form.bookingCutoffTime.length === 5 ? `${form.bookingCutoffTime}:00` : form.bookingCutoffTime) : null,
      venue: form.venue.trim(),
      venueAddress: form.venueAddress?.trim() || "",
      locationUrl: form.locationUrl?.trim() || null,
      city: form.city?.trim() || "Hyderabad",
      area: form.area?.trim() || "",
      googlePlaceId: form.googlePlaceId || "",
      latitude: form.latitude,
      longitude: form.longitude,
      price: (() => {
        if (Array.isArray(form.passTypes) && form.passTypes.length > 0) {
          const prices = form.passTypes.map((p) => {
            if (Array.isArray(p.pricingTiers) && p.pricingTiers.length > 0 && p.pricingTiers[0].price != null) {
              return Number(p.pricingTiers[0].price);
            }
            return Number(p.price) || 0;
          }).filter((pr) => pr > 0);
          if (prices.length > 0) return Math.min(...prices);
        }
        return Number(form.price) || (formattedTiers && formattedTiers[0] ? formattedTiers[0].price : 500);
      })(),
      capacity: (() => {
        if (Array.isArray(form.passTypes) && form.passTypes.length > 0) {
          const totalPassCap = form.passTypes.reduce((sum, p) => sum + (Number(p.totalQuantity) || 0), 0);
          if (totalPassCap > 0) return totalPassCap;
        }
        return Number(form.capacity) || 35;
      })(),
      trainerProfileId: leadTrainerId,
      trainerProfileIds: validTrainerIds,
      sessions: mappedSessions,
      passTypes: Array.isArray(form.passTypes) && form.passTypes.length > 0
        ? form.passTypes.map((p, idx) => {
            const pTiers = Array.isArray(p.pricingTiers) && p.pricingTiers.length > 0
              ? p.pricingTiers.map((t, tIdx) => ({
                  tierNumber: tIdx + 1,
                  tierName: t.tierName?.trim() || `Tier ${tIdx + 1}`,
                  minTickets: Number(t.minTickets) || 1,
                  maxTickets: t.maxTickets != null && t.maxTickets !== "" ? Number(t.maxTickets) : null,
                  price: Number(t.price) || 500,
                }))
              : undefined;

            const basePrice = pTiers && pTiers[0] ? pTiers[0].price : (Number(p.price) || 500);

            const numSessions = (p.sessionsIncluded !== null && p.sessionsIncluded !== undefined && p.sessionsIncluded !== "")
              ? Number(p.sessionsIncluded)
              : null;

            return {
              id: p.id && p.id.length >= 32 ? p.id : undefined,
              workshopSessionId: null,
              name: p.name.trim(),
              description: p.description?.trim() || "",
              price: basePrice,
              sessionsIncluded: numSessions,
              totalQuantity: Number(p.totalQuantity) || 50,
              salesStartUtc: p.salesStartUtc && p.salesStartUtc.trim() !== ""
                ? (p.salesStartUtc.includes("Z") ? p.salesStartUtc : new Date(p.salesStartUtc).toISOString())
                : null,
              salesEndUtc: p.salesEndUtc && p.salesEndUtc.trim() !== ""
                ? (p.salesEndUtc.includes("Z") ? p.salesEndUtc : new Date(p.salesEndUtc).toISOString())
                : null,
              displayOrder: idx + 1,
              isActive: p.isActive !== false,
              pricingTiers: pTiers,
            };
          })
        : undefined,
      imageUrl: finalImageUrl,
      landscapeImageUrl: finalLandscapeUrl,
      contactPerson: form.contactPerson?.trim() || "",
      contactNumber: form.contactNumber?.trim() || "",
      publicVisibility: form.publicVisibility,
      registrationType: form.registrationType || "Standard",
      termsAndCancellationPolicy: form.termsAndCancellationPolicy || "",
      isEthosOriginal: form.isEthosOriginal ?? false,
      timezone: "Asia/Kolkata",
      status: targetStatus,
      pricingTiers: (Array.isArray(form.passTypes) && form.passTypes.length > 0) ? undefined : formattedTiers,
    };
  };

  const executeSave = async (action = WIZARD_ACTION.DRAFT) => {
    if (isLockedOut) {
      alert("This workshop has already started and cannot be modified.");
      return;
    }

    setSaving(true);
    setStatusBanner(null);

    try {
      const targetStatus =
        action === WIZARD_ACTION.PUBLISH
          ? "Published"
          : action === WIZARD_ACTION.APPROVAL
          ? "PendingApproval"
          : "Draft";

      const payload = await buildPayload(targetStatus);

      if (isEdit) {
        await adminApi.updateWorkshop(effectiveId, payload);
        if (draftIdRef.current) {
          try {
            await adminApi.discardWorkshopDraft(draftIdRef.current);
          } catch {}
        }
        sessionStorage.removeItem(DRAFT_STORAGE_KEY);
        setStatusBanner({
          type: "success",
          text: "Workshop updated successfully.",
        });
        setTimeout(() => {
          navigate(`/admin_portal/workshops/${effectiveId}/overview`);
        }, 1000);
      } else {
        const res = await adminApi.createWorkshop(payload);
        if (draftIdRef.current) {
          try {
            await adminApi.discardWorkshopDraft(draftIdRef.current);
          } catch {}
        }
        sessionStorage.removeItem(DRAFT_STORAGE_KEY);
        const newId = res.id || res.workshopId;
        setStatusBanner({
          type: "success",
          text: "Workshop created successfully!",
        });
        setTimeout(() => {
          if (newId) {
            navigate(`/admin_portal/workshops/${newId}/overview`);
          } else {
            navigate("/admin_portal/workshops");
          }
        }, 1200);
      }
    } catch (err) {
      console.error("Save error:", err);
      const errorMessage = err?.data?.message || err?.data?.Message || err?.message || "Failed to save workshop. Please review inputs.";
      setStatusBanner({
        type: "error",
        text: errorMessage,
      });
      if (typeof window !== "undefined") {
        window.scrollTo({ top: 0, behavior: "smooth" });
      }
    } finally {
      setSaving(false);
    }
  };

  const validationChecklist = {
    details: Boolean(form.title?.trim().length >= 5 && form.trainerProfileId && form.city),
    portrait: Boolean(form.imageUrl),
    venue: Boolean(
      form.venue?.trim() &&
      Array.isArray(form.sessions) &&
      form.sessions.length > 0 &&
      form.sessions.every((s) => s.sessionDate && s.startTime && s.endTime && s.endTime > s.startTime && Number(s.capacity) > 0)
    ),
    ticketTypes: Boolean(
      Array.isArray(form.passTypes) &&
      form.passTypes.length > 0 &&
      form.passTypes.every((p) => {
        if (!p.name?.trim() || p.totalQuantity < 1) return false;
        if (p.workshopSessionId && p.sessionsIncluded !== 1) return false;
        if (!p.workshopSessionId && p.sessionsIncluded != null && Number(p.sessionsIncluded) < 1) return false;
        return true;
      })
    ),
    pricing: Boolean(
      Array.isArray(form.passTypes) &&
      form.passTypes.length > 0 &&
      form.passTypes.every((p) => {
        const tiers = p.pricingTiers;
        if (!Array.isArray(tiers) || tiers.length === 0) return Number(p.price) > 0;
        return (
          tiers[0].minTickets === 1 &&
          tiers.every((t, idx) => {
            if (t.price == null || t.price <= 0) return false;
            if (t.maxTickets != null && t.maxTickets < t.minTickets) return false;
            if (idx < tiers.length - 1 && t.maxTickets == null) return false;
            if (idx > 0) {
              if (t.price < tiers[idx - 1].price) return false;
              if (tiers[idx - 1].maxTickets != null && t.minTickets !== tiers[idx - 1].maxTickets + 1) return false;
            }
            return true;
          })
        );
      })
    ),
  };

  if (loadingWorkshop) {
    return (
      <div className="wizard-loading-screen">
        <div className="wizard-spinner" />
        <p>Loading workshop details...</p>
      </div>
    );
  }

  if (isEdit && loadError && !form.title) {
    return (
      <div className="admin-workshop-wizard-root">
        <div className="wizard-top-bar">
          <div className="wizard-top-left">
            <Link to="/admin_portal/workshops" className="wizard-back-link">
              <ArrowLeft size={16} />
              <span>Back to Workshops</span>
            </Link>
            <div className="wizard-title-group">
              <h1 className="wizard-main-title">Edit Workshop</h1>
              <span className="wizard-mode-pill">Multi-Step Editor</span>
            </div>
          </div>
        </div>

        <div className="wizard-content-container" style={{ textAlign: "center", padding: "80px 24px" }}>
          <div style={{ maxWidth: "480px", margin: "0 auto", display: "flex", flexDirection: "column", alignItems: "center", gap: "16px" }}>
            <div style={{ width: "56px", height: "56px", borderRadius: "50%", background: "rgba(239, 68, 68, 0.1)", display: "flex", alignItems: "center", justifyContent: "center", color: "#ef4444" }}>
              <AlertCircle size={32} />
            </div>
            <h2 style={{ fontSize: "20px", fontWeight: "700", color: "#f8fafc", margin: 0 }}>Unable to Load Workshop</h2>
            <p style={{ color: "#94a3b8", fontSize: "14px", lineHeight: "1.6", margin: 0 }}>
              {loadError}
            </p>
            <div style={{ display: "flex", gap: "12px", marginTop: "12px" }}>
              <button
                type="button"
                className="wizard-btn-next"
                style={{ display: "inline-flex", alignItems: "center", gap: "8px" }}
                onClick={() => window.location.reload()}
              >
                <RefreshCw size={15} />
                <span>Retry</span>
              </button>
              <button
                type="button"
                className="wizard-btn-cancel-draft"
                style={{ display: "inline-flex", alignItems: "center", gap: "8px" }}
                onClick={() => navigate("/admin_portal/workshops")}
              >
                <ArrowLeft size={15} />
                <span>Return to Workshops</span>
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="admin-workshop-wizard-root">
      {/* Top Header Bar */}
      <div className="wizard-top-bar">
        <div className="wizard-top-left">
          <Link to="/admin_portal/workshops" className="wizard-back-link">
            <ArrowLeft size={16} />
            <span>Back to Workshops</span>
          </Link>
          <div className="wizard-title-group">
            <h1 className="wizard-main-title">
              {isEdit ? "Edit Workshop" : "Create New Workshop"}
            </h1>
            <span className="wizard-mode-pill">
              {isEdit ? "Multi-Step Editor" : "Multi-Step Creation Wizard"}
            </span>
          </div>
        </div>

        <div className="wizard-top-actions">
          <button
            type="button"
            className="wizard-btn-draft-top"
            disabled={saving || isAutosaving || isLockedOut}
            onClick={() => performSaveDraft(true)}
            title="Save current progress as draft"
          >
            <Save size={14} />
            <span>
              {isAutosaving
                ? "Saving..."
                : draftVersion > 0
                ? "Draft Saved"
                : "Save as Draft"}
            </span>
          </button>
        </div>
      </div>

      {/* Global Status / Alert Banner */}
      {statusBanner && (
        <div className={`wizard-status-banner ${statusBanner.type}`}>
          {statusBanner.type === "warning" && <AlertTriangle size={16} />}
          {statusBanner.type === "success" && <CheckCircle2 size={16} />}
          {statusBanner.type === "error" && <AlertCircle size={16} />}
          <span>{statusBanner.text}</span>
        </div>
      )}

      {/* Stepper Navigation Indicator */}
      <div className="wizard-stepper-container">
        <div className="wizard-stepper-track">
          {STEPS.map((step) => {
            const Icon = step.icon;
            const isDone = currentStep > step.id;
            const isCurrent = currentStep === step.id;

            return (
              <div
                key={step.id}
                className={`stepper-node ${isCurrent ? "current" : ""} ${isDone ? "completed" : ""}`}
                role="tab"
                tabIndex={0}
                aria-selected={isCurrent}
                aria-label={`Step ${step.id}: ${step.label}${isDone ? " (Completed)" : isCurrent ? " (Current)" : ""}`}
                onClick={() => {
                  if (step.id < currentStep || validateStep(currentStep)) {
                    setCurrentStep(step.id);
                  }
                }}
                onKeyDown={(e) => {
                  if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    if (step.id < currentStep || validateStep(currentStep)) {
                      setCurrentStep(step.id);
                    }
                  }
                }}
              >
                <div className="stepper-circle">
                  {isDone ? <Check size={14} /> : <Icon size={14} />}
                </div>
                <span className="stepper-label">{step.label}</span>
              </div>
            );
          })}
        </div>
      </div>

      {/* Main Form Content Area */}
      <div className="wizard-content-container">
        {currentStep === 1 && (
          <Step1Details
            form={form}
            onChange={handleFieldChange}
            trainers={trainers}
            loadingTrainers={loadingTrainers}
            errors={errors}
          />
        )}

        {currentStep === 2 && (
          <Step2Photos
            form={form}
            onChange={handleFieldChange}
            portraitBlob={portraitBlob}
            setPortraitBlob={setPortraitBlob}
            landscapeBlob={landscapeBlob}
            setLandscapeBlob={setLandscapeBlob}
            errors={errors}
          />
        )}

        {currentStep === 3 && (
          <Step3VenueSchedule form={form} onChange={handleFieldChange} trainers={trainers} errors={errors} />
        )}

        {currentStep === 4 && (
          <Step4TicketTypes form={form} onChange={handleFieldChange} trainers={trainers} errors={errors} />
        )}

        {currentStep === 5 && (
          <Step5TicketPricing form={form} onChange={handleFieldChange} errors={errors} />
        )}

        {currentStep === 6 && (
          <Step6Review
            form={form}
            trainers={trainers}
            isEdit={isEdit}
            saving={saving}
            onSaveDraft={() => executeSave(WIZARD_ACTION.DRAFT)}
            onSubmitApproval={() => executeSave(WIZARD_ACTION.APPROVAL)}
            onPublishNow={() => executeSave(WIZARD_ACTION.PUBLISH)}
            validationChecklist={validationChecklist}
            onGoToStep={(step) => setCurrentStep(step)}
          />
        )}
      </div>

      {/* Bottom Sticky Action Footer (Steps 1 to 5; Step 6 has dedicated review action bar) */}
      {currentStep < 6 && (
        <div className="wizard-sticky-footer">
          <div className="wizard-footer-inner">
            <div className="wizard-footer-left">
              <button
                type="button"
                className="wizard-btn-prev"
                disabled={currentStep === 1 || saving}
                onClick={handleBack}
              >
                <ChevronLeft size={16} />
                <span>Previous</span>
              </button>

              <button
                type="button"
                className="wizard-btn-cancel-exit"
                disabled={saving}
                onClick={async () => {
                  if (!isEdit) {
                    if (window.confirm("Do you want to discard this draft?\n\n• Click OK to permanently discard this draft and exit\n• Click Cancel to keep working on this workshop")) {
                      try {
                        const idToDiscard = draftId || serverDraft?.id;
                        if (idToDiscard) {
                          await adminApi.discardWorkshopDraft(idToDiscard);
                        }
                        sessionStorage.removeItem(DRAFT_STORAGE_KEY);
                        navigate("/admin_portal/workshops");
                      } catch (err) {
                        alert(err.message || "Failed to discard draft.");
                      }
                    }
                  } else {
                    if (window.confirm("Exit editor? Any unsaved edits will be lost.")) {
                      navigate("/admin_portal/workshops");
                    }
                  }
                }}
              >
                <XCircle size={15} />
                <span>{isEdit ? "Exit Editor" : "Cancel Draft"}</span>
              </button>
            </div>

            <div className="wizard-footer-step-counter">
              Step {currentStep} of {STEPS.length}
            </div>

            <div className="wizard-footer-right">
              <button
                type="button"
                className="wizard-btn-save-draft"
                disabled={saving || isAutosaving || isLockedOut}
                onClick={async () => {
                  await performSaveDraft(true);
                }}
                title="Save current progress as draft and return later"
              >
                <Save size={15} />
                <span>{isAutosaving ? "Saving..." : "Save as Draft"}</span>
              </button>

              <button
                type="button"
                className="wizard-btn-next"
                onClick={handleNext}
                disabled={isLockedOut}
              >
                <span>Next Step</span>
                <ChevronRight size={16} />
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Resume Unsaved Draft Modal */}
      {showResumeModal && serverDraft && (
        <div className="wizard-modal-overlay" role="dialog" aria-modal="true" aria-labelledby="resume-draft-title">
          <div className="wizard-modal-card">
            <div className="wizard-modal-header">
              <FileText size={22} color="#FF5500" />
              <h3 id="resume-draft-title" className="wizard-modal-title">
                Unsaved Draft Found
              </h3>
            </div>
            <div className="wizard-modal-body">
              <p>
                You have an unsaved draft from <strong>{new Date(serverDraft.updatedAt).toLocaleString()}</strong>.
              </p>
              <p>
                Would you like to resume editing this draft or discard it to start fresh?
              </p>
            </div>
            <div className="wizard-modal-actions">
              <button
                type="button"
                className="wizard-modal-btn-discard"
                onClick={handleDiscardDraft}
              >
                Discard Draft
              </button>
              <button
                type="button"
                className="wizard-modal-btn-primary"
                onClick={handleResumeDraft}
              >
                Resume Draft
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Concurrency Conflict Modal */}
      {conflictModal && (
        <div className="wizard-modal-overlay" role="dialog" aria-modal="true" aria-labelledby="conflict-modal-title">
          <div className="wizard-modal-card">
            <div className="wizard-modal-header">
              <AlertTriangle size={22} color="#dc2626" />
              <h3 id="conflict-modal-title" className="wizard-modal-title" style={{ color: "#dc2626" }}>
                Draft Concurrency Conflict
              </h3>
            </div>
            <div className="wizard-modal-body">
              <p>
                {conflictModal.message || "This workshop draft was modified from another browser tab or session."}
              </p>
              <p>
                To prevent accidental data loss, please choose whether to overwrite the server version with your current edits or discard your local changes and reload the latest version from the server.
              </p>
            </div>
            <div className="wizard-modal-actions">
              <button
                type="button"
                className="wizard-modal-btn-cancel"
                onClick={handleReloadServerVersion}
              >
                Reload Server Version
              </button>
              <button
                type="button"
                className="wizard-modal-btn-primary"
                onClick={handleForceOverwrite}
                style={{ background: "#dc2626", borderColor: "#dc2626" }}
              >
                Overwrite Server Version
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
