const RAZORPAY_SCRIPT =
  "https://checkout.razorpay.com/v1/checkout.js";

let razorpayPromise = null;

export function loadRazorpay() {
  if (window.Razorpay) {
    return Promise.resolve(true);
  }

  if (razorpayPromise) {
    return razorpayPromise;
  }

  razorpayPromise = new Promise((resolve, reject) => {
    const existingScript = document.querySelector(
      `script[src="${RAZORPAY_SCRIPT}"]`
    );

    if (existingScript) {
      existingScript.addEventListener("load", () =>
        resolve(true)
      );

      existingScript.addEventListener("error", () =>
        reject(
          new Error(
            "Unable to load Razorpay checkout."
          )
        )
      );

      return;
    }

    const script = document.createElement("script");

    script.src = RAZORPAY_SCRIPT;
    script.async = true;

    script.onload = () => resolve(true);

    script.onerror = () => {
      razorpayPromise = null;

      reject(
        new Error(
          "Unable to load Razorpay checkout."
        )
      );
    };

    document.body.appendChild(script);
  });

  return razorpayPromise;
}
