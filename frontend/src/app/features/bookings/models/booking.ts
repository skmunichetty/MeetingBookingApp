export interface UpdateBookingRequest {
  guestName: string;
  meetingTitle: string;
  date: string;
  startTime: string;
  endTime: string;
}

export interface CreateBookingRequest extends UpdateBookingRequest {
  roomId: number;
}

export interface Booking {
  id: number;
  roomId: number;
  roomName: string;
  guestName: string;
  meetingTitle: string;
  date: string;
  startTime: string;
  endTime: string;
}
