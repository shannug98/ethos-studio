import React, { useState, useEffect, useRef, useCallback } from "react";
import { useOutletContext, Link, useNavigate } from "react-router-dom";
import { Html5Qrcode } from "html5-qrcode";
import { adminApi } from "../../../services/adminApi";
import "./AdminWorkshopScanner.css";

export default function AdminWorkshopScanner() {
  const { workshop, reloadWorkshop } = useOutletContext();
  const navigate = useNavigate();
  const workshopId = workshop.Id || workshop.id;

  // Scanner states
  const [isScanning, setIsScanning] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [cameraError, setCameraError] = useState(null);
  const [availableCameras, setAvailableCameras] = useState([]);
  const [selectedCameraId, setSelectedCameraId] = useState(null);
  const [torchOn, setTorchOn] = useState(false);

  // Manual input state
  const [manualTicketInput, setManualTicketInput] = useState("");
  const [manualSubmitting, setManualSubmitting] = useState(false);

  // Feedback notifications
  const [feedback, setFeedback] = useState(null); // { type: 'success' | 'error', title: '', message: '', code: '' }
  const [recentCheckIns, setRecentCheckIns] = useState(
    workshop.RecentCheckIns || workshop.recentCheckIns || []
  );

  const html5QrCodeRef = useRef(null);

  // Load available cameras
  useEffect(() => {
    Html5Qrcode.getCameras()
      .then((cameras) => {
        if (cameras && cameras.length > 0) {
          setAvailableCameras(cameras);
          // Prefer back/environment camera if available
          const backCam = cameras.find(
            (c) => c.label.toLowerCase().includes("back") || c.label.toLowerCase().includes("environment")
          );
          setSelectedCameraId(backCam ? backCam.id : cameras[0].id);
        }
      })
      .catch(() => {
        // Camera permissions or no cameras found
      });

    return () => {
      stopCameraScanner();
    };
  }, []);

  const stopCameraScanner = useCallback(async () => {
    if (html5QrCodeRef.current && html5QrCodeRef.current.isScanning) {
      try {
        await html5QrCodeRef.current.stop();
      } catch {
        // Ignore stop error
      }
    }
    setIsScanning(false);
    setTorchOn(false);
  }, []);

  const handleCheckInResult = useCallback(
    async (payload) => {
      if (isProcessing) return;
      setIsProcessing(true);

      try {
        const res = await adminApi.checkInWorkshopTicket(workshopId, payload);

        if (res && res.success) {
          setFeedback({
            type: "success",
            title: "Check-in Successful!",
            message: `${res.attendeeName} has been checked in to ${workshop.Title || workshop.title}.`,
            ticketNumber: res.ticketNumber,
            time: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
          });

          // Add to top of recent check-ins
          setRecentCheckIns((prev) => [
            {
              ticketId: res.ticketId,
              attendeeName: res.attendeeName,
              ticketNumber: res.ticketNumber,
              formattedTime: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
              checkInMethod: res.checkInMethod || "QR",
            },
            ...prev.slice(0, 14),
          ]);

          // Refresh workshop overview stats in background
          if (reloadWorkshop) reloadWorkshop();
        } else {
          setFeedback({
            type: "error",
            code: res?.code || "CHECKIN_FAILED",
            title: res?.code === "WRONG_WORKSHOP" ? "Wrong Workshop" : "Check-in Denied",
            message: res?.message || "Check-in validation failed.",
          });
        }
      } catch (err) {
        setFeedback({
          type: "error",
          code: "NETWORK_ERROR",
          title: "Check-in Error",
          message: err?.message || "Failed to contact verification server.",
        });
      } finally {
        // Debounce lock so scanner is immediately ready for next scan without duplicates
        setTimeout(() => {
          setIsProcessing(false);
        }, 1600);
      }
    },
    [isProcessing, workshopId, workshop, reloadWorkshop]
  );

  const startCameraScanner = async () => {
    setCameraError(null);
    setFeedback(null);

    const cameraId = selectedCameraId || (availableCameras[0] && availableCameras[0].id);
    if (!cameraId) {
      setCameraError("No camera device found or permission not granted. Please use manual entry.");
      return;
    }

    try {
      if (!html5QrCodeRef.current) {
        html5QrCodeRef.current = new Html5Qrcode("qr-camera-viewport");
      }

      await html5QrCodeRef.current.start(
        cameraId,
        {
          fps: 15,
          qrbox: { width: 250, height: 250 },
          aspectRatio: 1.0,
        },
        (decodedText) => {
          handleCheckInResult({ qrToken: decodedText });
        },
        () => {
          // Ignore parse errors while looking for QR
        }
      );

      setIsScanning(true);
    } catch (err) {
      setCameraError(
        err?.message || "Failed to access camera. Please allow camera permissions or enter ticket manually."
      );
      setIsScanning(false);
    }
  };

  const handleSwitchCamera = async () => {
    if (availableCameras.length <= 1) return;
    const currentIndex = availableCameras.findIndex((c) => c.id === selectedCameraId);
    const nextIndex = (currentIndex + 1) % availableCameras.length;
    const nextCamera = availableCameras[nextIndex];
    setSelectedCameraId(nextCamera.id);

    if (isScanning) {
      await stopCameraScanner();
      setTimeout(() => {
        startCameraScanner();
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
            onClick={() => navigate(`/admin_portal/workshops/${workshopId}/attendees`)}
          >
            📋 View Attendees
          </button>
        </div>
      </div>

      {/* Main 2-Column Grid */}
      <div className="scanner-main-layout">
        {/* Left Column: Viewfinder & Camera Controls */}
        <div className="scanner-camera-column">
          <div className="camera-card">
            {/* Viewfinder Canvas Wrap */}
            <div className="viewfinder-container">
              <div id="qr-camera-viewport" className="qr-viewport-canvas" />

              {/* Viewfinder Visual Frame (Reference Styling) */}
              <div className="viewfinder-overlay">
                <div className="viewfinder-box">
                  <div className="corner-bracket top-left" />
                  <div className="corner-bracket top-right" />
                  <div className="corner-bracket bottom-left" />
                  <div className="corner-bracket bottom-right" />
                  {isScanning && <div className="scanning-laser-line" />}
                </div>

                {isScanning && (
                  <div className="viewport-status-badge">
                    <span className="scan-radar-dot" />
                    <span>Scanning for {workshop.Title || workshop.title}</span>
                  </div>
                )}

                <div className="viewport-instruction-badge">
                  Position QR code within the frame
                </div>
              </div>

              {/* Viewport Control Buttons */}
              <div className="viewport-controls-bar">
                <button
                  type="button"
                  className={`viewport-control-btn ${torchOn ? "active" : ""}`}
                  onClick={handleToggleTorch}
                  title="Toggle Flashlight / Torch"
                  disabled={!isScanning}
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

          {/* Security Banner */}
          <div className="scanner-security-banner">
            <span className="banner-info-icon">ℹ</span>
            <div className="banner-text">
              <strong>Only tickets for this workshop can be scanned.</strong>
              <p>Tickets from other workshops or invalid passes will be rejected automatically.</p>
            </div>
          </div>

          {/* Manual Entry Fallback Drawer */}
          <div className="manual-entry-card">
            <h4 className="manual-entry-title">Manual Ticket Check-In Fallback</h4>
            <p className="manual-entry-desc">
              If a student's screen is broken or the QR is unreadable, enter their ticket code below:
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
          </div>
        </div>

        {/* Right Column: Recent Check-ins */}
        <div className="scanner-sidebar-column">
          <div className="recent-checkins-card">
            <div className="checkins-card-header">
              <h3 className="checkins-card-title">
                Recent Check-ins ({recentCheckIns.length})
              </h3>
              <Link
                to={`/admin_portal/workshops/${workshopId}/attendees`}
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
