import React, { useState, useEffect, useRef, useCallback } from "react";
import { useOutletContext, Link, useNavigate } from "react-router-dom";
import { Html5Qrcode, Html5QrcodeSupportedFormats } from "html5-qrcode";
import { adminApi } from "../../../services/adminApi";
import "./AdminWorkshopScanner.css";

// Synthesizer chime for instant audio confirmation
const playScanChime = (isSuccess) => {
  try {
    const AudioCtx = window.AudioContext || window.webkitAudioContext;
    if (!AudioCtx) return;
    const ctx = new AudioCtx();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.connect(gain);
    gain.connect(ctx.destination);

    if (isSuccess) {
      osc.type = "sine";
      osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
      osc.frequency.setValueAtTime(880, ctx.currentTime + 0.08); // A5
      gain.gain.setValueAtTime(0.25, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.3);
      osc.start();
      osc.stop(ctx.currentTime + 0.3);
    } else {
      osc.type = "sawtooth";
      osc.frequency.setValueAtTime(220, ctx.currentTime); // A3
      osc.frequency.setValueAtTime(146.83, ctx.currentTime + 0.1); // D3
      gain.gain.setValueAtTime(0.25, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.35);
      osc.start();
      osc.stop(ctx.currentTime + 0.35);
    }
  } catch {
    // Audio context may be restricted by user agent policies
  }
};

export default function AdminWorkshopScanner() {
  const { workshop, reloadWorkshop } = useOutletContext();
  const navigate = useNavigate();
  const workshopId = workshop.Id || workshop.id;
  const sessions = workshop.Sessions || workshop.sessions || [];

  // Scanner states
  const [isScanning, setIsScanning] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [cameraError, setCameraError] = useState(null);
  const [availableCameras, setAvailableCameras] = useState([]);
  const [selectedCameraId, setSelectedCameraId] = useState(null);
  const [torchOn, setTorchOn] = useState(false);

  // Manual input & file upload state
  const [manualTicketInput, setManualTicketInput] = useState("");
  const [manualSubmitting, setManualSubmitting] = useState(false);
  const fileInputRef = useRef(null);

  // Detailed Verification & Feedback
  const [lastScanResult, setLastScanResult] = useState(null);
  const [feedback, setFeedback] = useState(null);
  const [recentCheckIns, setRecentCheckIns] = useState(
    workshop.RecentCheckIns || workshop.recentCheckIns || []
  );

  const html5QrCodeRef = useRef(null);
  const isMountedRef = useRef(true);
  const isTransitioningRef = useRef(false);

  // Safe scanner unmount cleanup
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
      if (html5QrCodeRef.current) {
        const scanner = html5QrCodeRef.current;
        html5QrCodeRef.current = null;
        try {
          if (scanner.isScanning) {
            scanner
              .stop()
              .then(() => {
                try {
                  scanner.clear();
                } catch {}
              })
              .catch(() => {
                try {
                  scanner.clear();
                } catch {}
              });
          } else {
            try {
              scanner.clear();
            } catch {}
          }
        } catch {
          // Ignore cleanup errors on unmount
        }
      }
    };
  }, []);

  const stopCameraScanner = useCallback(async () => {
    if (isTransitioningRef.current) return;
    isTransitioningRef.current = true;
    if (html5QrCodeRef.current) {
      const scanner = html5QrCodeRef.current;
      html5QrCodeRef.current = null;
      try {
        if (scanner.isScanning) {
          await scanner.stop();
        }
        try {
          scanner.clear();
        } catch {}
      } catch (err) {
        console.warn("Could not cleanly stop camera scanner:", err);
      }
    }
    if (isMountedRef.current) {
      setIsScanning(false);
      setTorchOn(false);
    }
    isTransitioningRef.current = false;
  }, []);

  const handleCheckInResult = useCallback(
    async (payload) => {
      if (isProcessing) return;
      setIsProcessing(true);

      try {
        const res = await adminApi.checkInWorkshopTicket(workshopId, payload);

        if (res && res.success) {
          playScanChime(true);
          const scanInfo = {
            type: "success",
            status: "success",
            code: "SUCCESS",
            title: "Check-in Approved!",
            message: res.message || `${res.attendeeName || "Attendee"} checked in to ${workshop.Title || workshop.title}.`,
            ticketNumber: res.ticketNumber || payload.ticketNumber,
            attendeeName: res.attendeeName,
            workshopTitle: res.workshopTitle || workshop.Title || workshop.title,
            time: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" }),
            checkInMethod: res.checkInMethod || (payload.qrToken ? "QR Scan" : "Manual Entry"),
          };
          setLastScanResult(scanInfo);
          setFeedback(scanInfo);

          // Add to top of recent check-ins
          setRecentCheckIns((prev) => [
            {
              ticketId: res.ticketId,
              attendeeName: res.attendeeName,
              ticketNumber: res.ticketNumber,
              formattedTime: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
              checkInMethod: res.checkInMethod || (payload.qrToken ? "QR" : "Manual"),
            },
            ...prev.slice(0, 14),
          ]);

          // Refresh workshop overview stats in background
          if (reloadWorkshop) reloadWorkshop();
        } else {
          playScanChime(false);
          const isWrongWorkshop = res?.code === "WRONG_WORKSHOP";
          const isAlreadyCheckedIn = res?.code === "ALREADY_CHECKED_IN";
          const scanInfo = {
            type: "error",
            status: isWrongWorkshop ? "wrong_workshop" : isAlreadyCheckedIn ? "already_checked_in" : "error",
            code: res?.code || "CHECKIN_FAILED",
            title: isWrongWorkshop ? "Wrong Workshop Ticket" : isAlreadyCheckedIn ? "Ticket Already Checked In" : "Check-in Denied",
            message: res?.message || "Check-in validation failed.",
            ticketNumber: res?.ticketNumber || payload.ticketNumber || (payload.qrToken ? (payload.qrToken.length > 28 ? payload.qrToken.slice(0, 24) + "..." : payload.qrToken) : "N/A"),
            attendeeName: res?.attendeeName || null,
            workshopTitle: res?.workshopTitle || (isWrongWorkshop ? "Another Workshop" : null),
            targetWorkshopId: res?.workshopId || null,
            time: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" }),
            checkInMethod: payload.qrToken ? "QR Scan" : "Manual Entry",
          };
          setLastScanResult(scanInfo);
          setFeedback(scanInfo);
        }
      } catch (err) {
        playScanChime(false);
        const scanInfo = {
          type: "error",
          status: "error",
          code: "NETWORK_ERROR",
          title: "Connection Error",
          message: err?.message || "Failed to contact verification server.",
          ticketNumber: payload.ticketNumber || (payload.qrToken ? payload.qrToken.slice(0, 24) : "N/A"),
          time: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" }),
        };
        setLastScanResult(scanInfo);
        setFeedback(scanInfo);
      } finally {
        // Debounce lock so scanner is ready for next scan without immediate duplicate trigger
        setTimeout(() => {
          if (isMountedRef.current) {
            setIsProcessing(false);
          }
        }, 1600);
      }
    },
    [isProcessing, workshopId, workshop, reloadWorkshop]
  );

  const startCameraScanner = async () => {
    if (isTransitioningRef.current) return;
    isTransitioningRef.current = true;
    setCameraError(null);
    setFeedback(null);

    try {
      // Ensure previous scanner is cleanly stopped
      if (html5QrCodeRef.current) {
        const prevScanner = html5QrCodeRef.current;
        html5QrCodeRef.current = null;
        try {
          if (prevScanner.isScanning) {
            await prevScanner.stop();
          }
          prevScanner.clear();
        } catch {}
      }

      const scanner = new Html5Qrcode("qr-camera-viewport", {
        formatsToSupport: [Html5QrcodeSupportedFormats.QR_CODE],
        verbose: false,
      });
      html5QrCodeRef.current = scanner;

      const cameraConfig = selectedCameraId
        ? { deviceId: { exact: selectedCameraId } }
        : { facingMode: "environment" };

      await scanner.start(
        cameraConfig,
        {
          fps: 20,
          qrbox: (viewfinderWidth, viewfinderHeight) => {
            const minEdge = Math.min(viewfinderWidth, viewfinderHeight);
            const edge = Math.floor(minEdge * 0.75);
            return { width: Math.max(180, edge), height: Math.max(180, edge) };
          },
          aspectRatio: 1.0,
          disableFlip: false,
          experimentalFeatures: {
            useBarCodeDetectorIfSupported: true,
          },
        },
        (decodedText) => {
          handleCheckInResult({ qrToken: decodedText });
        },
        () => {
          // Parse tick
        }
      );

      if (isMountedRef.current) {
        setIsScanning(true);

        // Ensure video element has playsInline and muted for iOS/mobile compatibility
        try {
          const videoEl = document.querySelector("#qr-camera-viewport video");
          if (videoEl) {
            videoEl.setAttribute("playsinline", "true");
            videoEl.setAttribute("webkit-playsinline", "true");
            videoEl.muted = true;
          }
        } catch {}

        // Enumerate available cameras once user has granted permissions
        try {
          const devices = await Html5Qrcode.getCameras();
          if (devices && devices.length > 0) {
            setAvailableCameras(devices);
            if (!selectedCameraId) {
              const backCam = devices.find(
                (c) =>
                  c.label &&
                  (c.label.toLowerCase().includes("back") ||
                    c.label.toLowerCase().includes("rear") ||
                    c.label.toLowerCase().includes("environment"))
              );
              if (backCam) {
                setSelectedCameraId(backCam.id);
              }
            }
          }
        } catch {
          // Device enumeration warning
        }
      }
    } catch (err) {
      if (isMountedRef.current) {
        setIsScanning(false);
        console.error("Camera start error:", err);
        let errorMsg = "Failed to access camera.";
        const errStr = String(err?.name || err?.message || err).toLowerCase();
        if (errStr.includes("notallowed") || errStr.includes("permission")) {
          errorMsg =
            "Camera permission was denied. Please allow camera access in your browser settings, or use manual ticket entry / photo upload below.";
        } else if (errStr.includes("notfound") || errStr.includes("devicesnotfound")) {
          errorMsg =
            "No camera device was detected on your system. Please use manual ticket entry or upload a ticket photo below.";
        } else if (
          errStr.includes("notreadable") ||
          errStr.includes("trackstart") ||
          errStr.includes("in use")
        ) {
          errorMsg =
            "Camera is already in use by another application or browser tab. Please close other camera apps and try again.";
        } else if (errStr.includes("overconstrained")) {
          errorMsg =
            "Camera resolution or facing constraint could not be satisfied. Please select another camera from the dropdown.";
        } else if (typeof window !== "undefined" && window.isSecureContext === false) {
          errorMsg =
            "Camera access requires a secure HTTPS connection (or localhost). Please check your browser URL.";
        } else {
          errorMsg = `Camera error: ${err?.message || "Could not start video stream."}. You can use manual entry or file upload below.`;
        }
        setCameraError(errorMsg);
      }
    } finally {
      isTransitioningRef.current = false;
    }
  };

  const handleSwitchCamera = async () => {
    if (availableCameras.length <= 1 || isTransitioningRef.current) return;
    const currentIndex = availableCameras.findIndex((c) => c.id === selectedCameraId);
    const nextIndex = (currentIndex + 1) % availableCameras.length;
    const nextCamera = availableCameras[nextIndex];
    setSelectedCameraId(nextCamera.id);

    if (isScanning) {
      await stopCameraScanner();
      setTimeout(() => {
        if (isMountedRef.current) {
          startCameraScanner();
        }
      }, 300);
    }
  };

  const handleToggleTorch = async () => {
    if (html5QrCodeRef.current && isScanning) {
      try {
        const newTorch = !torchOn;
        await html5QrCodeRef.current.applyVideoConstraints({
          advanced: [{ torch: newTorch }],
        });
        setTorchOn(newTorch);
      } catch {
        // Torch not supported on this camera/browser
      }
    }
  };

  const handleManualSubmit = async (e) => {
    e.preventDefault();
    if (!manualTicketInput.trim()) return;
    setManualSubmitting(true);
    await handleCheckInResult({ ticketNumber: manualTicketInput.trim() });
    setManualTicketInput("");
    setManualSubmitting(false);
  };

  const handleFileUpload = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    if (isProcessing) return;
    setIsProcessing(true);
    setCameraError(null);

    try {
      if (isScanning) {
        await stopCameraScanner();
      }
      let scanner = html5QrCodeRef.current;
      if (!scanner) {
        scanner = new Html5Qrcode("qr-camera-viewport");
        html5QrCodeRef.current = scanner;
      }
      const decodedText = await scanner.scanFile(file, false);
      if (decodedText) {
        await handleCheckInResult({ qrToken: decodedText });
      } else {
        throw new Error("No QR found");
      }
    } catch (err) {
      console.warn("File scan error:", err);
      setFeedback({
        type: "error",
        code: "IMAGE_SCAN_FAILED",
        title: "No QR Code Detected",
        message:
          "Could not detect a valid QR code in the uploaded image. Please ensure the ticket QR is clearly visible or enter the ticket number manually.",
      });
    } finally {
      setIsProcessing(false);
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
    }
  };

  return (
    <div className="ethos-workshop-scanner-page">
      {/* Top Action Bar */}
      <div className="scanner-header-row">
        <div className="scanner-titles">
          <h1 className="scanner-page-title">QR Scanner</h1>
          <p className="scanner-page-subtitle">
            Scan attendee tickets for <strong>{workshop.Title || workshop.title}</strong>
          </p>
        </div>

        <div className="scanner-header-actions">
          <span className="session-active-pill">● Session Active</span>
          <button
            type="button"
            className="view-attendees-link-btn"
            onClick={() => navigate(`/admin_portal/workshops/${workshopId}/bookings-attendees?view=attendees`)}
          >
            📋 View Attendees
          </button>
        </div>
      </div>

      {/* Multi-Session Indicator */}
      {sessions && sessions.length > 0 && (
        <div className="scanner-sessions-info">
          <span className="scanner-sessions-label">Workshop Sessions ({sessions.length}):</span>
          <div className="scanner-sessions-tags">
            {sessions.map((s, idx) => (
              <span key={s.id || idx} className="scanner-session-tag">
                {s.title || `Session ${idx + 1}`} · {s.sessionDate || s.date || "Date TBD"}
                {s.startTime ? ` (${s.startTime} - ${s.endTime})` : ""}
              </span>
            ))}
          </div>
        </div>
      )}

      {/* Main 2-Column Grid */}
      <div className="scanner-main-layout">
        {/* Left Column: Viewfinder & Camera Controls */}
        <div className="scanner-camera-column">
          <div className="camera-card">
            {/* Viewfinder Canvas Wrap */}
            <div className="viewfinder-container">
              <div id="qr-camera-viewport" className="qr-viewport-canvas" />

              {/* Viewfinder Visual Frame when scanning */}
              {isScanning ? (
                <div className="viewfinder-overlay">
                  <div className="viewfinder-box">
                    <div className="corner-bracket top-left" />
                    <div className="corner-bracket top-right" />
                    <div className="corner-bracket bottom-left" />
                    <div className="corner-bracket bottom-right" />
                    <div className="scanning-laser-line" />
                  </div>

                  <div className="viewport-status-badge">
                    <span className="scan-radar-dot" />
                    <span>Scanning for {workshop.Title || workshop.title}</span>
                  </div>

                  <div className="viewport-instruction-badge">
                    Position QR code within the frame
                  </div>
                </div>
              ) : (
                <div className="viewfinder-idle-placeholder">
                  <div className="idle-camera-icon">📷</div>
                  <h3 className="idle-camera-title">Camera Scanner Ready</h3>
                  <p className="idle-camera-desc">
                    Click the button below to start your camera and begin scanning attendee tickets.
                  </p>
                  <button
                    type="button"
                    className="scanner-start-primary-btn"
                    onClick={startCameraScanner}
                  >
                    ▶ Start Camera Scanner
                  </button>
                </div>
              )}

              {/* Viewport Control Buttons (Flashlight & Switch) */}
              {isScanning && (
                <div className="viewport-controls-bar">
                  <button
                    type="button"
                    className={`viewport-control-btn ${torchOn ? "active" : ""}`}
                    onClick={handleToggleTorch}
                    title="Toggle Flashlight / Torch"
                  >
                    🔦
                  </button>
                  {availableCameras.length > 1 && (
                    <button
                      type="button"
                      className="viewport-control-btn"
                      onClick={handleSwitchCamera}
                      title="Switch Camera"
                    >
                      🔄
                    </button>
                  )}
                </div>
              )}
            </div>

            {/* Camera Start / Stop Controls */}
            <div className="camera-action-footer">
              {!isScanning ? (
                <button
                  type="button"
                  className="scanner-btn-primary"
                  onClick={startCameraScanner}
                >
                  ▶ Start Scanner
                </button>
              ) : (
                <button
                  type="button"
                  className="scanner-btn-danger"
                  onClick={stopCameraScanner}
                >
                  ⏹ Stop Camera
                </button>
              )}

              {availableCameras.length > 1 && (
                <select
                  className="camera-select-dropdown"
                  value={selectedCameraId || ""}
                  onChange={(e) => setSelectedCameraId(e.target.value)}
                  disabled={isScanning}
                >
                  {availableCameras.map((cam, idx) => (
                    <option key={cam.id} value={cam.id}>
                      {cam.label || `Camera ${idx + 1}`}
                    </option>
                  ))}
                </select>
              )}
            </div>

            {cameraError && <div className="scanner-error-box">{cameraError}</div>}
          </div>

          {/* Real-time Verification Alert directly under Camera Viewport */}
          {lastScanResult && (
            <div className={`scanner-live-alert alert-${lastScanResult.status}`}>
              <div className="live-alert-header">
                <span className="live-alert-icon">
                  {lastScanResult.status === "success" ? "✓" : lastScanResult.status === "wrong_workshop" ? "⚠️" : "✕"}
                </span>
                <div className="live-alert-heading-wrap">
                  <span className={`live-alert-badge badge-${lastScanResult.status}`}>
                    {lastScanResult.status === "success" ? "Valid Check-In" : lastScanResult.status === "wrong_workshop" ? "Cross-Workshop Warning" : "Scan Error"}
                  </span>
                  <h4 className="live-alert-title">{lastScanResult.title}</h4>
                  <p className="live-alert-msg">{lastScanResult.message}</p>
                </div>
              </div>

              <div className="live-alert-details-grid">
                <div className="live-detail-item">
                  <span className="live-detail-label">Ticket #</span>
                  <span className="live-detail-value mono">{lastScanResult.ticketNumber}</span>
                </div>
                {lastScanResult.attendeeName && (
                  <div className="live-detail-item">
                    <span className="live-detail-label">Attendee</span>
                    <span className="live-detail-value bold">{lastScanResult.attendeeName}</span>
                  </div>
                )}
                {lastScanResult.workshopTitle && (
                  <div className="live-detail-item">
                    <span className="live-detail-label">Ticket For</span>
                    <span className="live-detail-value highlight">{lastScanResult.workshopTitle}</span>
                  </div>
                )}
                <div className="live-detail-item">
                  <span className="live-detail-label">Time</span>
                  <span className="live-detail-value">{lastScanResult.time}</span>
                </div>
              </div>

              {lastScanResult.status === "wrong_workshop" && lastScanResult.targetWorkshopId && (
                <div className="live-alert-actions">
                  <button
                    type="button"
                    className="live-alert-switch-btn"
                    onClick={() => navigate(`/admin_portal/workshops/${lastScanResult.targetWorkshopId}/scanner`)}
                  >
                    Switch to {lastScanResult.workshopTitle} Scanner ➔
                  </button>
                </div>
              )}
            </div>
          )}

          {/* Security Banner */}
          <div className="scanner-security-banner">
            <span className="banner-info-icon">ℹ</span>
            <div className="banner-text">
              <strong>Only tickets for this workshop can be scanned.</strong>
              <p>Tickets from other workshops or invalid passes will be rejected automatically.</p>
            </div>
          </div>

          {/* Manual Entry Fallback & Image Upload Drawer */}
          <div className="manual-entry-card">
            <h4 className="manual-entry-title">Manual Ticket Check-In Fallback</h4>
            <p className="manual-entry-desc">
              If a student's screen is broken, camera access is unavailable, or the QR code is unreadable:
            </p>
            <form onSubmit={handleManualSubmit} className="manual-entry-form">
              <input
                type="text"
                placeholder="e.g. ETHOS-WKS-..."
                className="manual-input"
                value={manualTicketInput}
                onChange={(e) => setManualTicketInput(e.target.value)}
                disabled={manualSubmitting || isProcessing}
              />
              <button
                type="submit"
                className="manual-submit-btn"
                disabled={manualSubmitting || isProcessing || !manualTicketInput.trim()}
              >
                {manualSubmitting ? "Validating..." : "Check In"}
              </button>
            </form>

            <div className="scanner-upload-divider">
              <span>OR UPLOAD TICKET PHOTO</span>
            </div>

            <div className="scanner-file-upload-row">
              <input
                type="file"
                ref={fileInputRef}
                accept="image/*"
                onChange={handleFileUpload}
                style={{ display: "none" }}
              />
              <button
                type="button"
                className="scanner-file-upload-btn"
                onClick={() => fileInputRef.current?.click()}
                disabled={isProcessing}
              >
                📁 Scan QR from Image / Screenshot
              </button>
            </div>
          </div>
        </div>

        {/* Right Column: Live Verification Card & Recent Check-ins */}
        <div className="scanner-sidebar-column">
          {/* Dedicated Real-Time Verification Monitor */}
          <div className="live-scan-card">
            <div className="live-scan-card-header">
              <h3 className="live-scan-card-title">Live Verification Monitor</h3>
              {lastScanResult ? (
                <span className={`live-scan-pill pill-${lastScanResult.status}`}>
                  {lastScanResult.status === "success" ? "● Approved" : lastScanResult.status === "wrong_workshop" ? "● Wrong Workshop" : "● Denied"}
                </span>
              ) : (
                <span className="live-scan-pill pill-idle">● Ready</span>
              )}
            </div>

            {lastScanResult ? (
              <div className="live-scan-body">
                <div className={`scan-result-banner banner-${lastScanResult.status}`}>
                  <div className="scan-result-icon">
                    {lastScanResult.status === "success" ? "✓" : lastScanResult.status === "wrong_workshop" ? "⚠️" : "✕"}
                  </div>
                  <div className="scan-result-text">
                    <strong>{lastScanResult.title}</strong>
                    <p>{lastScanResult.message}</p>
                  </div>
                </div>

                <div className="scan-meta-table">
                  <div className="scan-meta-row">
                    <span className="scan-meta-key">Scanned Ticket #</span>
                    <span className="scan-meta-val mono">{lastScanResult.ticketNumber}</span>
                  </div>
                  {lastScanResult.attendeeName && (
                    <div className="scan-meta-row">
                      <span className="scan-meta-key">Attendee Name</span>
                      <span className="scan-meta-val font-semibold">{lastScanResult.attendeeName}</span>
                    </div>
                  )}
                  {lastScanResult.workshopTitle && (
                    <div className="scan-meta-row">
                      <span className="scan-meta-key">Ticket Belongs To</span>
                      <span className="scan-meta-val text-accent">{lastScanResult.workshopTitle}</span>
                    </div>
                  )}
                  <div className="scan-meta-row">
                    <span className="scan-meta-key">Current Scanner</span>
                    <span className="scan-meta-val">{workshop.Title || workshop.title}</span>
                  </div>
                  <div className="scan-meta-row">
                    <span className="scan-meta-key">Method</span>
                    <span className="scan-meta-val">{lastScanResult.checkInMethod || "QR Scan"}</span>
                  </div>
                  <div className="scan-meta-row">
                    <span className="scan-meta-key">Verified At</span>
                    <span className="scan-meta-val">{lastScanResult.time}</span>
                  </div>
                </div>

                {lastScanResult.status === "wrong_workshop" && lastScanResult.targetWorkshopId && (
                  <button
                    type="button"
                    className="scan-switch-workshop-btn"
                    onClick={() => navigate(`/admin_portal/workshops/${lastScanResult.targetWorkshopId}/scanner`)}
                  >
                    Switch to {lastScanResult.workshopTitle} Scanner ➔
                  </button>
                )}
              </div>
            ) : (
              <div className="live-scan-idle-state">
                <span className="idle-scan-icon">🎫</span>
                <p>Point camera at a ticket QR code</p>
                <small>Ticket validity, workshop matching, and attendee information appear here automatically.</small>
              </div>
            )}
          </div>
          <div className="recent-checkins-card">
            <div className="checkins-card-header">
              <h3 className="checkins-card-title">
                Recent Check-ins ({recentCheckIns.length})
              </h3>
              <Link
                to={`/admin_portal/workshops/${workshopId}/bookings-attendees?view=attendees`}
                className="checkins-view-all-link"
              >
                View All
              </Link>
            </div>

            <div className="recent-checkins-list">
              {recentCheckIns.length === 0 ? (
                <div className="empty-checkins-box">
                  <span className="empty-checkins-icon">🎫</span>
                  <p>No check-ins recorded yet for this session.</p>
                  <small>Scanned tickets will appear here in real-time.</small>
                </div>
              ) : (
                recentCheckIns.map((item, idx) => (
                  <div key={item.ticketId || idx} className="checkin-row-item">
                    <div className="checkin-avatar">
                      {item.attendeeName ? item.attendeeName.charAt(0).toUpperCase() : "A"}
                    </div>
                    <div className="checkin-info">
                      <div className="checkin-name">{item.attendeeName}</div>
                      <div className="checkin-ticket">{item.ticketNumber}</div>
                    </div>
                    <div className="checkin-status-col">
                      <span className="checkin-time">{item.formattedTime}</span>
                      <span className="checkin-badge-green">Checked In</span>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Bottom Dismissible Real-time Feedback Banner */}
      {feedback && (
        <div className={`scanner-feedback-banner feedback-${feedback.type}`}>
          <div className="feedback-content">
            <div className="feedback-icon-wrap">
              {feedback.type === "success" ? "✓" : "✕"}
            </div>
            <div className="feedback-text-wrap">
              <h4 className="feedback-title">{feedback.title}</h4>
              <p className="feedback-desc">
                {feedback.message}
                {feedback.ticketNumber && (
                  <span className="feedback-meta"> · Ticket: {feedback.ticketNumber} · Time: {feedback.time}</span>
                )}
              </p>
            </div>
          </div>
          <button
            type="button"
            className="feedback-dismiss-btn"
            onClick={() => setFeedback(null)}
            aria-label="Dismiss message"
          >
            ✕
          </button>
        </div>
      )}
    </div>
  );
}
