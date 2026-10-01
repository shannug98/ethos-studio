import React, { useMemo } from "react";
import QRCode from "qrcode";

/**
 * Standards-compliant (ISO/IEC 18004) SVG QR Code generator for ticket verification
 */
export default function QrCode({
  value,
  size = 180,
  color = "#000000",
  bgColor = "#ffffff",
  margin = 4,
  className = "",
  errorCorrectionLevel = "M",
}) {
  const qrData = useMemo(() => {
    try {
      const text = typeof value === "object" ? JSON.stringify(value) : String(value || "");
      if (!text || text.trim() === "") return null;
      const qr = QRCode.create(text, { errorCorrectionLevel });
      return qr.modules;
    } catch (err) {
      console.warn("QR Code generation error:", err);
      return null;
    }
  }, [value, errorCorrectionLevel]);

  if (!qrData) {
    return (
      <div
        style={{
          width: size,
          height: size,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          background: bgColor === "transparent" ? "#1e293b" : bgColor,
          color: color,
          borderRadius: "8px",
        }}
        className={className}
      >
        <span style={{ fontSize: "11px", opacity: 0.7 }}>QR Code</span>
      </div>
    );
  }

  const N = qrData.size;
  const totalSize = N + margin * 2;
  const cells = [];

  for (let r = 0; r < N; r++) {
    for (let c = 0; c < N; c++) {
      if (qrData.get(r, c)) {
        cells.push(
          <rect
            key={`${r}-${c}`}
            x={c + margin}
            y={r + margin}
            width={1.02}
            height={1.02}
            fill={color}
          />
        );
      }
    }
  }

  return (
    <svg
      viewBox={`0 0 ${totalSize} ${totalSize}`}
      width={size}
      height={size}
      className={className}
      style={{ display: "block", background: bgColor, shapeRendering: "crispEdges" }}
      role="img"
      aria-label="Workshop Pass QR Code"
    >
      {bgColor && bgColor !== "transparent" && (
        <rect width={totalSize} height={totalSize} fill={bgColor} />
      )}
      {cells}
    </svg>
  );
}
