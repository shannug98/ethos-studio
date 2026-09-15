import { useState, useEffect } from "react";
import { Outlet } from "react-router-dom";

import StudentSidebar from "./StudentSidebar";
import StudentTopbar from "./StudentTopbar";
import ProfilePhotoModal from "./ProfilePhotoModal";
import { studentStateSync } from "../../services/studentStateSync";
import "../../styles/student/student-layout.css";

export default function StudentLayout() {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [viewer, setViewer] = useState({ isOpen: false, photoUrl: null, name: "Student" });

  useEffect(() => {
    const unsub = studentStateSync.onPhotoViewerChange((state) => {
      setViewer(state);
    });
    return () => unsub();
  }, []);

  return (
    <div className="student-shell">
      <StudentSidebar
        isOpen={mobileOpen}
        onClose={() => setMobileOpen(false)}
      />

      <div className="student-main">
        <StudentTopbar
          onToggleMobile={() => setMobileOpen((prev) => !prev)}
        />

        <main className="student-content" id="student-main-content">
          <Outlet />
        </main>
      </div>

      <ProfilePhotoModal
        isOpen={viewer.isOpen}
        photoUrl={viewer.photoUrl}
        name={viewer.name}
        onClose={() => studentStateSync.closePhotoViewer()}
      />
    </div>
  );
}
