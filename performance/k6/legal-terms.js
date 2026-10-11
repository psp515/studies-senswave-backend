// Load test: fetch the current terms and conditions over one transport.
//
//   k6 run -e TRANSPORT=rest    -e BASE_URL=https://localhost:8001 performance/k6/legal-terms.js
//   k6 run -e TRANSPORT=grpc    -e BASE_URL=https://localhost:8002 performance/k6/legal-terms.js
//   k6 run -e TRANSPORT=grpcweb -e BASE_URL=https://localhost:8003 performance/k6/legal-terms.js
//
// Optional: VUS (default 10), DURATION (default 30s), RAMP (default 10s).
// The API uses a self-signed certificate, so TLS verification is skipped.

import http from 'k6/http';
import grpc from 'k6/net/grpc';
import { check, fail } from 'k6';

const TRANSPORT = (__ENV.TRANSPORT || 'rest').toLowerCase();
const BASE_URL = (__ENV.BASE_URL || 'https://localhost:8001').replace(/\/+$/, '');
const VUS = parseInt(__ENV.VUS || '10', 10);
const DURATION = __ENV.DURATION || '30s';
const RAMP = __ENV.RAMP || '10s';

const SERVICE = 'senswave.users.v1.LegalService';
const METHOD = 'GetTermsAndConditions';

if (!['rest', 'grpc', 'grpcweb'].includes(TRANSPORT)) {
    throw new Error(`Unknown TRANSPORT '${TRANSPORT}', expected rest | grpc | grpcweb`);
}

export const options = {
    insecureSkipTLSVerify: true,
    stages: [
        { duration: RAMP, target: VUS },
        { duration: DURATION, target: VUS },
        { duration: RAMP, target: 0 },
    ],
    thresholds: {
        checks: ['rate>0.99'],
    },
    tags: { transport: TRANSPORT },
};

// Native gRPC: the proto is loaded from the repository, in the init context.
const grpcClient = new grpc.Client();
if (TRANSPORT === 'grpc') {
    grpcClient.load(['../../src/Shared/Senswave.Contracts/Protos'], 'users/v1/legal.proto');
}

export default function () {
    switch (TRANSPORT) {
        case 'rest':
            return rest();
        case 'grpc':
            return nativeGrpc();
        case 'grpcweb':
            return grpcWeb();
    }
}

function rest() {
    const res = http.get(`${BASE_URL}/api/v1/legal/terms`);

    check(res, {
        'status is 200': (r) => r.status === 200,
        'has terms content': (r) => {
            const body = r.json();
            return body !== null && typeof body.content === 'string' && body.content.length > 0;
        },
    });
}

function nativeGrpc() {
    // One HTTP/2 connection per VU, reused across iterations.
    if (__ITER === 0) {
        grpcClient.connect(BASE_URL.replace(/^https?:\/\//, ''), { plaintext: false });
    }

    const res = grpcClient.invoke(`${SERVICE}/${METHOD}`, {});

    check(res, {
        'status is OK': (r) => r && r.status === grpc.StatusOK,
        'has terms content': (r) => r && r.message && typeof r.message.content === 'string' && r.message.content.length > 0,
    });
}

// gRPC-Web has no k6 client, so the call is framed by hand.
// Request: one data frame (flag 0x00, 4-byte big-endian length) with the empty GetTermsAndConditionsRequest.
const GRPC_WEB_EMPTY_REQUEST = new Uint8Array([0x00, 0x00, 0x00, 0x00, 0x00]).buffer;

function grpcWeb() {
    const res = http.post(`${BASE_URL}/${SERVICE}/${METHOD}`, GRPC_WEB_EMPTY_REQUEST, {
        headers: {
            'content-type': 'application/grpc-web+proto',
            'x-grpc-web': '1',
        },
        responseType: 'binary',
    });

    const frames = res.status === 200 ? parseGrpcWebFrames(res.body) : { data: 0, status: null };

    check(res, {
        'status is 200': (r) => r.status === 200,
        'grpc-status is 0': () => (frames.status ?? res.headers['Grpc-Status']) === '0',
        'has terms content': () => frames.data > 0,
    });
}

// Splits a gRPC-Web response body into frames. Returns the total data-frame payload size
// and the grpc-status value from the trailer frame (flag 0x80).
function parseGrpcWebFrames(body) {
    if (!body) {
        return { data: 0, status: null };
    }

    const bytes = new Uint8Array(body);
    let offset = 0;
    let data = 0;
    let status = null;

    while (offset + 5 <= bytes.length) {
        const flag = bytes[offset];
        const length = ((bytes[offset + 1] << 24) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 8) | bytes[offset + 4]) >>> 0;
        const start = offset + 5;
        const end = start + length;

        if (end > bytes.length) {
            fail(`Truncated gRPC-Web frame: needs ${end} bytes, got ${bytes.length}`);
        }

        if ((flag & 0x80) === 0x80) {
            const trailers = String.fromCharCode.apply(null, bytes.subarray(start, end));
            const match = /grpc-status:\s*(\d+)/i.exec(trailers);
            status = match ? match[1] : null;
        } else {
            data += length;
        }

        offset = end;
    }

    return { data, status };
}
