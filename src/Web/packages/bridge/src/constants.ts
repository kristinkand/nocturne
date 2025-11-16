/**
 * Centralized constants for the WebSocket Bridge
 * These should match the constants defined in the C# Constants project
 */

export const PortConstants = {
  // Legacy Nightscout
  LegacyNightscout: 1337,

  // Nocturne API Ports
  NocturneApiHttp: 1612,
  NocturneApiHttps: 1612,

  // WebSocket Bridge Ports
  SocketIo: 1613,
  WebSocketHealth: 1614,

  // Database Ports
  MongoDb: 27017,
  Redis: 6379,

  // Frontend Ports
  ViteDev: 5173,
  VitePreview: 4173,

  // Infrastructure Ports
  AspireDashboardHttp: 15888,
} as const;

export const UrlConstants = {
  Base: {
    LegacyNightscout: `http://localhost:${PortConstants.LegacyNightscout}`,
    NocturneApiHttp: `http://localhost:${PortConstants.NocturneApiHttp}`,
    NocturneApiHttps: `https://localhost:${PortConstants.NocturneApiHttps}`,
    FrontendDev: `http://localhost:${PortConstants.ViteDev}`,
    FrontendPreview: `http://localhost:${PortConstants.VitePreview}`,
    WebSocketBridge: `http://localhost:${PortConstants.SocketIo}`,
    WebSocketHealth: `http://localhost:${PortConstants.WebSocketHealth}`,
  },

  SignalR: {
    DataHub: "/hubs/data",
    NotificationHub: "/hubs/notification",
    get DataHubUrl() {
      return UrlConstants.Base.NocturneApiHttp + this.DataHub;
    },
    get NotificationHubUrl() {
      return UrlConstants.Base.NocturneApiHttp + this.NotificationHub;
    },
  },

  Health: {
    Check: "/health",
    Stats: "/stats",
    Ready: "/ready",
    Live: "/live",
  },
} as const;

export const ServiceNames = {
  NocturneApi: "nocturne-api",
  WebSocketBridge: "websocket-bridge",
  MongoDb: "mongodb",
  Redis: "redis",
} as const;

export const EnvironmentVariables = {
  SignalRHubUrl: "SIGNALR_HUB_URL",
  SocketIoPort: "SOCKETIO_PORT",
  HealthPort: "HEALTH_PORT",
  CorsOrigins: "CORS_ORIGINS",
  LogLevel: "LOG_LEVEL",
  NodeEnv: "NODE_ENV",
} as const;
