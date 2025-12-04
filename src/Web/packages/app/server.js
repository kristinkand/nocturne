import { handler } from './build/handler.js';
import express from 'express';
import { createServer } from 'http';
import { setupBridge } from '@nocturne/bridge';

const app = express();
const httpServer = createServer(app);

const PORT = process.env.PORT || 3000;
const API_URL = process.env.NOCTURNE_API_URL || process.env.PUBLIC_API_URL || 'http://localhost:1612';
const SIGNALR_HUB_URL = `${API_URL}/hubs/data`;
const API_SECRET = process.env.API_SECRET || '';

// Initialize WebSocket bridge
try {
  const bridge = await setupBridge(httpServer, {
    signalr: {
      hubUrl: SIGNALR_HUB_URL
    },
    socketio: {
      cors: {
        origin: '*',
        methods: ['GET', 'POST'],
        credentials: true
      }
    },
    apiSecret: API_SECRET
  });

  console.log('WebSocket bridge initialized successfully');
  console.log(`SignalR connected: ${bridge.isConnected()}`);
} catch (error) {
  console.error('Failed to initialize WebSocket bridge:', error);
  // Continue without bridge - the app can still function
}

// Use SvelteKit handler
app.use(handler);

httpServer.listen(PORT, () => {
  console.log(`Server listening on port ${PORT}`);
});
