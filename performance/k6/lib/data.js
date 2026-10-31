import { SharedArray } from 'k6/data';
import exec from 'k6/execution';
import { DATA_FILE } from './config.js';

const users = new SharedArray('users', () => JSON.parse(open(DATA_FILE)).users);

// Round-robin over the seeded users, one user per iteration.
export function nextUser() {
    return users[exec.scenario.iterationInTest % users.length];
}
