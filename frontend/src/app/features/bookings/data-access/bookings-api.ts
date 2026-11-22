import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, switchMap } from 'rxjs';

import { Booking, CreateBookingRequest, UpdateBookingRequest } from '../models/booking';

@Injectable({ providedIn: 'root' })
export class BookingsApi {
  private readonly http = inject(HttpClient);

  ensureGuestSession(): Observable<void> {
    return this.http.get<void>('/api/guest-session').pipe(map(() => undefined));
  }

  createBooking(request: CreateBookingRequest): Observable<Booking> {
    return this.ensureGuestSession().pipe(switchMap(() => this.http.post<Booking>('/api/bookings', request)));
  }

  getBooking(bookingId: number): Observable<Booking> {
    return this.http.get<Booking>(`/api/bookings/${bookingId}`);
  }

  updateBooking(bookingId: number, request: UpdateBookingRequest): Observable<Booking> {
    return this.http.put<Booking>(`/api/bookings/${bookingId}`, request);
  }

  getMyBookings(): Observable<Booking[]> {
    return this.http.get<Booking[]>('/api/bookings/mine');
  }

  cancelBooking(bookingId: number): Observable<void> {
    return this.http.delete<void>(`/api/bookings/${bookingId}`).pipe(map(() => undefined));
  }
}
