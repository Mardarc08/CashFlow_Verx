# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM node:20-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci --silent

COPY . .
RUN npm run build:prod

# ── Stage 2: Serve com Nginx ──────────────────────────────────────────────────
FROM nginx:1.25-alpine AS final

# Remove config padrão
RUN rm /etc/nginx/conf.d/default.conf

# Config customizada para SPA Angular (redireciona tudo para index.html)
COPY nginx.conf /etc/nginx/conf.d/default.conf

COPY --from=build /app/dist/cashflow-dashboard/browser /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
