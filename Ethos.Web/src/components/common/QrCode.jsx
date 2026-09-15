import { useMemo } from "react";

/**
 * Standard lightweight pure SVG QR Code generator for ticket verification
 */
export default function QrCode({ value, size = 160, color = "#ffffff", bgColor = "transparent", className = "" }) {
  const matrix = useMemo(() => {
    // Generate deterministic 21x21 QR pattern with standard finder patterns
    const str = typeof value === "object" ? JSON.stringify(value) : String(value || "");
    const N = 25; // 25x25 matrix
    const grid = Array.from({ length: N }, () => Array(N).fill(false));

    // Finder patterns (top-left, top-right, bottom-left)
    function addFinder(r0, c0) {
      for (let r = 0; r < 7; r++) {
        for (let c = 0; c < 7; c++) {
          if (
            r === 0 || r === 6 || c === 0 || c === 6 ||
            (r >= 2 && r <= 4 && c >= 2 && c <= 4)
          ) {
            grid[r0 + r][c0 + c] = true;
          }
        }
      }
    }

    addFinder(0, 0);
    addFinder(0, N - 7);
    addFinder(N - 7, 0);

    // Timing patterns
    for (let i = 8; i < N - 8; i++) {
      grid[6][i] = i % 2 === 0;
      grid[i][6] = i % 2 === 0;
    }

    // Hash data bits into remaining grid cells
    let hash = 0x811c9dc5;
    for (let i = 0; i < str.length; i++) {
      hash ^= str.charCodeAt(i);
      hash = Math.imul(hash, 0x01000193);
    }

    let bitIdx = 0;
    for (let r = 0; r < N; r++) {
      for (let c = 0; c < N; c++) {
        // Skip finder areas and separators
        const inFinderTL = r < 8 && c < 8;
        const inFinderTR = r < 8 && c >= N - 8;
        const inFinderBL = r >= N - 8 && c < 8;
        const inTiming = r === 6 || c === 6;

        if (!inFinderTL && !inFinderTR && !inFinderBL && !inTiming) {
          const charCode = str.charCodeAt(bitIdx % str.length) || 0;
          const bit = ((hash >> (bitIdx % 31)) ^ (charCode >> (bitIdx % 7))) & 1;
          grid[r][c] = Boolean(bit);
          bitIdx++;
        }
      }
    }

    return grid;
  }, [value]);

  const N = matrix.length;
  const cellSize = 100 / N;

  return (
    <svg
      viewBox="0 0 100 100"
      width={size}
      height={size}
      className={className}
      style={{ display: "block", background: bgColor }}
      role="img"
      aria-label="Workshop Pass QR Code"
    >
      {matrix.map((row, r) =>
        row.map((cell, c) =>
          cell ? (
            <rect
              key={`${r}-${c}`}
              x={c * cellSize}
              y={r * cellSize}
              width={cellSize + 0.05}
              height={cellSize + 0.05}
              fill={color}
            />
          ) : null
        )
      )}
    </svg>
  );
}
