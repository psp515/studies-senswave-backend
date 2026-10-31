import http from 'k6/http';
import { fail } from 'k6';

// One data frame: flag 0x00 + 4-byte big-endian length + message.
export function frame(messageBytes) {
    const n = messageBytes.length;
    return new Uint8Array([0x00, (n >>> 24) & 0xff, (n >>> 16) & 0xff, (n >>> 8) & 0xff, n & 0xff, ...messageBytes]).buffer;
}

export function call(baseUrl, path, messageBytes, token) {
    const headers = { 'content-type': 'application/grpc-web+proto', 'x-grpc-web': '1' };
    if (token) headers.authorization = `Bearer ${token}`;
    const res = http.post(`${baseUrl}/${path}`, frame(messageBytes), { headers, responseType: 'binary' });
    const frames = res.status === 200 ? parseFrames(res.body) : { data: 0, status: null };
    return { res, data: frames.data, grpcStatus: frames.status ?? res.headers['Grpc-Status'] ?? null };
}

// Returns the total data-frame payload size and grpc-status from the trailer frame (flag 0x80).
export function parseFrames(body) {
    if (!body) return { data: 0, status: null };
    const bytes = new Uint8Array(body);
    let offset = 0, data = 0, status = null;
    while (offset + 5 <= bytes.length) {
        const flag = bytes[offset];
        const length = ((bytes[offset + 1] << 24) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 8) | bytes[offset + 4]) >>> 0;
        const start = offset + 5, end = start + length;
        if (end > bytes.length) fail(`Truncated gRPC-Web frame: needs ${end} bytes, got ${bytes.length}`);
        if ((flag & 0x80) === 0x80) {
            const match = /grpc-status:\s*(\d+)/i.exec(String.fromCharCode.apply(null, bytes.subarray(start, end)));
            status = match ? match[1] : null;
        } else {
            data += length;
        }
        offset = end;
    }
    return { data, status };
}
