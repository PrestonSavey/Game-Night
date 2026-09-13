import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { Preview } from "./preview/Preview";
import "./design/app.css";

// http://localhost:5173/?preview walks every screen with fake data and no server.
const preview = new URLSearchParams(window.location.search).has("preview");

createRoot(document.getElementById("root")!).render(
  <StrictMode>{preview ? <Preview /> : <App />}</StrictMode>,
);
