/**
 * Centralized presentation helpers for workshop passes, sessions, and timing displays.
 */

/**
 * FIX 01: Authoritative pass availability mapping with defensive fallbacks.
 * Uses backend WorkshopPassTypeDto properties (isAvailable, availableSeats)
 * with backwards-compatible fallbacks (availableQuantity, passesRemaining, remainingQuantity).
 */
export function getPassAvailability(pass) {
  if (!pass) {
    return {
      remainingSeats: 0,
      isSoldOut: true,
      isAvailable: false,
    };
  }

  const remainingSeats =
    pass.availableSeats ??
    pass.availableQuantity ??
    pass.passesRemaining ??
    pass.remainingQuantity ??
    0;

  const isSoldOut =
    pass.isAvailable !== undefined
      ? !pass.isAvailable
      : (Boolean(pass.isSalesClosed) || remainingSeats <= 0);

  const isAvailable = !isSoldOut;

  return {
    remainingSeats,
    isSoldOut,
    isAvailable,
  };
}

/**
 * FIX 03: Extract calendar date string (YYYY-MM-DD) avoiding timezone shifts.
 */
export function getCalendarDateKey(dateInput) {
  if (!dateInput) return "";
  if (typeof dateInput === "string") {
    return dateInput.split("T")[0];
  }
  if (dateInput instanceof Date) {
    const y = dateInput.getFullYear();
    const m = String(dateInput.getMonth() + 1).padStart(2, "0");
    const d = String(dateInput.getDate()).padStart(2, "0");
    return `${y}-${m}-${d}`;
  }
  return String(dateInput);
}

/**
 * FIX 03: Groups and chronologically sorts sessions by date (ascending)
 * and then by startTime (ascending) within each date.
 * Returns an array of [dateKey, sortedSessionsForDate] pairs.
 * Never mutates input session arrays.
 */
export function getChronologicalGroupedSessions(sessions, fallbackDate) {
  if (!Array.isArray(sessions) || sessions.length === 0) {
    if (fallbackDate) {
      const key = getCalendarDateKey(fallbackDate);
      return key ? [[key, []]] : [];
    }
    return [];
  }

  const grouped = {};
  for (const session of sessions) {
    const rawDate = session.sessionDate || session.date || fallbackDate;
    const dateKey = getCalendarDateKey(rawDate);
    if (!dateKey) continue;
    if (!grouped[dateKey]) {
      grouped[dateKey] = [];
    }
    grouped[dateKey].push(session);
  }

  // Sort dates chronologically ascending (YYYY-MM-DD lexicographical sort is chronological)
  const sortedDateEntries = Object.entries(grouped).sort(([dateA], [dateB]) => {
    return dateA.localeCompare(dateB);
  });

  // Sort sessions within each date chronologically by startTime (ascending)
  return sortedDateEntries.map(([dateKey, dateSessions]) => {
    const sortedSessions = [...dateSessions].sort((a, b) => {
      const timeA = (a.startTime || "00:00").slice(0, 5);
      const timeB = (b.startTime || "00:00").slice(0, 5);
      return timeA.localeCompare(timeB);
    });
    return [dateKey, sortedSessions];
  });
}

/**
 * FIX 04: Multi-session timing display calculation.
 * Case 1: 1 session -> actual session time ("18:00 - 19:30")
 * Case 2: multiple sessions and all have same start/end time -> "18:00 - 19:30"
 * Case 3: multiple sessions with differing start/end times -> "Multiple Sessions — See Schedule"
 * Fallback: workshop.startTime - workshop.endTime
 */
export function getWorkshopTimingDisplay(sessions, fallbackStartTime, fallbackEndTime, formatFn) {
  if (Array.isArray(sessions) && sessions.length > 0) {
    const normalizeTime = (t) => (t ? String(t).slice(0, 5) : "");
    const format = (start, end) => {
      const s = normalizeTime(start);
      const e = normalizeTime(end);
      if (formatFn) return `${formatFn(s)} - ${formatFn(e)}`;
      return `${s} - ${e}`;
    };

    if (sessions.length === 1) {
      return format(sessions[0].startTime, sessions[0].endTime);
    }

    const firstStart = normalizeTime(sessions[0].startTime);
    const firstEnd = normalizeTime(sessions[0].endTime);
    const allSame = sessions.every(
      (s) => normalizeTime(s.startTime) === firstStart && normalizeTime(s.endTime) === firstEnd
    );

    if (allSame) {
      return format(firstStart, firstEnd);
    }

    return "Multiple Sessions — See Schedule";
  }

  const s = (fallbackStartTime ? String(fallbackStartTime).slice(0, 5) : "18:00");
  const e = (fallbackEndTime ? String(fallbackEndTime).slice(0, 5) : "19:30");
  if (formatFn) return `${formatFn(s)} - ${formatFn(e)}`;
  return `${s} - ${e}`;
}

/**
 * Centralized portrait fallback for workshop banner images.
 * Returns landscapeImageUrl if present, otherwise permanent portrait imageUrl, or null.
 */
export function getWorkshopBannerImage(workshop) {
  if (!workshop) return null;
  return workshop.landscapeImageUrl || workshop.imageUrl || null;
}

/**
 * Authoritative customer-facing pass scope label from SessionsIncluded.
 * null / undefined -> All Workshops
 * 1 -> Solo
 * 2 -> Dual
 * 3 -> Trio
 * N > 3 -> N-Session Bundle
 */
export function getPassScopeLabel(sessionsIncluded) {
  if (sessionsIncluded == null) return "All Workshops";
  const n = Number(sessionsIncluded);
  if (n === 1) return "Solo";
  if (n === 2) return "Dual";
  if (n === 3) return "Trio";
  return `${n}-Session Bundle`;
}

