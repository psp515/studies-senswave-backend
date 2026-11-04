// Shared options for every scenario. The runner passes everything as env vars (see performance/README.md).
export const TRANSPORT = (__ENV.TRANSPORT || 'rest').toLowerCase();
export const BASE_URL = (__ENV.BASE_URL || 'https://localhost:8443').replace(/\/+$/, '');
export const MODE = (__ENV.MODE || 'run').toLowerCase();
export const RATE = parseInt(__ENV.RATE || '100', 10);
export const DURATION = __ENV.DURATION || '30s';
export const DATA_FILE = __ENV.DATA_FILE || '';
export const INSECURE = (__ENV.INSECURE || 'false') === 'true';

if (!['rest', 'grpc', 'grpcweb'].includes(TRANSPORT)) {
    throw new Error(`Unknown TRANSPORT '${TRANSPORT}', expected rest | grpc | grpcweb`);
}

// Open model: a constant arrival rate, so every transport gets exactly the same load.
export function options() {
    const base = {
        insecureSkipTLSVerify: INSECURE,
        summaryTrendStats: ['avg', 'min', 'p(50)', 'p(90)', 'p(95)', 'p(99)', 'max'],
        discardResponseBodies: false,
    };

    if (MODE === 'verify') {
        return { ...base, scenarios: { verify: { executor: 'per-vu-iterations', vus: 1, iterations: 1 } }, thresholds: { checks: ['rate==1'] } };
    }

    return {
        ...base,
        scenarios: {
            load: {
                executor: 'constant-arrival-rate',
                rate: RATE,
                timeUnit: '1s',
                duration: DURATION,
                // The pool grows with latency up to 2 VUs per request/s (one connection per VU), so the client caps only at a
                // latency of 2 s per request. A slower transport therefore opens more connections (README, section 8).
                preAllocatedVUs: Math.max(20, Math.ceil(RATE * 0.2)),
                maxVUs: Math.max(100, RATE * 2),
            },
        },
    };
}
