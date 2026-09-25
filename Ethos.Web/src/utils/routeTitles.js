/**
 * Authoritative Route Title Resolver for Ethos Dance Studio
 * Maps routes and URL fragments to descriptive browser tab titles.
 */
export function getRouteTitle(pathname = "/", hash = "") {
  const path = pathname || "/";

  // Homepage and section anchors
  if (path === "/" || path === "") {
    if (hash === "#contact") return "Contact | Ethos Dance Studio";
    if (hash === "#about") return "About | Ethos Dance Studio";
    if (hash === "#trainers") return "Trainers | Ethos Dance Studio";
    return "Ethos Dance Studio";
  }

  // Public Marketing Routes
  if (path === "/workshops") return "Workshops | Ethos Dance Studio";
  if (path.startsWith("/workshops/") && path.includes("/checkout")) return "Checkout | Ethos Dance Studio";
  if (path.startsWith("/workshops/")) return "Workshop Details | Ethos Dance Studio";
  if (path === "/gallery") return "Gallery | Ethos Dance Studio";
  if (path === "/classes") return "Classes | Ethos Dance Studio";
  if (path === "/events") return "Events | Ethos Dance Studio";
  if (path === "/contact") return "Contact | Ethos Dance Studio";
  if (path === "/login") return "Login | Ethos Dance Studio";
  if (path.startsWith("/feedback/workshop/")) return "Feedback | Ethos Dance Studio";

  // Admin Portal Routes
  if (path === "/admin_portal/login" || path === "/admin/login") return "Admin Login | Ethos Dance Studio";
  if (path.startsWith("/admin_portal/workshops/wizard")) return "Workshop Wizard | Ethos Dance Studio";
  if (path.startsWith("/admin_portal/workshops")) return "Admin Workshops | Ethos Dance Studio";
  if (path.startsWith("/admin_portal/videos")) return "Media Library | Ethos Dance Studio";
  if (path.startsWith("/admin_portal/dashboard")) return "Admin Dashboard | Ethos Dance Studio";
  if (path.startsWith("/admin_portal/students")) return "Admin Students | Ethos Dance Studio";
  if (path.startsWith("/admin_portal/trainers")) return "Admin Trainers | Ethos Dance Studio";
  if (path.startsWith("/admin_portal/payments")) return "Admin Payments | Ethos Dance Studio";
  if (path.startsWith("/admin_portal")) return "Admin Portal | Ethos Dance Studio";

  // Trainer Portal Routes
  if (path === "/trainer/login") return "Trainer Login | Ethos Dance Studio";
  if (path.startsWith("/trainer/application")) return "Trainer Application | Ethos Dance Studio";
  if (path.startsWith("/trainer/dashboard")) return "Trainer Dashboard | Ethos Dance Studio";
  if (path.startsWith("/trainer")) return "Trainer Portal | Ethos Dance Studio";

  // Student Portal Routes
  if (path.startsWith("/student/dashboard")) return "Student Dashboard | Ethos Dance Studio";
  if (path.startsWith("/student/workshops")) return "Student Workshops | Ethos Dance Studio";
  if (path.startsWith("/student")) return "Student Portal | Ethos Dance Studio";

  return "Ethos Dance Studio";
}
