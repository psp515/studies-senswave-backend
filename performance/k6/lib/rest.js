import http from 'k6/http';
import { BASE_URL } from './config.js';

export function get(path, token) {
    return http.get(`${BASE_URL}/${path}`, { headers: token ? { authorization: `Bearer ${token}` } : {} });
}

export function post(path, body, token) {
    return http.post(`${BASE_URL}/${path}`, JSON.stringify(body), {
        headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
    });
}
