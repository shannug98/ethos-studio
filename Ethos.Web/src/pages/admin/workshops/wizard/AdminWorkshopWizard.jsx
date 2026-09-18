import React, { useState, useEffect } from "react";
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
} from "lucide-react";
import { adminApi } from "../../../../services/adminApi";
import Step1Details, { DANCE_STYLE_PRESETS } from "./Step1Details";
import Step2Photos from "./Step2Photos";
import Step3VenueSchedule from "./Step3VenueSchedule";
import Step4Pricing from "./Step4Pricing";
import Step5Review from "./Step5Review";
import "./AdminWorkshopWizard.css";

const DRAFT_STORAGE_KEY = "ethos_admin_workshop_wizard_draft";

const STEPS = [
  { id: 1, label: "Workshop Details", icon: FileText },
  { id: 2, label: "Photos", icon: ImageIcon },
  { id: 3, label: "Venue & Schedule", icon: MapPin },
  { id: 4, label: "Tickets & Pricing", icon: IndianRupee },
  { id: 5, label: "Review & Publish", icon: Sparkles },
];

const INITIAL_FORM = {
  title: "",
  description: "",
  shortDescription: "",
  danceStyle: "Urban Choreography",
  customStyle: "",
  level: "Open Level",
  workshopDate: "",
  startTime: "18:00",
  endTime: "19:30",
  venue: "Ethos Main Studio A",
  venueAddress: "Plot 42, Road No 36, Jubilee Hills, Hyderabad",
  city: "Hyderabad",
  area: "Jubilee Hills",
  googlePlaceId: "ethos_main_a",
  latitude: 17.4319,
  longitude: 78.4073,
  price: 500,
  capacity: 35,
  trainerProfileId: "",
  imageUrl: "",
  landscapeImageUrl: "",
  contactPerson: "",
  contactNumber: "",
  publicVisibility: true,
  registrationType: "Standard",
  termsAndCancellationPolicy: "",
  status: 3, // 3: Approved, 1: Draft, 2: PendingApproval
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
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState({});
  const [statusBanner, setStatusBanner] = useState(null);
  const [isLockedOut, setIsLockedOut] = useState(false);

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
        try {
          const ws = await adminApi.getWorkshopById(effectiveId);
          if (!mounted) return;

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
          setForm({
            title: ws.title || "",
            description: ws.description || "",
            shortDescription: ws.shortDescription || "",
            danceStyle: isPresetStyle ? ws.danceStyle : "Other",
            customStyle: isPresetStyle ? "" : ws.danceStyle || "",
            level: ws.level || "Open Level",
            workshopDate: ws.workshopDate ? ws.workshopDate.slice(0, 10) : "",
            startTime: ws.startTime ? ws.startTime.slice(0, 5) : "18:00",
            endTime: ws.endTime ? ws.endTime.slice(0, 5) : "19:30",
            venue: ws.venue || "Ethos Main Studio",
            venueAddress: ws.venueAddress || "",
            city: ws.city || "Hyderabad",
            area: ws.area || "",
            googlePlaceId: ws.googlePlaceId || "",
            latitude: ws.latitude || null,
            longitude: ws.longitude || null,
            price: ws.price ?? 500,
            capacity: ws.capacity ?? 35,
            trainerProfileId: ws.trainerProfileId || "",
            imageUrl: ws.imageUrl || "",
            landscapeImageUrl: ws.landscapeImageUrl || "",
            contactPerson: ws.contactPerson || "",
            contactNumber: ws.contactNumber || "",
            publicVisibility: ws.publicVisibility ?? true,
            registrationType: ws.registrationType || "Standard",
            termsAndCancellationPolicy: ws.termsAndCancellationPolicy || "",
            status: ws.status === "Draft" ? 1 : ws.status === "PendingApproval" ? 2 : 3,
          });
        } catch (err) {
          setStatusBanner({
            type: "error",
            text: err?.message || "Failed to load workshop data.",
          });
        } finally {
          if (mounted) setLoadingWorkshop(false);
        }
      }
      loadWorkshopData();
      return () => {
        mounted = false;
      };
    } else {
      // Restore draft if present
      try {
        const saved = sessionStorage.getItem(DRAFT_STORAGE_KEY);
        if (saved) {
          const parsed = JSON.parse(saved);
          setForm((prev) => ({ ...prev, ...parsed }));
        }
      } catch {
        // ignore storage parse errors
      }
    }
  }, [isEdit, effectiveId]);

  // Persist draft on changes in create mode
  useEffect(() => {
    if (!isEdit) {
      try {
        sessionStorage.setItem(DRAFT_STORAGE_KEY, JSON.stringify(form));
      } catch {
        // ignore
      }
    }
  }, [form, isEdit]);

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
        errs.trainerProfileId = "Please select a lead trainer.";
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
      if (!form.workshopDate) {
        errs.workshopDate = "Workshop date is required.";
      }
      if (!form.startTime) {
        errs.startTime = "Start time is required.";
      }
      if (!form.endTime) {
        errs.endTime = "End time is required.";
      }
      if (form.startTime && form.endTime && form.startTime >= form.endTime) {
        errs.endTime = "End time must be after start time.";
      }
    }

    if (stepNum === 4) {
      if (!form.capacity || form.capacity < 5) {
        errs.capacity = "Minimum capacity is 5 attendees.";
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

    return { finalImageUrl, finalLandscapeUrl };
  };

  // Payload builder
  const buildPayload = async (statusCode) => {
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

    return {
      title: form.title.trim(),
      description: form.description?.trim() || "",
      shortDescription: form.shortDescription?.trim() || "",
      danceStyle: actualStyle,
      level: form.level,
      workshopDate: form.workshopDate,
      startTime: form.startTime.length === 5 ? `${form.startTime}:00` : form.startTime,
      endTime: form.endTime.length === 5 ? `${form.endTime}:00` : form.endTime,
      venue: form.venue.trim(),
      venueAddress: form.venueAddress?.trim() || "",
      city: form.city?.trim() || "Hyderabad",
      area: form.area?.trim() || "",
      googlePlaceId: form.googlePlaceId || "",
      latitude: form.latitude,
      longitude: form.longitude,
      price: Number(form.price) || (formattedTiers && formattedTiers[0] ? formattedTiers[0].price : 500),
      capacity: Number(form.capacity) || 35,
      trainerProfileId: form.trainerProfileId && form.trainerProfileId !== "none" ? form.trainerProfileId : null,
      imageUrl: finalImageUrl,
      landscapeImageUrl: finalLandscapeUrl,
      contactPerson: form.contactPerson?.trim() || "",
      contactNumber: form.contactNumber?.trim() || "",
      publicVisibility: form.publicVisibility,
      registrationType: form.registrationType || "Standard",
      termsAndCancellationPolicy: form.termsAndCancellationPolicy || "",
      timezone: "Asia/Kolkata",
      status: statusCode, // 1: Draft, 2: PendingApproval, 3: Approved
      pricingTiers: formattedTiers,
    };
  };

  const executeSave = async (statusCode) => {
    if (isLockedOut) {
      alert("This workshop has already started and cannot be modified.");
      return;
    }

    setSaving(true);
    setStatusBanner(null);

    try {
      const payload = await buildPayload(statusCode);

      if (isEdit) {
        await adminApi.updateWorkshop(effectiveId, payload);
        setStatusBanner({
          type: "success",
          text: "Workshop updated successfully.",
        });
        setTimeout(() => {
          navigate(`/admin_portal/workshops/${effectiveId}/overview`);
        }, 1000);
      } else {
        const res = await adminApi.createWorkshop(payload);
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
      setStatusBanner({
        type: "error",
        text: err?.message || "Failed to save workshop. Please review inputs.",
      });
    } finally {
      setSaving(false);
    }
  };

  const validationChecklist = {
    details: Boolean(form.title?.trim().length >= 5 && form.trainerProfileId && form.city),
    portrait: Boolean(form.imageUrl),
    venue: Boolean(form.venue?.trim() && form.workshopDate && form.startTime && form.endTime),
    capacity: Boolean(form.capacity >= 5),
  };

  if (loadingWorkshop) {
    return (
      <div className="wizard-loading-screen">
        <div className="wizard-spinner" />
        <p>Loading workshop details...</p>
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
          {!isEdit && (
            <button
              type="button"
              className="wizard-btn-draft-top"
              disabled={saving || isLockedOut}
              onClick={() => executeSave(1)}
            >
              <Save size={14} />
              <span>Save Draft</span>
            </button>
          )}
        </div>
      </div>

      {/* Global Status / Alert Banner */}
      {statusBanner && (
        <div className={`wizard-status-banner ${statusBanner.type}`}>
          {statusBanner.type === "warning" && <AlertTriangle size={16} />}
          {statusBanner.type === "success" && <CheckCircle2 size={16} />}
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
                onClick={() => {
                  if (step.id < currentStep || validateStep(currentStep)) {
                    setCurrentStep(step.id);
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
          <Step3VenueSchedule form={form} onChange={handleFieldChange} errors={errors} />
        )}

        {currentStep === 4 && (
          <Step4Pricing form={form} onChange={handleFieldChange} errors={errors} />
        )}

        {currentStep === 5 && (
          <Step5Review
            form={form}
            trainers={trainers}
            isEdit={isEdit}
            saving={saving}
            onSaveDraft={() => executeSave(1)}
            onSubmitApproval={() => executeSave(2)}
            onPublishNow={() => executeSave(3)}
            validationChecklist={validationChecklist}
          />
        )}
      </div>

      {/* Bottom Sticky Action Footer */}
      <div className="wizard-sticky-footer">
        <div className="wizard-footer-inner">
          <button
            type="button"
            className="wizard-btn-prev"
            disabled={currentStep === 1 || saving}
            onClick={handleBack}
          >
            <ChevronLeft size={16} />
            <span>Previous</span>
          </button>

          <div className="wizard-footer-step-counter">
            Step {currentStep} of {STEPS.length}
          </div>

          {currentStep < STEPS.length ? (
            <button
              type="button"
              className="wizard-btn-next"
              onClick={handleNext}
              disabled={isLockedOut}
            >
              <span>Next Step</span>
              <ChevronRight size={16} />
            </button>
          ) : (
            <button
              type="button"
              className="wizard-btn-next primary-glow"
              disabled={saving || isLockedOut}
              onClick={() => executeSave(3)}
            >
              <Sparkles size={16} />
              <span>{saving ? "Publishing..." : isEdit ? "Update & Publish" : "Publish Workshop"}</span>
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
