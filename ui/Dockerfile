FROM node:22-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine
# Copy custom nginx configuration (see below)
COPY nginx.conf /etc/nginx/conf.d/default.conf
# Copy built files from the builder stage to NGINX html directory
COPY --from=builder /app/dist/browser /usr/share/nginx/html
ENV NODE_ENV=production
EXPOSE 80

CMD [ "nginx", "-g", "daemon off;" ]
