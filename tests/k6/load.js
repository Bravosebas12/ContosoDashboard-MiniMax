import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
  scenarios: {
    nominal_load: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '2m', target: 20 },
        { duration: '6m', target: 50 },
        { duration: '2m', target: 0 },
      ],
      gracefulRampDown: '30s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<2000'],
  },
};

export default function () {
  const list = http.get(`${BASE_URL}/documents`);
  const dashboard = http.get(`${BASE_URL}/`);

  check(list, {
    'documents status is 200|302': (r) => r.status === 200 || r.status === 302,
  });
  check(dashboard, {
    'dashboard status is 200|302': (r) => r.status === 200 || r.status === 302,
  });

  sleep(1);
}
