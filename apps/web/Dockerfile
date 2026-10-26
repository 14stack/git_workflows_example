# syntax=docker/dockerfile:1
ARG NODE_VERSION=24

# ---------------------------------------------------------------------------
FROM node:${NODE_VERSION}-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npx ng build --configuration production

# ---------------------------------------------------------------------------
# Unprivileged nginx (stable line): runs as UID 101 and listens on 8080.
FROM nginxinc/nginx-unprivileged:1.30-alpine AS runtime
# Pick up Alpine security fixes published after the base image was built.
USER root
RUN apk upgrade --no-cache
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/web/browser/ /usr/share/nginx/html/
USER 101
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=3 \
  CMD wget -qO- http://127.0.0.1:8080/healthz || exit 1
