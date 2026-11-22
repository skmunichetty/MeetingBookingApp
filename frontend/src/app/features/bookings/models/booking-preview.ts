import { CreateBookingRequest } from './booking';

export interface BookingPreview extends CreateBookingRequest {
  roomName: string;
}
