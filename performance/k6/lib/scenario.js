import { check } from 'k6';
import exec from 'k6/execution';
import { BASE_URL, TRANSPORT } from './config.js';
import * as grpcClient from './grpc.js';
import * as grpcWeb from './grpcweb.js';

// Runs one operation over the selected transport with the same two checks everywhere:
// "ok" (success status) and "has body" (non-empty response).
export function run(op) {
    // The runner reads this to bind the server metrics export to the measurement phase.
    if (exec.scenario.name === 'measure' && exec.scenario.iterationInTest === 0) {
        console.log(`PERF_MEASURE_START ${Date.now()}`);
    }

    switch (TRANSPORT) {
        case 'rest': {
            const res = op.rest();
            return check(res, { ok: (r) => r.status === 200, 'has body': (r) => !!r.body && r.body.length > 0 });
        }
        case 'grpc': {
            const res = grpcClient.invoke(op.grpc.method, op.grpc.message, op.token);
            return check(res, { ok: (r) => r && r.status === grpcClient.StatusOK, 'has body': (r) => r && r.message !== null });
        }
        case 'grpcweb': {
            const r = grpcWeb.call(BASE_URL, op.grpcWeb.path, op.grpcWeb.bytes, op.token);
            return check(r, { ok: (x) => x.res.status === 200 && !x.truncated && x.grpcStatus === '0', 'has body': (x) => x.data > 0 });
        }
    }
}
