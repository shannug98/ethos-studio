/**
 * pricingUxHelpers.js
 * Pure JS helper functions for Step 5 Ticket Pricing:
 * - Mathematical tier validation matching backend ValidateTicketTypePricingTiers
 * - Simulated cart split quote calculation matching WorkshopPricingService
 * - Pass category classification matrix
 */

export function classifyPassCategory(pass) {
  if (!pass) return "ALL_ACCESS";
  const hasTarget = Boolean(pass.workshopSessionId || pass.targetSessionClientId);
  if (pass.category === "SINGLE") {
    return Number(pass.sessionsIncluded) === 1 ? "SINGLE" : "INVALID_SINGLE";
  }
  if (pass.category === "BUNDLE") {
    return (!hasTarget && Number(pass.sessionsIncluded) >= 2) ? "BUNDLE" : "INVALID_BUNDLE";
  }
  if (pass.category === "ALL_ACCESS") {
    return "ALL_ACCESS";
  }
  if (hasTarget) {
    return Number(pass.sessionsIncluded) === 1 ? "SINGLE" : "INVALID_SINGLE";
  }
  if (pass.sessionsIncluded != null) {
    return Number(pass.sessionsIncluded) >= 2 ? "BUNDLE" : "INVALID_BUNDLE";
  }
  return "ALL_ACCESS";
}

export function calculateSimulatedQuote(tiers, totalCapacity, soldCount, requestedQty) {
  const qty = Number(requestedQty) || 0;
  const sold = Number(soldCount) || 0;
  const cap = Number(totalCapacity) || 0;

  if (qty <= 0) {
    return { error: "Quantity must be at least 1 ticket.", splits: [], total: 0, avgPrice: 0 };
  }

  if (cap > 0 && sold + qty > cap) {
    return {
      error: `Capacity exceeded: ${sold + qty} requested tickets exceeds commercial quota of ${cap} (Remaining: ${Math.max(0, cap - sold)}).`,
      splits: [],
      total: 0,
      avgPrice: 0,
      isCapacityExceeded: true,
    };
  }

  if (!Array.isArray(tiers) || tiers.length === 0) {
    return { error: "No pricing tiers defined.", splits: [], total: 0, avgPrice: 0 };
  }

  const splits = [];
  let remainingToQuote = qty;
  let currentTicketNum = sold + 1;

  for (let i = 0; i < tiers.length; i++) {
    if (remainingToQuote <= 0) break;
    const tier = tiers[i];
    const minT = Number(tier.minTickets) || 1;
    const maxT = tier.maxTickets != null && tier.maxTickets !== "" ? Number(tier.maxTickets) : null;
    const price = Number(tier.price) || 0;

    if (maxT !== null && currentTicketNum > maxT) {
      continue;
    }

    const availableInThisTier = maxT === null ? remainingToQuote : Math.max(0, maxT - currentTicketNum + 1);
    const quantityInThisTier = Math.min(remainingToQuote, availableInThisTier);

    if (quantityInThisTier > 0) {
      splits.push({
        tierNumber: tier.tierNumber || i + 1,
        tierName: tier.tierName || `Tier ${i + 1}`,
        unitPrice: price,
        quantity: quantityInThisTier,
        subtotal: quantityInThisTier * price,
        rangeLabel: maxT ? `${minT}–${maxT}` : `${minT}+`,
      });
      remainingToQuote -= quantityInThisTier;
      currentTicketNum += quantityInThisTier;
    }
  }

  if (remainingToQuote > 0) {
    return {
      error: `Tiers do not cover up to ticket #${currentTicketNum + remainingToQuote - 1}. Add an open-ended final tier.`,
      splits: [],
      total: 0,
      avgPrice: 0,
    };
  }

  const total = splits.reduce((sum, s) => sum + s.subtotal, 0);
  const avgPrice = total / qty;

  let scenarioLabel = "Scenario A: Single Tier Order";
  if (splits.length === 2) {
    scenarioLabel = "Scenario B: Split Across 2 Tiers";
  } else if (splits.length > 2) {
    scenarioLabel = `Scenario C: Split Across ${splits.length} Tiers`;
  } else if (
    tiers.length > 0 &&
    tiers[tiers.length - 1].maxTickets == null &&
    splits[0]?.tierNumber === (tiers[tiers.length - 1].tierNumber || tiers.length)
  ) {
    scenarioLabel = "Scenario D: Open-Ended Final Tier";
  }

  return {
    error: null,
    splits,
    total,
    avgPrice,
    scenarioLabel,
  };
}

export function getTierValidation(tiers, totalCapacity) {
  if (!Array.isArray(tiers) || tiers.length === 0) {
    return { valid: false, message: "At least one pricing tier must be defined." };
  }
  if (tiers[0].minTickets !== 1) {
    return { valid: false, message: `Tier 1 must start at Min Tickets = 1 (currently ${tiers[0].minTickets}).` };
  }
  for (let i = 0; i < tiers.length; i++) {
    const curr = tiers[i];
    if (curr.price == null || curr.price <= 0) {
      return { valid: false, message: `Tier ${i + 1} price must be greater than zero.` };
    }
    if (curr.maxTickets != null && curr.maxTickets < curr.minTickets) {
      return { valid: false, message: `Tier ${i + 1} max tickets (${curr.maxTickets}) cannot be less than min (${curr.minTickets}).` };
    }
    if (i < tiers.length - 1 && curr.maxTickets == null) {
      return { valid: false, message: `Only the final tier (Tier ${tiers.length}) can be open-ended (unlimited).` };
    }
    if (i > 0) {
      const prev = tiers[i - 1];
      if (curr.price < prev.price) {
        return {
          valid: false,
          message: `Non-decreasing pricing rule violated: Tier ${i + 1} (₹${curr.price}) cannot be cheaper than Tier ${i} (₹${prev.price}).`,
        };
      }
      if (prev.maxTickets != null && curr.minTickets !== prev.maxTickets + 1) {
        return {
          valid: false,
          message: `Discontinuity: Tier ${i} ends at ticket ${prev.maxTickets}, but Tier ${i + 1} starts at ticket ${curr.minTickets}.`,
        };
      }
    }
  }
  return { valid: true, message: "All tiers are contiguous, positive, and monotonic non-decreasing." };
}

/**
 * Derives the human-readable pass scope label from SessionsIncluded.
 * This is the authoritative frontend display helper — keeps frontend aligned with backend GetPassCategory().
 * null → All Workshops
 * 1 → Solo
 * 2 → Dual
 * 3 → Trio
 * N>3 → N-Session Bundle
 */
export function getPassScopeLabel(sessionsIncluded) {
  if (sessionsIncluded == null) return 'All Workshops';
  const n = Number(sessionsIncluded);
  if (n === 1) return 'Solo';
  if (n === 2) return 'Dual';
  if (n === 3) return 'Trio';
  return `${n}-Session Bundle`;
}

/**
 * Derives the pass category key from sessionsIncluded.
 * Mirrors backend WorkshopPassType.GetPassCategory().
 */
export function getPassCategoryKey(sessionsIncluded) {
  if (sessionsIncluded == null) return 'ALL_ACCESS';
  const n = Number(sessionsIncluded);
  if (n === 1) return 'SINGLE';
  return 'BUNDLE'; // 2, 3, or more
}
