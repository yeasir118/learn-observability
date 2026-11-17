// TinyShop's load generator (TS-017): a steady, weighted mix of what browsers do, sent through nginx.
// Settings come from the environment: BASE_URL, RATE (iterations per second), DURATION.
import http from 'k6/http';
import { check } from 'k6';

// --- Settings ----------------------------------------------------------------------------------
// Everything in __ENV is text, so RATE is turned into a number.
const BASE_URL = __ENV.BASE_URL || 'http://host.docker.internal:8000';
const RATE = Number(__ENV.RATE || 10);
const DURATION = __ENV.DURATION || '30s';

// --- Route names -------------------------------------------------------------------------------
// One fixed name per route, with {id} where the number changes. Without them every order id
// would become its own row in the summary and the report (TS-017, decision 4).
const ROUTES = {
    products: 'GET /api/products',
    user: 'GET /api/users/{id}',
    placeOrder: 'POST /api/orders',          // valid and invalid orders: the same route
    order: 'GET /api/orders/{id}',
    userOrders: 'GET /api/orders?userId={id}',
};

// A threshold with no rules ([]) judges nothing; it only makes k6 print that route's own row
// in the end-of-test summary (TS-017, AC3).
function perRouteRows() {
    const rows = {};
    for (const route of Object.values(ROUTES)) {
        rows[`http_req_duration{name:${route}}`] = [];
        rows[`http_reqs{name:${route}}`] = [];
    }
    return rows;
}

export const options = {
    scenarios: {
        shoppers: {
            executor: 'constant-arrival-rate',
            rate: RATE,
            timeUnit: '1s',
            duration: DURATION,
            // One VU per in-flight iteration. Start with about one second's worth; allow five
            // seconds' worth, for when a fault makes requests slow (a 3 s payment delay).
            preAllocatedVUs: Math.max(RATE, 1),
            maxVUs: Math.max(RATE * 5, 10),
        },
    },
    thresholds: {
        // The run fails on any unexpected response or failed check (TS-017, decision 3).
        http_req_failed: ['rate==0'],
        checks: ['rate==1'],
        ...perRouteRows(),
    },
};

// --- Test data ---------------------------------------------------------------------------------
// The seed data (no setup or cleanup needed).
const USER_IDS = { min: 123, max: 127 };
const PRODUCT_IDS = { min: 1, max: 5 };
const UNKNOWN_ID = 999999;   // no user or product has this id

const JSON_HEADERS = { 'Content-Type': 'application/json' };

// A whole number from min to max, both included.
function randomInt(min, max) {
    return min + Math.floor(Math.random() * (max - min + 1));
}

function randomUserId() {
    return randomInt(USER_IDS.min, USER_IDS.max);
}

function randomProductId() {
    return randomInt(PRODUCT_IDS.min, PRODUCT_IDS.max);
}

// The request params that file a request under its route's name.
function named(route) {
    return { tags: { name: route } };
}

// --- Journeys ----------------------------------------------------------------------------------
function browseProducts() {
    const res = http.get(`${BASE_URL}/api/products`, named(ROUTES.products));

    check(res, {
        'products: status is 200': (r) => r.status === 200,
        'products: list is not empty': (r) => r.json('items.#') > 0,
    });
}

function lookUpUser() {
    const userId = randomUserId();
    const res = http.get(`${BASE_URL}/api/users/${userId}`, named(ROUTES.user));

    check(res, {
        'user: status is 200': (r) => r.status === 200,
        'user: it is the one we asked for': (r) => r.json('id') === userId,
    });
}

function placeOrder() {
    const body = JSON.stringify({
        userId: randomUserId(),
        productId: randomProductId(),
        quantity: randomInt(1, 5),
    });
    const created = http.post(`${BASE_URL}/api/orders`, body, {
        headers: JSON_HEADERS,
        ...named(ROUTES.placeOrder),
    });

    // k6 gives the headers as a plain object, with each name in its canonical form ("Location").
    const location = created.headers['Location'];

    const ok = check(created, {
        'order: status is 201': (r) => r.status === 201,
        'order: has a Location': () => typeof location === 'string' && location.length > 0,
    });

    // Nothing to read back if the order wasn't created.
    if (!ok) {
        return;
    }

    // Location is a path that already starts with "/" (/api/orders/1001).
    const readBack = http.get(`${BASE_URL}${location}`, named(ROUTES.order));

    check(readBack, {
        'order read-back: status is 200': (r) => r.status === 200,
        'order read-back: id is a number': (r) => Number.isInteger(r.json('id')),
    });
}

function listUserOrders() {
    const res = http.get(`${BASE_URL}/api/orders?userId=${randomUserId()}`, named(ROUTES.userOrders));

    check(res, {
        'user orders: status is 200': (r) => r.status === 200,
        // A user with no orders yet gets an empty list, which is fine.
        'user orders: has a list': (r) => Array.isArray(r.json('items')),
    });
}

// An order the API must refuse: an unknown user, or an unknown product (contracts.md §5.3).
function placeInvalidOrder() {
    const unknownUser = Math.random() < 0.5;
    const body = JSON.stringify({
        userId: unknownUser ? UNKNOWN_ID : randomUserId(),
        productId: unknownUser ? randomProductId() : UNKNOWN_ID,
        quantity: 1,
    });

    const res = http.post(`${BASE_URL}/api/orders`, body, {
        headers: JSON_HEADERS,
        ...named(ROUTES.placeOrder),
        // Here, and only here, a 422 is the right answer: don't count it in http_req_failed.
        responseCallback: http.expectedStatuses(422),
    });

    check(res, {
        'invalid order: status is 422': (r) => r.status === 422,
        'invalid order: says what was wrong': (r) =>
            r.json('code') === (unknownUser ? 'user_not_found' : 'product_not_found'),
    });
}

// --- The mix -----------------------------------------------------------------------------------
// How often each journey runs, out of 100 iterations (TS-017, decision 2).
const JOURNEYS = [
    { weight: 45, run: browseProducts },
    { weight: 15, run: lookUpUser },
    { weight: 20, run: placeOrder },
    { weight: 10, run: listUserOrders },
    { weight: 10, run: placeInvalidOrder },
];

const TOTAL_WEIGHT = JOURNEYS.reduce((sum, journey) => sum + journey.weight, 0);

// Rolls a number from 0 up to the total, then walks the list: each journey owns a slice as wide
// as its weight (0-45 browse, 45-60 user, 60-80 order, ...).
function pickJourney() {
    let roll = Math.random() * TOTAL_WEIGHT;
    for (const journey of JOURNEYS) {
        if (roll < journey.weight) {
            return journey;
        }
        roll -= journey.weight;
    }
    return JOURNEYS[JOURNEYS.length - 1];   // only reached through rounding
}

export default function () {
    pickJourney().run();
}
