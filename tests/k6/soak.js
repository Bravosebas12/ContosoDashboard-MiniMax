import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
  scenarios: {
    soak_profile: {
      executor: 'constant-vus',
      vus: 30,
      duration: __ENV.SOAK_DURATION || '24h',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<2000'],
  },
};

export default function () {
  const list = http.get(`${BASE_URL}/documents`);
  const home = http.get(`${BASE_URL}/`);

  check(list, {
    'documents status is 200|302': (r) => r.status === 200 || r.status === 302,
  });
  check(home, {
    'home status is 200|302': (r) => r.status === 200 || r.status === 302,
  });

  sleep(1);
}
