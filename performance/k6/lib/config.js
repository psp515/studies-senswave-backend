// Shared options for every scenario. The runner passes everything as env vars (see performance/README.md).
export const TRANSPORT = (__ENV.TRANSPORT || 'rest').toLowerCase();
export const BASE_URL = (__ENV.BASE_URL || 'https://localhost:8443').replace(/\/+$/, '');
export const MODE = (__ENV.MODE || 'run').toLowerCase();
export const RATE = parseInt(__ENV.RATE || '100', 10);
export const DURATION = __ENV.DURATION || '30s';
export const WARMUP = parseInt(__ENV.WARMUP || '0', 10);
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

    // A fixed pool (one connection per VU), so the client caps at an average latency of about 1 s per request and every
    // transport holds the same VUs. k6 counts a dropped iteration whenever no VU is free, even below maxVUs.
    const vus = Math.max(100, RATE);
    const load = { executor: 'constant-arrival-rate', rate: RATE, timeUnit: '1s', preAllocatedVUs: vus, maxVUs: vus };

    // Warm-up and measurement share one process, so the VUs and their connections carry over into the measurement.
    // gracefulStop 0s: the phases must not overlap in k6's plan, otherwise the measurement gets its own VUs.
    const scenarios = { measure: { ...load, duration: DURATION, startTime: `${WARMUP}s` } };
    if (WARMUP > 0) {
        scenarios.warmup = { ...load, duration: `${WARMUP}s`, gracefulStop: '0s' };
    }

    return {
        ...base,
        scenarios,
        // Always true: a threshold is what makes k6 export the metric of the measurement phase alone.
        thresholds: {
            'dropped_iterations{scenario:measure}': ['count>=0'],
            'iterations{scenario:measure}': ['count>=0'],
            'checks{scenario:measure}': ['rate>=0'],
            'http_req_duration{scenario:measure}': ['max>=0'],
            'grpc_req_duration{scenario:measure}': ['max>=0'],
            'data_sent{scenario:measure}': ['count>=0'],
            'data_received{scenario:measure}': ['count>=0'],
        },
    };
}
