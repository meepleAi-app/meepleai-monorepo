# ==============================================================================
# Stage 1: Dependencies - Install all dependencies
# ==============================================================================
FROM node:20-alpine AS deps
WORKDIR /app

# Install build dependencies for canvas package (Issue #4168)
# canvas requires Python, make, g++, and Cairo graphics libraries
RUN apk add --no-cache \
    python3 \
    make \
    g++ \
    cairo-dev \
    jpeg-dev \
    pango-dev \
    giflib-dev \
    pixman-dev

# Install pnpm globally
RUN npm install -g pnpm@10

# Copy package files
COPY package.json pnpm-lock.yaml ./

# Install dependencies (including devDependencies for build stage)
RUN pnpm install --frozen-lockfile

# ==============================================================================
# Stage 2: Builder - Build the Next.js application
# ==============================================================================
FROM node:20-alpine AS builder
WORKDIR /app

# Install pnpm globally
RUN npm install -g pnpm@10

# Copy dependencies from deps stage
COPY --from=deps /app/node_modules ./node_modules

# Copy application source
COPY . .

# Set environment to production
ENV NODE_ENV=production

# Ensure public directory exists (Next.js may need it)
RUN mkdir -p public

# Copy PDF.js worker to public for client-side access
# Worker version must match the pdfjs-dist bundled by react-pdf (resolved via pnpm)
# Issue #4168: Try .mjs first (v5+), fallback to .js (v3)
RUN WORKER_PATH=$(find node_modules -path "*/pdfjs-dist/build/pdf.worker.min.mjs" -o -path "*/pdfjs-dist/build/pdf.worker.min.js" | head -1) && \
    cp "$WORKER_PATH" public/pdf.worker.min.js || cp "$WORKER_PATH" public/pdf.worker.min.mjs

# Build Next.js application (generates .next/standalone)
RUN pnpm run build

# ==============================================================================
# Stage 3: Runner - Production runtime environment
# ==============================================================================
FROM node:20-alpine AS runner
WORKDIR /app

# Set environment to production
ENV NODE_ENV=production

# Install curl for healthcheck
RUN apk add --no-cache curl

# Create non-root user for security
RUN addgroup -g 1001 -S nodejs && \
    adduser -S nextjs -u 1001

# Copy necessary files from builder
# Next.js standalone output includes only necessary files
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static
COPY --from=builder --chown=nextjs:nodejs /app/public ./public

# Switch to non-root user
USER nextjs

# Expose port
EXPOSE 3000

# Set hostname to allow connections from all interfaces
ENV HOSTNAME="0.0.0.0"
ENV PORT=3000

# Start the application
CMD ["node", "server.js"]
