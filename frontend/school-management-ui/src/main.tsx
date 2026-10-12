import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
// U1: the app font, bundled with the app (no request to a font service at runtime).
import "@fontsource/plus-jakarta-sans/400.css";
import "@fontsource/plus-jakarta-sans/500.css";
import "@fontsource/plus-jakarta-sans/600.css";
import "@fontsource/plus-jakarta-sans/700.css";
import "@fontsource/plus-jakarta-sans/800.css";
import "@/assets/theme.css";
import "@/assets/layout.css";
import "@/assets/components.css";
import "./index.css";
import App from "./App.tsx";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
