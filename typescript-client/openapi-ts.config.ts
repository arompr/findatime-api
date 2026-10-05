import { defineConfig } from "@hey-api/openapi-ts";

export default defineConfig({
  input: "../src/FindAtime.Api/obj/FindAtime.Api.json",
  output: "src/generated",
  plugins: [
    "@hey-api/typescript",
    "@hey-api/sdk",
    {
      name: "@hey-api/client-fetch",
      baseUrl: false,
    },
  ],
});
