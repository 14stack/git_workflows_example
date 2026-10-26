# syntax=docker/dockerfile:1
# Base images are pinned by tag + digest (reproducible builds); Dependabot bumps both.

# ---------------------------------------------------------------------------
FROM node:24-alpine@sha256:06d7adf3e0ef61a13f1d28682726366f97a15aa1888c934d3ecec86c33918b53 AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npx ng build --configuration production

# ---------------------------------------------------------------------------
# Unprivileged nginx (stable line): runs as UID 101 and listens on 8080.
FROM nginxinc/nginx-unprivileged:1.30-alpine@sha256:f5116ed4606f3eb015dd4a8e28cb9b351476dc0ad4bc6e9f93b19cec130cdf03 AS runtime
# Pick up Alpine security fixes published after the base image was built: the nginx images
# often lag behind Alpine's security updates, which fails the nightly container scan.
USER root
# nosemgrep: avoid-apk-upgrade
RUN apk upgrade --no-cache
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist/web/browser/ /usr/share/nginx/html/
USER 101
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=3 \
  CMD ["wget", "-q", "-O", "/dev/null", "http://127.0.0.1:8080/healthz"]
