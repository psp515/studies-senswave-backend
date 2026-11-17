// S1 Flat data: the signed-in user's profile.
import { options as build } from './lib/config.js';
import { nextUser } from './lib/data.js';
import { run } from './lib/scenario.js';
import * as rest from './lib/rest.js';

export const options = build();

export default function () {
    const user = nextUser();
    run({
        token: user.accessToken,
        rest: () => rest.get('api/v1/users', user.accessToken),
        grpc: { method: 'senswave.users.v1.UsersService/GetUser', message: {} },
        grpcWeb: { path: 'senswave.users.v1.UsersService/GetUser', bytes: [] },
    });
}
