import { createServer } from 'node:http';

const SERVICE_NAME = 'familiarization-api';
const COMPETITOR_NAME_VARIABLE = 'COMPETITOR_NAME';
const DEFAULT_PORT = 8080;
const HOST = '0.0.0.0';

const ROUTE_ROOT = '/';
const ROUTE_HEALTH = '/healthz';

const METHOD_GET = 'GET';
const CONTENT_TYPE_HEADER = 'content-type';
const CONTENT_TYPE_JSON = 'application/json; charset=utf-8';

const STATUS_OK = 200;
const STATUS_NOT_FOUND = 404;
const STATUS_METHOD_NOT_ALLOWED = 405;

const EXIT_OK = 0;
const EXIT_MISCONFIGURED = 1;
const SHUTDOWN_SIGNALS = ['SIGTERM', 'SIGINT'];

const HEALTH_BODY = { status: 'ok' };
const NOT_FOUND_BODY = { error: 'not found' };
const METHOD_NOT_ALLOWED_BODY = { error: 'method not allowed' };

function readCompetitorName() {
  const value = process.env[COMPETITOR_NAME_VARIABLE];
  if (typeof value !== 'string' || value.trim() === '') {
    process.stderr.write(
      `${SERVICE_NAME}: environment variable ${COMPETITOR_NAME_VARIABLE} is not set or is blank. ` +
        `Set ${COMPETITOR_NAME_VARIABLE} to your competitor user name and start the service again.\n`
    );
    process.exit(EXIT_MISCONFIGURED);
  }
  return value.trim();
}

function readPort() {
  const parsed = Number.parseInt(process.env.PORT ?? '', 10);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : DEFAULT_PORT;
}

function respond(response, statusCode, body) {
  const payload = JSON.stringify(body);
  response.writeHead(statusCode, { [CONTENT_TYPE_HEADER]: CONTENT_TYPE_JSON });
  response.end(payload);
}

const competitorName = readCompetitorName();
const port = readPort();

const server = createServer((request, response) => {
  if (request.method !== METHOD_GET) {
    respond(response, STATUS_METHOD_NOT_ALLOWED, METHOD_NOT_ALLOWED_BODY);
    return;
  }

  const path = new URL(request.url ?? ROUTE_ROOT, `http://${request.headers.host ?? HOST}`).pathname;

  if (path === ROUTE_HEALTH) {
    respond(response, STATUS_OK, HEALTH_BODY);
    return;
  }

  if (path === ROUTE_ROOT) {
    respond(response, STATUS_OK, {
      competitor: competitorName,
      service: SERVICE_NAME,
      time: new Date().toISOString()
    });
    return;
  }

  respond(response, STATUS_NOT_FOUND, NOT_FOUND_BODY);
});

for (const signal of SHUTDOWN_SIGNALS) {
  process.on(signal, () => {
    server.close(() => process.exit(EXIT_OK));
  });
}

server.listen(port, HOST, () => {
  process.stdout.write(`${SERVICE_NAME} listening on port ${port} for competitor ${competitorName}\n`);
});
