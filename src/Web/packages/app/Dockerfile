# Build stage
FROM node:24-alpine AS builder

# Install pnpm
RUN npm install -g pnpm

WORKDIR /workspace

# Copy workspace root files (context is src/Web/)
COPY pnpm-workspace.yaml ./
COPY pnpm-lock.yaml ./
COPY package.json* ./

# Copy all packages (needed for workspace resolution)
COPY packages ./packages

# Install all dependencies for the workspace
RUN pnpm install --frozen-lockfile

# Build the bridge package first (workspace dependency)
WORKDIR /workspace/packages/bridge
RUN pnpm run build

# Build the app package
WORKDIR /workspace/packages/app
RUN pnpm run build

# Production stage
FROM node:24-alpine

# Install pnpm
RUN npm install -g pnpm

WORKDIR /app

# Copy workspace files needed for production
COPY --from=builder /workspace/pnpm-workspace.yaml ./
COPY --from=builder /workspace/pnpm-lock.yaml ./
COPY --from=builder /workspace/package.json ./

# Copy package.json files for both packages
COPY --from=builder /workspace/packages/app/package.json ./packages/app/
COPY --from=builder /workspace/packages/bridge/package.json ./packages/bridge/

# Install production dependencies only
RUN pnpm install --frozen-lockfile --prod

# Copy built artifacts
COPY --from=builder /workspace/packages/app/build ./packages/app/build
COPY --from=builder /workspace/packages/app/server.js ./packages/app/
COPY --from=builder /workspace/packages/bridge/dist ./packages/bridge/dist

# Set working directory to app package
WORKDIR /app/packages/app

# Accept runtime environment variables
ENV NODE_ENV=production
ENV PORT=5173

# Expose the port
EXPOSE 5173

# Start the production server
CMD ["node", "server.js"]
