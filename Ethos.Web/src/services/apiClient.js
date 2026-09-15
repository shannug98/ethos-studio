import { API_BASE_URL } from "../config/api";

const TOKEN_KEY = "ethos_access_token";

/**
 * AuthContext registers the session invalidation callback here.
 *
 * apiClient deliberately does not import React Router or AuthContext.
 * This keeps the API layer independent from application state/navigation.
 */
let unauthorizedHandler = null;

/**
 * Prevents multiple simultaneous 401 responses from repeatedly
 * invalidating the same session.
 */
let unauthorizedHandled = false;

export function registerUnauthorizedHandler(handler) {
  unauthorizedHandler =
    typeof handler === "function" ? handler : null;

  /**
   * A new handler represents a fresh authentication lifecycle.
   * Reset the one-time 401 guard when a handler is registered.
   */
  if (unauthorizedHandler) {
    unauthorizedHandled = false;
  }
}

export function resetUnauthorizedGuard() {
  unauthorizedHandled = false;
}

const DEFAULT_TIMEOUT_MS = 15000;

function createTimeoutSignal(timeoutMs, externalSignal) {
  const controller = new AbortController();
  const timer = setTimeout(() => {
    controller.abort(new Error("Request timed out"));
  }, timeoutMs);

  if (externalSignal) {
    if (externalSignal.aborted) {
      clearTimeout(timer);
      controller.abort(externalSignal.reason);
    } else {
      externalSignal.addEventListener("abort", () => {
        clearTimeout(timer);
        controller.abort(externalSignal.reason);
      }, { once: true });
    }
  }

  return {
    signal: controller.signal,
    cleanup: () => clearTimeout(timer),
  };
}

function createApiError(response, data) {
  const message =
    data?.message ||
    data?.title ||
    data?.error ||
    (typeof data === "string" ? data : null) ||
    `Request failed with status ${response.status}`;

  const error = new Error(message);

  error.status = response.status;
  error.data = data;
  error.code = data?.code || null;
  error.isApiError = true;

  return error;
}

async function parseResponse(response) {
  const contentType =
    response.headers.get("content-type") || "";

  if (contentType.includes("application/json")) {
    try {
      return await response.json();
    } catch {
      return null;
    }
  }

  try {
    const text = await response.text();
    return text || null;
  } catch {
    return null;
  }
}

async function request(endpoint, options = {}) {
  const token = localStorage.getItem(TOKEN_KEY);

  const headers = {
    Accept: "application/json",
    ...(options.headers || {}),
  };

  if (!(options.body instanceof FormData)) {
    headers["Content-Type"] = "application/json";
  }

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const timeoutMs = options.timeout ?? DEFAULT_TIMEOUT_MS;
  const { signal, cleanup } = createTimeoutSignal(timeoutMs, options.signal);

  let response;

  try {
    response = await fetch(`${API_BASE_URL}${endpoint}`, {
      ...options,
      headers,
      signal,
    });
  } catch (error) {
    cleanup();

    if (error?.name === "AbortError" || signal.aborted) {
      const timeoutError = new Error(
        "Request timed out. Please check your network connection and try again."
      );
      timeoutError.isTimeout = true;
      throw timeoutError;
    }

    const networkError = new Error(
      "Unable to connect to Ethos. Please check your internet connection and try again."
    );

    networkError.isNetworkError = true;
    networkError.cause = error;

    throw networkError;
  } finally {
    cleanup();
  }

  const data = await parseResponse(response);

  if (!response.ok) {
    const error = createApiError(response, data);

    /**
     * 401 = authentication/session is no longer valid.
     *
     * Handle session invalidation only once for the current
     * authentication lifecycle.
     */
    if (
      response.status === 401 &&
      unauthorizedHandler &&
      !unauthorizedHandled
    ) {
      unauthorizedHandled = true;

      try {
        unauthorizedHandler(error);
      } catch {
        /**
         * Session handling must never replace the original
         * API error that the calling component expects.
         */
      }
    }

    /**
     * IMPORTANT:
     *
     * 403 is deliberately NOT handled here.
     * A 403 means the user is authenticated but does not have
     * permission to perform/access the requested operation.
     *
     * The TrainerPermissionRoute and feature-level permission
     * checks handle this distinction.
     */

    throw error;
  }

  return data;
}

export const apiClient = {
  get(endpoint) {
    return request(endpoint, {
      method: "GET",
    });
  },

  post(endpoint, body) {
    return request(endpoint, {
      method: "POST",
      body:
        body instanceof FormData
          ? body
          : JSON.stringify(body ?? {}),
    });
  },

  put(endpoint, body) {
    return request(endpoint, {
      method: "PUT",
      body:
        body instanceof FormData
          ? body
          : JSON.stringify(body ?? {}),
    });
  },

  patch(endpoint, body) {
    return request(endpoint, {
      method: "PATCH",
      body:
        body instanceof FormData
          ? body
          : JSON.stringify(body ?? {}),
    });
  },

  delete(endpoint) {
    return request(endpoint, {
      method: "DELETE",
    });
  },

  async getBlob(endpoint) {
    const token = localStorage.getItem(TOKEN_KEY);
    const headers = {};
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
      headers,
    });
    if (!response.ok) {
      return null;
    }
    return await response.blob();
  },
};

export { API_BASE_URL };
