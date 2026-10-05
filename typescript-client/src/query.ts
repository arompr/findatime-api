import { client } from "./generated/client.gen";
import type { Options } from "./generated";
import type { RequestResult } from "./generated/client";
import type {
  AvailabilityQueryRequest,
  GetEventAvailabilitiesResponse,
  GetParticipantAvailabilityResponse,
} from "./generated";

// The HTTP `QUERY` method (RFC 10008) is a first-class path-item verb in
// OpenAPI 3.2, but hey-api does not emit operations for it yet (see
// hey-api/hey-api#2660). These hand-written functions call `client.request`
// with the `QUERY` method directly, mirroring the generated SDK functions.
const QUERY = "QUERY" as never;

export type GetEventAvailabilitiesData = {
  body: AvailabilityQueryRequest;
  path: {
    publicId: string;
  };
  query?: never;
  url: "/events/{publicId}/availabilities";
};

export type GetEventAvailabilitiesResponses = {
  200: GetEventAvailabilitiesResponse;
};

export type GetEventAvailabilitiesErrors = {
  400: unknown;
  404: unknown;
};

export type GetParticipantAvailabilityData = {
  body: AvailabilityQueryRequest;
  path: {
    publicId: string;
    participantId: string;
  };
  query?: never;
  url: "/events/{publicId}/participants/{participantId}/availability";
};

export type GetParticipantAvailabilityResponses = {
  200: GetParticipantAvailabilityResponse;
};

export type GetParticipantAvailabilityErrors = {
  400: unknown;
  404: unknown;
};

export const getEventAvailabilities = <ThrowOnError extends boolean = false>(
  options: Options<GetEventAvailabilitiesData, ThrowOnError>,
): RequestResult<
  GetEventAvailabilitiesResponses,
  GetEventAvailabilitiesErrors,
  ThrowOnError
> =>
  (options.client ?? client).request<
    GetEventAvailabilitiesResponses,
    GetEventAvailabilitiesErrors,
    ThrowOnError
  >({
    url: "/events/{publicId}/availabilities",
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options.headers,
    },
    method: QUERY,
  });

export const getParticipantAvailability = <
  ThrowOnError extends boolean = false,
>(
  options: Options<GetParticipantAvailabilityData, ThrowOnError>,
): RequestResult<
  GetParticipantAvailabilityResponses,
  GetParticipantAvailabilityErrors,
  ThrowOnError
> =>
  (options.client ?? client).request<
    GetParticipantAvailabilityResponses,
    GetParticipantAvailabilityErrors,
    ThrowOnError
  >({
    url: "/events/{publicId}/participants/{participantId}/availability",
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options.headers,
    },
    method: QUERY,
  });
