import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const API = process.env.VITE_API_TARGET ?? "http://localhost:5080";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // So you can open the app on your actual phone over the LAN, which is the only
    // way to find out what this really feels like.
    host: true,
    // Proxying the API through the dev server keeps the client same-origin, which means
    // CORS never comes up in development and the app talks to relative paths in both
    // environments. ws:true matters - SignalR upgrades /hub to a WebSocket.
    proxy: {
      "/hub": { target: API, ws: true, changeOrigin: true },
      "/api": { target: API, changeOrigin: true },
      "/healthz": { target: API, changeOrigin: true },
    },
  },
});
