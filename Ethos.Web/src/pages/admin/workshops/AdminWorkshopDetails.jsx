import React from "react";
import { useOutletContext, useNavigate } from "react-router-dom";
import "./AdminWorkshopSubPages.css";

export default function AdminWorkshopDetails() {
  const { workshop } = useOutletContext();
  const navigate = useNavigate();
  const workshopId = workshop.Id || workshop.id;

  return (
    <div className="workshop-subpage-container">
      <div className="subpage-header">
        <div>
          <h1 className="subpage-title">Workshop Specifications & Details</h1>
          <p className="subpage-subtitle">Full master configuration, schedule timings, and syllabus.</p>
        </div>
        <button
          type="button"
          className="admin-btn-primary"
          onClick={() => navigate(`/admin_portal/workshops/${workshopId}/edit`)}
        >
          ✏️ Edit Workshop
        </button>
      </div>

      <div className="details-card">
        <div className="details-grid-2col">
          <div className="spec-group">
            <label>Workshop Title</label>
            <div className="spec-val font-bold">{workshop.Title || workshop.title}</div>
          </div>

          <div className="spec-group">
            <label>System Reference</label>
            <div className="spec-val font-mono">{workshop.WorkshopReference || workshop.workshopReference || "—"}</div>
          </div>

          <div className="spec-group">
            <label>Lead Instructor / Trainer</label>
            <div className="spec-val">{workshop.TrainerName || workshop.trainerName || "Ethos Master"}</div>
          </div>

          <div className="spec-group">
            <label>Dance Style & Level</label>
            <div className="spec-val">
              {workshop.DanceStyle || workshop.danceStyle} · {workshop.Level || workshop.level}
            </div>
          </div>

          <div className="spec-group">
            <label>Date & Times</label>
            <div className="spec-val">{workshop.FormattedSchedule || workshop.formattedSchedule}</div>
          </div>

          <div className="spec-group">
            <label>Venue / Location</label>
            <div className="spec-val">📍 {workshop.Venue || workshop.venue || "Ethos Main Studio"}</div>
          </div>

          <div className="spec-group">
            <label>Standard Admission Price</label>
            <div className="spec-val font-bold text-indigo-600">
              ₹ {Number(workshop.Price || workshop.price || 0).toLocaleString("en-IN")}
            </div>
          </div>

          <div className="spec-group">
            <label>Capacity Allocation</label>
            <div className="spec-val">
              <strong>{workshop.BookedCount || workshop.bookedCount || 0}</strong> booked of{" "}
              {workshop.Capacity || workshop.capacity} maximum capacity
            </div>
          </div>
        </div>

        {workshop.Description || workshop.description ? (
          <div className="spec-full-desc">
            <label>Syllabus & Workshop Description</label>
            <div className="desc-content">{workshop.Description || workshop.description}</div>
          </div>
        ) : null}
      </div>
    </div>
  );
}
