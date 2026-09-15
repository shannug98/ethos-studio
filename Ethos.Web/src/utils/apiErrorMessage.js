export function getApiErrorMessage(
  error,
  fallback = "Something went wrong. Please try again."
) {
  if (!error) {
    return fallback;
  }

  if (error.isNetworkError) {
    return "Unable to connect to Ethos. Please check your internet connection and try again.";
  }

  if (error.status === 401) {
    return "Your session has expired. Please sign in again.";
  }

  if (error.status === 403) {
    return "You don't have permission to perform this action.";
  }

  if (error.status === 404) {
    return "The requested resource could not be found.";
  }

  if (error.status === 409) {
    return (
      error.message ||
      "This request conflicts with the current state."
    );
  }

  if (error.status >= 500) {
    return "Ethos is temporarily unavailable. Please try again shortly.";
  }

  return (
    error.message ||
    fallback
  );
}
