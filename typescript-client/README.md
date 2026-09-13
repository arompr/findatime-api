# @arompr/findatime-typescript-client

TypeScript types and fetch client for the Findatime REST API. Auto-generated
from the BE's OpenAPI document and published to GitHub Packages. One named type
per API DTO.

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

Each BE DTO has a matching named export. See `src/generated/models/` in the
package source for the full list.

## Client usage

The package also ships a typed `fetch` client (`FindAtimeApiService`) and its
configuration object (`OpenAPI`). Configure the base URL once at app startup,
then call the static methods.

```ts
import { OpenAPI, FindAtimeApiService } from "@arompr/findatime-typescript-client";

OpenAPI.BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5263";

const created = await FindAtimeApiService.createEvent({
    name: "Team sync",
    guestId: "11111111-1111-1111-1111-111111111111",
    organizerName: "Alice",
});
// `created` is typed as CreateEventResponse
```

To set a bearer token or extra headers:

```ts
OpenAPI.TOKEN = "bearer-token";
OpenAPI.HEADERS = { "X-Guest-Id": guestId };
```

All service methods return a `CancelablePromise`, which is a thenable — `await`
and `.then()` work exactly like a native `Promise`.

If you prefer to bring your own HTTP layer (axios, TanStack Query, etc.),
import the types only:

```ts
import type { CreateEventResponse } from "@arompr/findatime-typescript-client";
```