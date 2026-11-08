import {
  createEvent,
  deleteEvent,
  getEvent,
  getEventAvailabilities,
  getMyParticipant,
  getParticipantAvailability,
  health,
  joinEvent,
  leaveEvent,
  searchEvents,
  setAvailability,
  updateEventParams,
} from "./generated/sdk.gen";

export { client } from "./generated/client.gen";

export const FindAtimeApi = {
  createEvent,
  searchEvents,
  getEvent,
  updateEventParams,
  deleteEvent,
  joinEvent,
  leaveEvent,
  getMyParticipant,
  setAvailability,
  getEventAvailabilities,
  getParticipantAvailability,
  health,
};

type Assert<T extends true> = T;

type AllGeneratedOperationsExposed = Assert<
  Exclude<
    keyof typeof import("./generated/sdk.gen"),
    keyof typeof FindAtimeApi
  > extends never
    ? true
    : false
>;
