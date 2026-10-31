// S5 Write command: toggle the Pico controller tile (publishes to the device over MQTT).
import exec from 'k6/execution';
import { options as build } from './lib/config.js';
import { nextUser } from './lib/data.js';
import { run } from './lib/scenario.js';
import * as rest from './lib/rest.js';
import { stringField, messageField, boolValue } from './lib/protobuf.js';

export const options = build();

export default function () {
    const user = nextUser();
    const value = exec.scenario.iterationInTest % 2 === 0;
    run({
        token: user.accessToken,
        rest: () => rest.post(`api/v1/devices/${user.deviceId}/tile/action`, { value }, user.accessToken),
        grpc: { method: 'senswave.devices.v1.DevicesService/DeviceTileAction', message: { device_id: user.deviceId, value: value } },
        grpcWeb: { path: 'senswave.devices.v1.DevicesService/DeviceTileAction', bytes: [...stringField(1, user.deviceId), ...messageField(2, boolValue(value))] },
    });
}
