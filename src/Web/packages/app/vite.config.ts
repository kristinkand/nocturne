import { defineConfig, loadEnv } from "vite";
import { sveltekit } from "@sveltejs/kit/vite";
import commonjs from "vite-plugin-commonjs";
import tailwindcss from "@tailwindcss/vite";
import { resolve } from "path";
import { setupBridge } from "@nocturne/bridge";

export default defineConfig(({ mode }) => {
  // Load env file based on `mode` in the current working directory.
  const env = loadEnv(mode, process.cwd(), "");

  return {
    assetsInclude: ["**/*.jpg", "**/*.png", "**/*.gif"],
    plugins: [
      tailwindcss(),
      sveltekit(),
      commonjs(),
      // Custom plugin to integrate WebSocket bridge into Vite dev server
      {
        name: "websocket-bridge",
        configureServer(server) {
          const SIGNALR_HUB_URL =
            env.SIGNALR_HUB_URL || "http://localhost:1612/hubs/data";
          const API_SECRET = env.API_SECRET || "";

          // Ensure the HTTP server is available before initializing the bridge
          if (!server.httpServer) {
            console.error(
              "HTTP server not available, skipping WebSocket bridge initialization"
            );
            return;
          }

          // Initialize WebSocket bridge with Vite's HTTP server
          setupBridge(server.httpServer, {
            signalr: {
              hubUrl: SIGNALR_HUB_URL,
            },
            socketio: {
              cors: {
                origin: "*",
                methods: ["GET", "POST"],
                credentials: true,
              },
            },
            apiSecret: API_SECRET,
          })
            .then((bridge) => {
              console.log("✓ WebSocket bridge initialized successfully");
              console.log(`  SignalR Hub: ${SIGNALR_HUB_URL}`);
              console.log(`  SignalR connected: ${bridge.isConnected()}`);
            })
            .catch((error) => {
              console.error("✗ Failed to initialize WebSocket bridge:", error);
              console.error(
                "  Continuing without bridge - real-time features may not work"
              );
            });
        },
      },
    ],
    server: {
      host: "0.0.0.0",
      proxy: {
        "^/api/.*": {
          target: env.PUBLIC_API_URL || "http://localhost:1612",
          changeOrigin: true,
          secure: false,
        },
      },
      fs: {
        allow: [
          "../node_modules", // This is for src/Web/packages/node_modules
          resolve(__dirname, "../../node_modules"), // This is for src/Web/node_modules
        ],
      },
    },
  };
});
