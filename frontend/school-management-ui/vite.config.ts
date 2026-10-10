import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "path";

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    // D2: the browser calls /api on the Vite origin and Vite forwards it to the
    // API. One origin means the refresh-token cookie is first-party and can be
    // SameSite=Strict, and no CORS is needed. Start the API with the "https"
    // launch profile (https://localhost:7100).
    proxy: {
      "/api": {
        target: "https://localhost:7100",
        changeOrigin: true,
        secure: false, // the ASP.NET Core dev certificate is self-signed
      },
    },
  },
});
