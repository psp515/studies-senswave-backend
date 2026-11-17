// S3 Nested list: a dashboard's display layout with dynamic values.
import { options as build } from './lib/config.js';
import { nextUser } from './lib/data.js';
import { run } from './lib/scenario.js';
import * as rest from './lib/rest.js';
import { stringField } from './lib/protobuf.js';

export const options = build();

export default function () {
    const user = nextUser();
    run({
        token: user.accessToken,
        rest: () => rest.get(`api/v1/devices/dashboards/${user.dashboardId}/display`, user.accessToken),
        grpc: { method: 'senswave.devices.v1.DashboardsService/DisplayDashboard', message: { dashboard_id: user.dashboardId } },
        grpcWeb: { path: 'senswave.devices.v1.DashboardsService/DisplayDashboard', bytes: stringField(1, user.dashboardId) },
    });
}
