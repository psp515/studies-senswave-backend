// Minimal protobuf encoder for request messages: string fields, int32 fields and google.protobuf.Value(bool).
function varint(value) {
    const out = [];
    let v = value >>> 0;
    while (v > 0x7f) { out.push((v & 0x7f) | 0x80); v >>>= 7; }
    out.push(v);
    return out;
}

function utf8(text) {
    const out = [];
    for (const ch of unescape(encodeURIComponent(text))) out.push(ch.charCodeAt(0));
    return out;
}

export function stringField(number, text) {
    const bytes = utf8(text);
    return [...varint((number << 3) | 2), ...varint(bytes.length), ...bytes];
}

export function int32Field(number, value) {
    return [...varint((number << 3) | 0), ...varint(value)];
}

export function messageField(number, bytes) {
    return [...varint((number << 3) | 2), ...varint(bytes.length), ...bytes];
}

// google.protobuf.Value { bool bool_value = 4; }
export function boolValue(value) {
    return [...varint((4 << 3) | 0), value ? 1 : 0];
}
