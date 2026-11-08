# @arompr/findatime-typescript-client

TypeScript types and fetch client for the Findatime REST API. Auto-generated
from the BE's OpenAPI document with [Hey API](https://heyapi.dev/) and published
to GitHub Packages. One named type per API DTO, plus a typed client.

## Publishing

Publishing is done manually via the `publish-typescript-client` GitHub Actions workflow.
Pick the source ref and the version to publish. No version is ever auto-bumped.

## Install

The package is public, but GitHub Packages still requires a token with
`read:packages` scope to install:

```
echo "@arompr:registry=https://npm.pkg.github.com" >> .npmrc
echo "//npm.pkg.github.com/:_authToken=${GITHUB_TOKEN}" >> .npmrc
npm install @arompr/findatime-typescript-client
```

## Usage

```ts
import type { CreateEventResponse } from "@arompr/findatime-typescript-client";
```

Each BE DTO has a matching named export. See `src/generated/types.gen.ts` in the
package source for the full list.

## Client usage

The package also ships a typed `fetch` client. Configure the base URL (and any
default headers) once at app startup, then call the `FindAtimeApi` methods:

```ts
import { client, FindAtimeApi } from "@arompr/findatime-typescript-client";

client.setConfig({
    baseUrl: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5263",
    headers: { "X-Guest-Id": guestId },
});

const { data, error } = await FindAtimeApi.createEvent({
    body: {
        name: "Team sync",
        guestId: "11111111-1111-1111-1111-111111111111",
        organizerName: "Alice",
        isPasscodeProtected: false,
    },
});
// `data` is typed as CreateEventResponse when `error` is undefined
```

To send a bearer token or extra headers per request, pass them on the call or
set them on the client:

```ts
client.setConfig({ headers: { Authorization: `Bearer ${token}` } });
```

Every method returns a native `Promise` resolving to `{ data, error, response }`
(the `error` field carries the typed error union on a non-2xx response). Set
`client.setConfig({ responseStyle: "data" })` to resolve directly to `data`
instead, or `client.setConfig({ throwOnError: true })` to throw on errors.

If you prefer to bring your own HTTP layer (axios, TanStack Query, etc.), import
the types only:

```ts
import type { CreateEventResponse } from "@arompr/findatime-typescript-client";
```

## Availability ranges

Availability ranges are ISO 8601 instants (`{ start, end }`, typed as `string`).
Send them with an explicit offset (e.g. `toISOString()`); the API answers in UTC
(`+00:00`) and has no timezone field. Every operation, including
`getEventAvailabilities` and `getParticipantAvailability` (plain `GET`s since
0.1.0), comes from the generated SDK.
