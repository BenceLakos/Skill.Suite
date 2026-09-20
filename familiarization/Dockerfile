FROM node:22-alpine

WORKDIR /app

ENV NODE_ENV=production \
    PORT=8080

COPY --chown=node:node package.json ./
COPY --chown=node:node src/ ./src/

USER node

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/healthz || exit 1

CMD ["node", "src/server.js"]
