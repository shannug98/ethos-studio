import React, { useState } from "react";
import { useOutletContext, useNavigate } from "react-router-dom";
import { adminApi } from "../../../services/adminApi";
import "./AdminWorkshopSubPages.css";

export default function AdminWorkshopEdit() {
  const { workshop, reloadWorkshop } = useOutletContext();
  const navigate = useNavigate();
  const workshopId = workshop.Id || workshop.id;

  const [title, setTitle] = useState(workshop.Title || workshop.title || "");
  const [description, setDescription] = useState(workshop.Description || workshop.description || "");
  const [danceStyle, setDanceStyle] = useState(workshop.DanceStyle || workshop.danceStyle || "");
  const [level, setLevel] = useState(workshop.Level || workshop.level || "Open Level");
  const [venue, setVenue] = useState(workshop.Venue || workshop.venue || "Ethos Main Studio");
  const [price, setPrice] = useState(workshop.Price ?? workshop.price ?? 0);
  const [capacity, setCapacity] = useState(workshop.Capacity ?? workshop.capacity ?? 30);
  const [imageUrl, setImageUrl] = useState(workshop.ImageUrl || workshop.imageUrl || "");
  const [submitting, setSubmitting] = useState(false);
  const [statusMsg, setStatusMsg] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    setStatusMsg(null);

    try {
      await adminApi.updateWorkshop(workshopId, {
        title,
        description,
        danceStyle,
        level,
        venue,
        price: Number(price),
        capacity: Number(capacity),
        imageUrl: imageUrl.trim() || null,
      });

      setStatusMsg({ type: "success", text: "Workshop updated successfully." });
      if (reloadWorkshop) await reloadWorkshop();
      setTimeout(() => {
        navigate(`/admin_portal/workshops/${workshopId}/overview`);
      }, 1200);
    } catch (err) {
      setStatusMsg({ type: "error", text: err?.message || "Failed to update workshop." });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="workshop-subpage-container">
      <div className="subpage-header">
        <div>
          <h1 className="subpage-title">Edit Workshop Details</h1>
          <p className="subpage-subtitle">Update workshop title, capacity, price, venue, and banner image.</p>
        </div>
      </div>

      {statusMsg && (
        <div className={`subpage-${statusMsg.type}-banner`}>{statusMsg.text}</div>
      )}

      <div className="details-card">
        <form onSubmit={handleSubmit} className="workshop-edit-form">
          <div className="form-group">
            <label className="form-label">Workshop Title *</label>
            <input
              type="text"
              className="form-control"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              required
            />
          </div>

          <div className="form-row-2col">
            <div className="form-group">
              <label className="form-label">Dance Style *</label>
              <input
                type="text"
                className="form-control"
                value={danceStyle}
                onChange={(e) => setDanceStyle(e.target.value)}
                required
              />
            </div>

            <div className="form-group">
              <label className="form-label">Level *</label>
              <select
                className="form-control"
                value={level}
                onChange={(e) => setLevel(e.target.value)}
              >
                <option value="Beginner">Beginner</option>
                <option value="Intermediate">Intermediate</option>
                <option value="Advanced">Advanced</option>
                <option value="Open Level">Open Level</option>
              </select>
            </div>
          </div>

          <div className="form-row-2col">
            <div className="form-group">
              <label className="form-label">Price (₹) *</label>
              <input
                type="number"
                min="0"
                className="form-control"
                value={price}
                onChange={(e) => setPrice(e.target.value)}
                required
              />
            </div>

            <div className="form-group">
              <label className="form-label">Capacity (Max Seats) *</label>
              <input
                type="number"
                min="1"
                className="form-control"
                value={capacity}
                onChange={(e) => setCapacity(e.target.value)}
                required
              />
            </div>
          </div>

          <div className="form-group">
            <label className="form-label">Venue / Studio Location *</label>
            <input
              type="text"
              className="form-control"
              value={venue}
              onChange={(e) => setVenue(e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label className="form-label">Cover Image URL</label>
            <input
              type="url"
              className="form-control"
              placeholder="https://..."
              value={imageUrl}
              onChange={(e) => setImageUrl(e.target.value)}
            />
          </div>

          <div className="form-group">
            <label className="form-label">Syllabus & Description</label>
            <textarea
              rows="4"
              className="form-control"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>

          <div className="edit-actions-row">
            <button
              type="button"
              className="admin-btn-secondary"
              onClick={() => navigate(`/admin_portal/workshops/${workshopId}/overview`)}
              disabled={submitting}
            >
              Cancel
            </button>
            <button type="submit" className="admin-btn-primary" disabled={submitting}>
              {submitting ? "Saving Changes..." : "Save Workshop Changes"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
