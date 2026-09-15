import { Outlet } from "react-router-dom";

import { TrainerPermissionsProvider } from "../../hooks/useTrainerPermissions";

import TrainerSidebar from "./TrainerSidebar";
import TrainerTopbar from "./TrainerTopbar";

export default function TrainerLayout() {
  return (
    <TrainerPermissionsProvider>
      <div className="trainer-shell">
        <TrainerSidebar />

        <div className="trainer-main">
          <TrainerTopbar />

          <main
            className="trainer-content"
            id="trainer-main-content"
          >
            <Outlet />
          </main>
        </div>
      </div>
    </TrainerPermissionsProvider>
  );
}
