import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8000';
const EMAIL = __ENV.EMAIL || 'YOUR_USERNAME';
const PASSWORD = __ENV.PASSWORD || 'YOUR_PASSWORD';

// Test routes: [source, destination]
const ROUTES = [
  [{ lat: 40.7128, lon: -74.0060 }, { lat: 40.7580, lon: -73.9855 }], // Downtown -> Times Square
  [{ lat: 40.7484, lon: -73.9856 }, { lat: 40.7614, lon: -73.9776 }], // Empire State -> Central Park
  [{ lat: 40.7061, lon: -74.0088 }, { lat: 40.7282, lon: -73.9942 }], // Wall St -> SoHo
  [{ lat: 40.7527, lon: -73.9772 }, { lat: 40.7061, lon: -74.0088 }], // Grand Central -> Wall St
  [{ lat: 40.7589, lon: -73.9851 }, { lat: 40.7484, lon: -73.9856 }], // Times Square -> Empire State
  [{ lat: 40.7282, lon: -73.9942 }, { lat: 40.7128, lon: -74.0060 }], // SoHo -> Downtown
  [{ lat: 40.7614, lon: -73.9776 }, { lat: 40.7527, lon: -73.9772 }], // Central Park -> Grand Central
  [{ lat: 40.7489, lon: -73.9680 }, { lat: 40.7282, lon: -73.9942 }], // Midtown East -> SoHo
  [{ lat: 40.7178, lon: -74.0431 }, { lat: 40.7128, lon: -74.0060 }], // Jersey City -> Downtown
  [{ lat: 40.7831, lon: -73.9712 }, { lat: 40.7489, lon: -73.9680 }], // Upper West Side -> Midtown East
  [{ lat: 40.7061, lon: -74.0088 }, { lat: 40.7614, lon: -73.9776 }], // Wall St -> Central Park
  [{ lat: 40.7580, lon: -73.9855 }, { lat: 40.7178, lon: -74.0431 }], // Times Square -> Jersey City
  [{ lat: 40.7282, lon: -73.9942 }, { lat: 40.7831, lon: -73.9712 }], // SoHo -> Upper West Side
  [{ lat: 40.7527, lon: -73.9772 }, { lat: 40.7580, lon: -73.9855 }], // Grand Central -> Times Square
  [{ lat: 40.7831, lon: -73.9712 }, { lat: 40.7061, lon: -74.0088 }], // Upper West Side -> Wall St
  [{ lat: 40.7489, lon: -73.9680 }, { lat: 40.7614, lon: -73.9776 }], // Midtown East -> Central Park
  [{ lat: 40.7128, lon: -74.0060 }, { lat: 40.7831, lon: -73.9712 }], // Downtown -> Upper West Side
  [{ lat: 40.7614, lon: -73.9776 }, { lat: 40.7061, lon: -74.0088 }], // Central Park -> Wall St
  [{ lat: 40.7484, lon: -73.9856 }, { lat: 40.7527, lon: -73.9772 }], // Empire State -> Grand Central
  [{ lat: 40.7178, lon: -74.0431 }, { lat: 40.7580, lon: -73.9855 }], // Jersey City -> Times Square
];

export const options = {
  stages: [
    { duration: '30s', target: 10 },  // ramp up to 10 users
    { duration: '1m',  target: 10 },  // hold at 10 users
    { duration: '30s', target: 50 },  // ramp up to 50 users
    { duration: '1m',  target: 50 },  // hold at 50 users
    { duration: '30s', target: 0  },  // ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<5000'], // 95% of requests under 5s
    http_req_failed:   ['rate<0.01'],  // less than 1% failure rate
  },
};

export function setup() {
  const res = http.post(
    `${BASE_URL}/api/login`,
    JSON.stringify({ email: EMAIL, password: PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } }
  );

  check(res, { 'login successful': (r) => r.status === 200 });

  const token = res.json('accessToken');
  if (!token) throw new Error('Failed to get access token');
  return { token };
}

export default function ({ token }) {
  const route = ROUTES[Math.floor(Math.random() * ROUTES.length)];

  const res = http.post(
    `${BASE_URL}/api/route`,
    JSON.stringify({ source: route[0], destination: route[1] }),
    { headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
    }}
  );

  if (res.status !== 200) {
    console.log(`Failed: ${res.status} — ${res.body}`);
  }

  check(res, {
    'status 200': (r) => r.status === 200,
    'has geometry': (r) => r.json('geometry') !== null,
  });

  sleep(1);
}
