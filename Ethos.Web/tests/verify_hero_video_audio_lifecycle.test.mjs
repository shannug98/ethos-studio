import { test, describe } from "node:test";
import assert from "node:assert";
import fs from "node:fs";
import path from "node:path";

describe("Homepage Hero Video Audio Lifecycle Invariants", () => {
  const heroPath = path.resolve("src/components/home/Hero.jsx");
  const heroCode = fs.readFileSync(heroPath, "utf-8");

  test("1. Registers video elements in an indexed videoRefs array rather than a single ref", () => {
    assert.ok(heroCode.includes("const videoRefs = useRef([]);"), "Must declare videoRefs array ref");
    assert.ok(!heroCode.includes("const videoRef = useRef(null);"), "Must not use single videoRef");
    assert.ok(heroCode.includes("videoRefs.current[index] = el;"), "Must register indexed element into videoRefs.current");
  });

  test("2. Contains an unmount cleanup effect that pauses and mutes all mounted videos", () => {
    assert.ok(heroCode.includes("videoRefs.current.forEach((el) => {"), "Must iterate videoRefs in unmount cleanup");
    assert.ok(heroCode.includes("el.pause();"), "Must pause video on unmount");
    assert.ok(heroCode.includes("el.currentTime = 0;"), "Must reset currentTime to 0");
    assert.ok(heroCode.includes("el.muted = true;"), "Must set muted = true on unmount");
  });

  test("3. Authoritatively synchronizes active and inactive video states on slide transition", () => {
    assert.ok(heroCode.includes("slides.forEach((slide, idx) => {"), "Must iterate all slides");
    assert.ok(heroCode.includes("const videoEl = videoRefs.current[idx];"), "Must lookup corresponding video element");
    assert.ok(heroCode.includes('if (idx === activeSlide && slide.type === "video") {'), "Must check active slide video");
    assert.ok(heroCode.includes("videoEl.muted = isHeroMuted;"), "Must apply isHeroMuted to active video");
    assert.ok(heroCode.includes("videoEl.currentTime = 0;"), "Must reset active video time");
    assert.ok(heroCode.includes("videoEl.play().catch(() => {});"), "Must play active video");
    assert.ok(heroCode.includes("videoEl.pause();"), "Must pause inactive video");
    assert.ok(heroCode.includes("videoEl.muted = true;"), "Must hard-mute inactive video");
  });

  test("4. Binds muted attribute strictly so inactive slides remain hard-muted in JSX", () => {
    assert.ok(heroCode.includes("muted={isActive ? isHeroMuted : true}"), "Must bind muted conditionally on active state");
  });

  test("5. Toggles mute on the active slide element only", () => {
    assert.ok(heroCode.includes("const currentVideo = videoRefs.current[activeSlide];"), "Must target active slide in sound toggle");
    assert.ok(heroCode.includes('if (currentVideo && slides[activeSlide]?.type === "video") {'), "Must guard active slide video");
    assert.ok(heroCode.includes("currentVideo.muted = nextMuted;"), "Must update muted on active video element only");
  });
});
