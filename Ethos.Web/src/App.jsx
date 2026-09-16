import { useState, lazy, Suspense } from "react";
import { BrowserRouter, Routes, Route, Navigate, Outlet, useParams } from "react-router-dom";

import Navbar from "./components/Navbar";
import Footer from "./components/Footer";
import FloatingSocials from "./components/FloatingSocials";
import EchoBot from "./components/EchoBot/EchoBot";
import PolicyModal from "./components/PolicyModal";
import RouteLoadingFallback from "./components/common/RouteLoadingFallback";

// Public Marketing & Auth Pages (Eagerly load light Home, lazy-load remaining)
import Home from "./pages/Home";
const Classes = lazy(() => import("./pages/Classes"));
const WorkshopsPage = lazy(() => import("./pages/WorkshopsPage"));
const WorkshopDetailsPage = lazy(() => import("./pages/WorkshopDetailsPage"));
const WorkshopCheckoutPage = lazy(() => import("./pages/WorkshopCheckoutPage"));
const Events = lazy(() => import("./pages/Events"));
const Gallery = lazy(() => import("./pages/Gallery"));
const Login = lazy(() => import("./pages/Login"));
const Register = lazy(() => import("./pages/Register"));
const GuestWorkshopFeedback = lazy(() => import("./pages/GuestWorkshopFeedback"));

// Member Services (Student & Trainer Portals) launch gating flag
const ENABLE_MEMBER_PORTALS = false;

// Student Portal Layout & Shell (Eager layout/guard, lazy pages)
import StudentLayout from "./components/student/StudentLayout";
import StudentProtectedRoute from "./components/auth/StudentProtectedRoute";
const StudentLogin = lazy(() => import("./pages/student/StudentLogin"));
const StudentDashboard = lazy(() => import("./pages/student/StudentDashboard"));
const StudentClasses = lazy(() => import("./pages/student/StudentClasses"));
const StudentWorkshops = lazy(() => import("./pages/student/StudentWorkshops"));
const StudentMyWorkshops = lazy(() => import("./pages/student/StudentMyWorkshops"));
const StudentPackages = lazy(() => import("./pages/student/StudentPackages"));
const StudentFeedback = lazy(() => import("./pages/student/StudentFeedback"));
const StudentProfile = lazy(() => import("./pages/student/StudentProfile"));
const StudentNotifications = lazy(() => import("./pages/student/StudentNotifications"));

// Trainer Portal Layout & Guards (Eager layout/guards, lazy pages)
import TrainerProtectedRoute from "./components/auth/TrainerProtectedRoute";
import TrainerPermissionRoute from "./components/auth/TrainerPermissionRoute";
import { TRAINER_PERMISSIONS } from "./constants/trainerPermissions";
import TrainerLayout from "./components/trainer/TrainerLayout";
const TrainerLogin = lazy(() => import("./pages/trainer/TrainerLogin"));
const TrainerDashboard = lazy(() => import("./pages/trainer/TrainerDashboard"));
const TrainerProfile = lazy(() => import("./pages/trainer/TrainerProfile"));
const TrainerApplication = lazy(() => import("./pages/trainer/TrainerApplication"));
const TrainerApplicationDetails = lazy(() => import("./pages/trainer/TrainerApplicationDetails"));
const TrainerApplicationIntroduction = lazy(() => import("./pages/trainer/TrainerApplicationIntroduction"));
const TrainerApplicationTier = lazy(() => import("./pages/trainer/TrainerApplicationTier"));
const TrainerApplicationPayment = lazy(() => import("./pages/trainer/TrainerApplicationPayment"));
const TrainerApplicationReview = lazy(() => import("./pages/trainer/TrainerApplicationReview"));
const TrainerApplicationStatus = lazy(() => import("./pages/trainer/TrainerApplicationStatus"));
const TrainerApplicationDossier = lazy(() => import("./pages/trainer/TrainerApplicationDossier"));
const TrainerWorkshops = lazy(() => import("./pages/trainer/TrainerWorkshops"));
const TrainerWorkshopCreate = lazy(() => import("./pages/trainer/TrainerWorkshopCreate"));
const TrainerWorkshopEdit = lazy(() => import("./pages/trainer/TrainerWorkshopEdit"));
const TrainerWorkshopDetails = lazy(() => import("./pages/trainer/TrainerWorkshopDetails"));
const TrainerSchedule = lazy(() => import("./pages/trainer/TrainerSchedule"));
const TrainerPerformance = lazy(() => import("./pages/trainer/TrainerPerformance"));
const TrainerTier = lazy(() => import("./pages/trainer/TrainerTier"));
const TrainerNotifications = lazy(() => import("./pages/trainer/TrainerNotifications"));

// Admin Portal Layout & Guards (Eager layout/guards, lazy pages)
import AdminProtectedRoute from "./components/auth/AdminProtectedRoute";
import AdminLayout from "./components/admin/AdminLayout";
const AdminLogin = lazy(() => import("./pages/admin/AdminLogin"));
const AdminDashboard = lazy(() => import("./pages/admin/AdminDashboard"));
const AdminAudit = lazy(() => import("./pages/admin/AdminAudit"));
const AdminSecurity = lazy(() => import("./pages/admin/AdminSecurity"));
const AdminObservability = lazy(() => import("./pages/admin/AdminObservability"));
const AdminIncidents = lazy(() => import("./pages/admin/AdminIncidents"));
const AdminCorrectiveActions = lazy(() => import("./pages/admin/AdminCorrectiveActions"));
const AdminCommunications = lazy(() => import("./pages/admin/AdminCommunications"));
const AdminUsers = lazy(() => import("./pages/admin/AdminUsers"));
const AdminStudents = lazy(() => import("./pages/admin/AdminStudents"));
const AdminStudentDossier = lazy(() => import("./pages/admin/AdminStudentDossier"));
const AdminTrainers = lazy(() => import("./pages/admin/AdminTrainers"));
const AdminTrainerDossier = lazy(() => import("./pages/admin/AdminTrainerDossier"));
const AdminClasses = lazy(() => import("./pages/admin/AdminClasses"));
const AdminWorkshops = lazy(() => import("./pages/admin/AdminWorkshops"));
const AdminBookings = lazy(() => import("./pages/admin/AdminBookings"));
const AdminAttendance = lazy(() => import("./pages/admin/AdminAttendance"));
const AdminPayments = lazy(() => import("./pages/admin/AdminPayments"));
const AdminPackages = lazy(() => import("./pages/admin/AdminPackages"));
const AdminDevices = lazy(() => import("./pages/admin/AdminDevices"));
const AdminDailyActivity = lazy(() => import("./pages/admin/AdminDailyActivity"));
const AdminFeedback = lazy(() => import("./pages/admin/AdminFeedback"));
const AdminPlatforms = lazy(() => import("./pages/admin/AdminPlatforms"));
const AdminVideos = lazy(() => import("./pages/admin/AdminVideos"));
import { isAdminAuthenticated } from "./services/adminApi";

function AdminEntryRedirect() {
  return isAdminAuthenticated() ? (
    <Navigate to="/admin_portal/dashboard" replace />
  ) : (
    <Navigate to="/admin_portal/login" replace />
  );
}

function AdminDailyDateRedirect() {
  const { date } = useParams();
  return <Navigate to={`/admin_portal/dashboard/day/${date || ""}`} replace />;
}

function PublicLayout() {
  const [activePolicy, setActivePolicy] = useState(null);

  const handleOpenPolicy = (policyType) => {
    setActivePolicy(policyType);
  };

  const handleClosePolicy = () => {
    setActivePolicy(null);
  };

  return (
    <div className="ethos-app">
      <Navbar />
      <Outlet />
      <FloatingSocials />
      <EchoBot />
      <Footer onOpenPolicy={handleOpenPolicy} />
      <PolicyModal
        isOpen={!!activePolicy}
        policyType={activePolicy}
        onClose={handleClosePolicy}
        onSelectPolicy={handleOpenPolicy}
      />
    </div>
  );
}

function App() {
  return (
    <BrowserRouter>
      <Suspense fallback={<RouteLoadingFallback />}>
        <Routes>
          {/* PUBLIC WEBSITE ROUTES */}
          <Route element={<PublicLayout />}>
            <Route path="/" element={<Home />} />
          <Route path="/classes" element={<Classes />} />
          <Route path="/workshops" element={<WorkshopsPage />} />
          <Route path="/workshops/:slug" element={<WorkshopDetailsPage />} />
          <Route path="/workshops/:slug/checkout" element={<WorkshopCheckoutPage />} />
          <Route path="/events" element={<Events />} />
          <Route path="/gallery" element={<Gallery />} />

          <Route path="/login" element={<Login />} />
          <Route path="/student/login" element={<Navigate to="/login" replace />} />
          <Route path="/trainer/login" element={<Navigate to="/login" replace />} />
          <Route path="/register" element={<Navigate to="/login" replace />} />
          <Route path="/feedback/workshop/:token" element={<GuestWorkshopFeedback />} />
        </Route>

        {/* MEMBER SERVICES (STUDENT & TRAINER PORTALS) - CONTROLLED VIA GATING FLAG */}
        {ENABLE_MEMBER_PORTALS ? (
          <>
            {/* TRAINER AUTH / PRE-LOGIN ROUTES */}
            <Route path="/trainer/login" element={<TrainerLogin />} />
            <Route
              path="/trainer/application"
              element={<TrainerApplication />}
            />
            <Route
              path="/trainer/application/details"
              element={<TrainerApplicationDetails />}
            />
            <Route
              path="/trainer/application/documents"
              element={<Navigate to="/trainer/application/tier" replace />}
            />
            <Route
              path="/trainer/application/introduction"
              element={<TrainerApplicationIntroduction />}
            />
            <Route
              path="/trainer/application/tier"
              element={<TrainerApplicationTier />}
            />
            <Route
              path="/trainer/application/payment"
              element={<TrainerApplicationPayment />}
            />
            <Route
              path="/trainer/application/review"
              element={<TrainerApplicationReview />}
            />
            <Route
              path="/trainer/application/status"
              element={<TrainerApplicationStatus />}
            />

            {/* AUTHENTICATED TRAINER PORTAL */}
            <Route element={<TrainerProtectedRoute />}>
              <Route path="/trainer" element={<TrainerLayout />}>
                <Route index element={<Navigate to="/trainer/dashboard" replace />} />
                <Route
                  path="dashboard"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_DASHBOARD}>
                      <TrainerDashboard />
                    </TrainerPermissionRoute>
                  }
                />
                <Route
                  path="profile"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_PROFILE}>
                      <TrainerProfile />
                    </TrainerPermissionRoute>
                  }
                />
                <Route path="application" element={<TrainerApplicationDossier />} />
                <Route
                  path="workshops"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_WORKSHOPS}>
                      <TrainerWorkshops />
                    </TrainerPermissionRoute>
                  }
                />
                <Route
                  path="students"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_WORKSHOPS}>
                      <TrainerWorkshops />
                    </TrainerPermissionRoute>
                  }
                />
                <Route
                  path="attendance"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_WORKSHOPS}>
                      <TrainerWorkshops />
                    </TrainerPermissionRoute>
                  }
                />
                <Route
                  path="workshops/create"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.CREATE_WORKSHOP}>
                      <TrainerWorkshopCreate />
                    </TrainerPermissionRoute>
                  }
                />
                <Route
                  path="workshops/:id/edit"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.UPDATE_WORKSHOP}>
                      <TrainerWorkshopEdit />
                    </TrainerPermissionRoute>
                  }
                />
                <Route
                  path="workshops/:id"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_WORKSHOPS}>
                      <TrainerWorkshopDetails />
                    </TrainerPermissionRoute>
                  }
                />
                <Route
                  path="workshops/:id/students"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_WORKSHOP_STUDENTS}>
                      <TrainerWorkshopDetails />
                    </TrainerPermissionRoute>
                  }
                />
                <Route
                  path="workshops/:id/feedback"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_WORKSHOP_FEEDBACK}>
                      <TrainerWorkshopDetails />
                    </TrainerPermissionRoute>
                  }
                />
                <Route
                  path="workshops/:id/attendance"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_WORKSHOPS}>
                      <TrainerWorkshopDetails />
                    </TrainerPermissionRoute>
                  }
                />
                <Route path="schedule" element={<TrainerSchedule />} />
                <Route
                  path="performance"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_PERFORMANCE}>
                      <TrainerPerformance />
                    </TrainerPermissionRoute>
                  }
                />
                <Route
                  path="tier"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_TIER}>
                      <TrainerTier />
                    </TrainerPermissionRoute>
                  }
                />
                <Route
                  path="tier/history"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_TIER}>
                      <TrainerTier />
                    </TrainerPermissionRoute>
                  }
                />
                <Route
                  path="tier/upgrade"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_TIER}>
                      <TrainerTier />
                    </TrainerPermissionRoute>
                  }
                />
                <Route
                  path="notifications"
                  element={
                    <TrainerPermissionRoute permission={TRAINER_PERMISSIONS.VIEW_NOTIFICATIONS}>
                      <TrainerNotifications />
                    </TrainerPermissionRoute>
                  }
                />
              </Route>
            </Route>

            {/* AUTHENTICATED STUDENT PORTAL */}
            <Route element={<StudentProtectedRoute />}>
              <Route path="/student" element={<StudentLayout />}>
                <Route index element={<Navigate to="/student/dashboard" replace />} />
                <Route path="dashboard" element={<StudentDashboard />} />
                <Route path="classes" element={<StudentClasses />} />
                <Route path="workshops" element={<StudentWorkshops />} />
                <Route path="my-workshops" element={<StudentMyWorkshops />} />
                <Route path="packages" element={<StudentPackages />} />
                <Route path="feedback" element={<StudentFeedback />} />
                <Route path="profile" element={<StudentProfile />} />
                <Route path="notifications" element={<StudentNotifications />} />
                {/* Absolute route definitions for full compatibility and verification */}
                <Route path="/student/dashboard" element={<StudentDashboard />} />
                <Route path="/student/classes" element={<StudentClasses />} />
                <Route path="/student/workshops" element={<StudentWorkshops />} />
                <Route path="/student/my-workshops" element={<StudentMyWorkshops />} />
                <Route path="/student/packages" element={<StudentPackages />} />
                <Route path="/student/feedback" element={<StudentFeedback />} />
                <Route path="/student/profile" element={<StudentProfile />} />
                <Route path="/student/notifications" element={<StudentNotifications />} />
              </Route>
              <Route path="/student" element={<Navigate to="/student/dashboard" replace />} />
            </Route>
          </>
        ) : (
          <>
            {/* LOCKED MEMBER PORTALS - ALL DIRECT TRAINER & STUDENT URLS REDIRECT TO COMING SOON */}
            <Route path="/trainer" element={<Navigate to="/login" replace />} />
            <Route path="/trainer/*" element={<Navigate to="/login" replace />} />
            <Route path="/student" element={<Navigate to="/login" replace />} />
            <Route path="/student/*" element={<Navigate to="/login" replace />} />
          </>
        )}

        {/* ADMIN PORTAL AUTH & SHELL ROUTES */}
        <Route path="/admin_portal" element={<AdminEntryRedirect />} />
        <Route path="/admin_portal/login" element={<AdminLogin />} />

        {/* AUTHENTICATED ADMIN PORTAL */}
        <Route element={<AdminProtectedRoute />}>
          <Route path="/admin_portal" element={<AdminLayout />}>
            <Route index element={<Navigate to="/admin_portal/dashboard" replace />} />
            <Route path="dashboard" element={<AdminDashboard />} />
            <Route path="dashboard/day/:date" element={<AdminDailyActivity />} />
            <Route path="dashboard/daily/:date" element={<AdminDailyDateRedirect />} />
            <Route path="telemetry/:date" element={<AdminDailyDateRedirect />} />
            <Route path="users" element={<AdminUsers />} />
            <Route path="students" element={<AdminStudents />} />
            <Route path="students/:studentId" element={<AdminStudentDossier />} />
            <Route path="trainers" element={<AdminTrainers />} />
            <Route path="trainers/:trainerId" element={<AdminTrainerDossier />} />
            <Route path="classes" element={<AdminClasses />} />
            <Route path="workshops" element={<AdminWorkshops />} />
            <Route path="bookings" element={<AdminBookings />} />
            <Route path="attendance" element={<AdminAttendance />} />
            <Route path="packages" element={<AdminPackages />} />
            <Route path="payments" element={<AdminPayments />} />
            <Route path="feedback" element={<AdminFeedback />} />
            <Route path="videos" element={<AdminVideos />} />
            <Route path="audit-logs" element={<AdminAudit />} />
            <Route path="audit" element={<Navigate to="/admin_portal/audit-logs" replace />} />
            <Route path="security" element={<AdminSecurity />} />
            <Route path="security-events" element={<Navigate to="/admin_portal/security" replace />} />
            <Route path="observability" element={<AdminObservability />} />
            <Route path="observability/trace/:traceId" element={<AdminObservability />} />
            <Route path="platforms" element={<AdminPlatforms />} />
            <Route path="incidents" element={<AdminIncidents />} />
            <Route path="corrective-actions" element={<AdminCorrectiveActions />} />
            <Route path="communications" element={<AdminCommunications />} />
            <Route path="devices" element={<AdminDevices />} />
          </Route>
        </Route>
      </Routes>
      </Suspense>
    </BrowserRouter>
  );
}

export default App;
