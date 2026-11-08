// S4 String: the current terms and conditions (Markdown, anonymous).
import { options as build } from './lib/config.js';
import { run } from './lib/scenario.js';
import * as rest from './lib/rest.js';

export const options = build();

export default function () {
    run({
        token: null,
        rest: () => rest.get('api/v1/legal/terms'),
        grpc: { method: 'senswave.users.v1.LegalService/GetTermsAndConditions', message: {} },
        grpcWeb: { path: 'senswave.users.v1.LegalService/GetTermsAndConditions', bytes: [] },
    });
}
