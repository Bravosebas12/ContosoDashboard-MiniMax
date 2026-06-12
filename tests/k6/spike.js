import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
  scenarios: {
    spike_profile: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 5 },
        { duration: '30s', target: 250 },
        { duration: '2m', target: 250 },
        { duration: '30s', target: 5 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '30s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.1'],
    http_req_duration: ['p(95)<4000'],
  },
};

export default function () {
  const res = http.get(`${BASE_URL}/documents`);
  check(res, {
    'status is 200|302': (r) => r.status === 200 || r.status === 302,
  });
  sleep(0.2);
}
