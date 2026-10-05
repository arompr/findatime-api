import {
  createEvent,
  getEvent,
  getMyParticipant,
  health,
  joinEvent,
  leaveEvent,
  searchEvents,
  setAvailability,
  updateEventParams,
} from "./generated/sdk.gen";
import { getEventAvailabilities, getParticipantAvailability } from "./query";

export { client } from "./generated/client.gen";

export const FindAtimeApi = {
  createEvent,
  searchEvents,
  getEvent,
  updateEventParams,
  joinEvent,
  leaveEvent,
  getMyParticipant,
  setAvailability,
  getEventAvailabilities,
  getParticipantAvailability,
  health,
};
