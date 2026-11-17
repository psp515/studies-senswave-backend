// S2 List of objects: a device's operations, page size 30.
import { options as build } from './lib/config.js';
import { nextUser } from './lib/data.js';
import { run } from './lib/scenario.js';
import * as rest from './lib/rest.js';
import { stringField, int32Field } from './lib/protobuf.js';

export const options = build();

export default function () {
    const user = nextUser();
    run({
        token: user.accessToken,
        rest: () => rest.get(`api/v1/devices/operations/display?deviceId=${user.deviceId}&page=1&size=30`, user.accessToken),
        grpc: { method: 'senswave.devices.v1.OperationsService/DisplayOperations', message: { device_id: user.deviceId, page: 1, size: 30 } },
        grpcWeb: { path: 'senswave.devices.v1.OperationsService/DisplayOperations', bytes: [...stringField(1, user.deviceId), ...int32Field(2, 1), ...int32Field(3, 30)] },
    });
}
