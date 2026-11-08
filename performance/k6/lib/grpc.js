import grpc from 'k6/net/grpc';
import { BASE_URL, TRANSPORT } from './config.js';

const client = new grpc.Client();
if (TRANSPORT === 'grpc') {
    client.load(['../../src/Shared/Senswave.Contracts/Protos'],
        'users/v1/users.proto', 'users/v1/legal.proto', 'devices/v1/operations.proto', 'devices/v1/dashboards.proto', 'devices/v1/devices.proto');
}

let connected = false;

// One HTTP/2 connection per VU, reused across iterations (same as REST and gRPC-Web connection reuse in k6).
export function invoke(method, message, token) {
    if (!connected) {
        client.connect(BASE_URL.replace(/^https?:\/\//, ''), { plaintext: false });
        connected = true;
    }
    const metadata = token ? { authorization: `Bearer ${token}` } : {};
    return client.invoke(method, message, { metadata });
}

export const StatusOK = grpc.StatusOK;
