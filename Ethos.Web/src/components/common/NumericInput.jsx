import React, { useState, useEffect, useRef } from "react";

/**
 * Robust NumericInput component adhering to the 15 operational scenarios:
 * 1. Empty/null value renders as empty string "" (never forces "0").
 * 2. Initial numeric value (e.g., 500) renders as string "500".
 * 3. Strips extraneous leading zeros: typing "5" when "0" replaces it with "5" ("05" -> "5").
 * 4. Allows single "0" when allowed.
 * 5. Rejects non-digit characters on keypress/input.
 * 6. Deleting to empty calls onChange(null) or emptyValue.
 * 7. Pastes sanitized to digits (extracts numeric portion).
 * 8. Clamps to max on input, validates min on blur.
 * 9. Preserves cursor position across reformats.
 * 10. Up/Down arrow keys support stepping.
 */
export default function NumericInput({
  value,
  onChange,
  onBlur,
  onFocus,
  min = 0,
  max,
  step = 1,
  placeholder = "",
  disabled = false,
  readOnly = false,
  className = "",
  prefix,
  suffix,
  allowNull = true,
  emptyValue = null,
  id,
  name,
  "aria-label": ariaLabel,
  autoFocus = false,
  style,
  ...rest
}) {
  const [isFocused, setIsFocused] = useState(false);
  const inputRef = useRef(null);
  const cursorRef = useRef(null);

  // Derive display string from numeric value prop
  const formatValue = (v) => {
    if (v === null || v === undefined || v === "") return "";
    return String(v);
  };

  const [displayValue, setDisplayValue] = useState(() => formatValue(value));

  // Keep internal string in sync when external value changes
  useEffect(() => {
    setDisplayValue(formatValue(value));
  }, [value]);

  // Restore cursor position after DOM updates
  useEffect(() => {
    if (cursorRef.current !== null && inputRef.current) {
      inputRef.current.setSelectionRange(cursorRef.current, cursorRef.current);
      cursorRef.current = null;
    }
  }, [displayValue]);

  // Clean raw string to valid digits and strip leading zeros
  const sanitizeDigits = (raw) => {
    if (!raw) return "";
    // Keep only digits
    let digits = raw.replace(/\D/g, "");
    if (!digits) return "";
    // If digits start with '0' and has more digits, strip leading zeros ("05" -> "5", "007" -> "7")
    if (digits.length > 1 && digits.startsWith("0")) {
      digits = digits.replace(/^0+/, "");
      if (digits === "") digits = "0";
    }
    return digits;
  };

  const notifyChange = (cleanStr) => {
    if (cleanStr === "") {
      onChange(allowNull ? emptyValue : (min ?? 0));
      return;
    }

    let num = parseInt(cleanStr, 10);
    if (isNaN(num)) {
      onChange(allowNull ? emptyValue : (min ?? 0));
      return;
    }

    if (max !== undefined && max !== null && num > max) {
      num = max;
    }

    onChange(num);
  };

  const handleInputChange = (e) => {
    const rawVal = e.target.value;
    const oldSelStart = e.target.selectionStart;

    if (rawVal === "") {
      cursorRef.current = 0;
      setDisplayValue("");
      notifyChange("");
      return;
    }

    const cleanStr = sanitizeDigits(rawVal);
    let finalStr = cleanStr;

    if (max !== undefined && max !== null && cleanStr !== "") {
      const num = parseInt(cleanStr, 10);
      if (num > max) {
        finalStr = String(max);
      }
    }

    // Calculate new cursor position
    const diff = finalStr.length - rawVal.length;
    cursorRef.current = Math.max(0, (oldSelStart || 0) + diff);

    setDisplayValue(finalStr);
    notifyChange(finalStr);
  };

  const handleKeyDown = (e) => {
    if (disabled || readOnly) return;

    // Up Arrow: increment
    if (e.key === "ArrowUp") {
      e.preventDefault();
      const current = displayValue === "" ? (min ?? 0) : parseInt(displayValue, 10);
      let next = (isNaN(current) ? (min ?? 0) : current) + step;
      if (max !== undefined && max !== null && next > max) next = max;
      setDisplayValue(String(next));
      onChange(next);
      return;
    }

    // Down Arrow: decrement
    if (e.key === "ArrowDown") {
      e.preventDefault();
      const current = displayValue === "" ? (min ?? 0) : parseInt(displayValue, 10);
      let next = (isNaN(current) ? (min ?? 0) : current) - step;
      if (min !== undefined && min !== null && next < min) next = min;
      setDisplayValue(String(next));
      onChange(next);
      return;
    }
  };

  const handlePaste = (e) => {
    e.preventDefault();
    if (disabled || readOnly) return;

    const pasted = e.clipboardData.getData("text");
    const digits = sanitizeDigits(pasted);
    if (!digits) return; // ignore non-numeric paste

    let finalStr = digits;
    if (max !== undefined && max !== null) {
      const num = parseInt(digits, 10);
      if (num > max) finalStr = String(max);
    }

    cursorRef.current = finalStr.length;
    setDisplayValue(finalStr);
    notifyChange(finalStr);
  };

  const handleBlurInternal = (e) => {
    // If not empty, check min boundary
    if (displayValue !== "") {
      let num = parseInt(displayValue, 10);
      if (!isNaN(num)) {
        if (min !== undefined && min !== null && num < min) {
          num = min;
          setDisplayValue(String(num));
          onChange(num);
        }
      }
    }
    if (onBlur) onBlur(e);
  };

  const handleFocusInternal = (e) => {
    setIsFocused(true);
    if (onFocus) onFocus(e);
  };

  const hasPrefixOrSuffix = Boolean(prefix || suffix);

  if (!hasPrefixOrSuffix) {
    return (
      <input
        ref={inputRef}
        type="text"
        inputMode="numeric"
        pattern="[0-9]*"
        id={id}
        name={name}
        aria-label={ariaLabel}
        className={`numeric-input ${className}`}
        style={style}
        value={displayValue}
        onChange={handleInputChange}
        onKeyDown={handleKeyDown}
        onPaste={handlePaste}
        onBlur={handleBlurInternal}
        onFocus={handleFocusInternal}
        placeholder={placeholder}
        disabled={disabled}
        readOnly={readOnly}
        autoFocus={autoFocus}
        autoComplete="off"
        {...rest}
      />
    );
  }

  const containerStyleKeys = [
    "width", "maxWidth", "minWidth", "flex", "margin", "marginTop", "marginBottom",
    "marginLeft", "marginRight", "border", "borderColor", "borderWidth", "borderStyle",
    "borderRadius", "background", "backgroundColor", "boxShadow", "height", "boxSizing"
  ];

  const containerStyle = {};
  const innerInputStyle = {};

  if (style) {
    Object.entries(style).forEach(([k, v]) => {
      if (containerStyleKeys.includes(k)) {
        containerStyle[k] = v;
      } else {
        innerInputStyle[k] = v;
      }
    });
  }

  return (
    <div
      className={`numeric-input-container ${isFocused ? "is-focused" : ""} ${disabled ? "is-disabled" : ""} ${className}`}
      style={containerStyle}
      onClick={() => inputRef.current?.focus()}
    >
      {prefix && <span className="numeric-input-prefix">{prefix}</span>}
      <input
        ref={inputRef}
        type="text"
        inputMode="numeric"
        pattern="[0-9]*"
        id={id}
        name={name}
        aria-label={ariaLabel}
        className="numeric-input with-adornment"
        style={innerInputStyle}
        value={displayValue}
        onChange={handleInputChange}
        onKeyDown={handleKeyDown}
        onPaste={handlePaste}
        onBlur={handleBlurInternal}
        onFocus={handleFocusInternal}
        placeholder={placeholder}
        disabled={disabled}
        readOnly={readOnly}
        autoFocus={autoFocus}
        autoComplete="off"
        {...rest}
      />
      {suffix && <span className="numeric-input-suffix">{suffix}</span>}
    </div>
  );
}

