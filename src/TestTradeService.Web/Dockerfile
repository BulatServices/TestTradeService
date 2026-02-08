FROM node:20-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
ARG VITE_API_BASE_URL=http://localhost:5000
ARG VITE_SIGNALR_URL=http://localhost:5000/hubs/market-data
ARG VITE_REQUEST_TIMEOUT_MS=15000
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_SIGNALR_URL=$VITE_SIGNALR_URL
ENV VITE_REQUEST_TIMEOUT_MS=$VITE_REQUEST_TIMEOUT_MS
RUN npm run build

FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
