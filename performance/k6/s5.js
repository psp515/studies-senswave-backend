// S5 Write command: toggle the stream device tile (publishes to the device over MQTT).
import exec from 'k6/execution';
import { options as build } from './lib/config.js';
import { nextUser, userCount } from './lib/data.js';
import { run } from './lib/scenario.js';
import * as rest from './lib/rest.js';
import { stringField, messageField, boolValue } from './lib/protobuf.js';

export const options = build();

export default function () {
    const user = nextUser();
    // Alternate per user (round-robin over users), so each device sees changing values.
    const value = Math.floor(exec.scenario.iterationInTest / userCount) % 2 === 0;
    run({
        token: user.accessToken,
        rest: () => rest.post(`api/v1/devices/${user.streamDeviceId}/tile/action`, { value }, user.accessToken),
        grpc: { method: 'senswave.devices.v1.DevicesService/DeviceTileAction', message: { device_id: user.streamDeviceId, value: value } },
        grpcWeb: { path: 'senswave.devices.v1.DevicesService/DeviceTileAction', bytes: [...stringField(1, user.streamDeviceId), ...messageField(2, boolValue(value))] },
    });
}
