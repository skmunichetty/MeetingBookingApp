import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { RoomsApi } from '../../../rooms/data-access/rooms-api';
import { Room } from '../../../rooms/models/room';
import { BookingForm } from '../../components/booking-form/booking-form';
import { BookingsApi } from '../../data-access/bookings-api';
import { Booking } from '../../models/booking';

type LoadStatus = 'loading' | 'loaded' | 'not-found' | 'error';

export const BOOKING_UPDATED_NOTICE = 'Booking updated';

@Component({
  selector: 'app-edit-booking-page',
  imports: [RouterLink, BookingForm],
  templateUrl: './edit-booking-page.html',
  styleUrl: '../booking-page/booking-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditBookingPage implements OnInit {
  private readonly bookingsApi = inject(BookingsApi);
  private readonly roomsApi = inject(RoomsApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly id = input.required<string>();

  protected readonly status = signal<LoadStatus>('loading');
  protected readonly booking = signal<Booking | null>(null);
  protected readonly room = signal<Room | null>(null);

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    const bookingId = Number(this.id());
    if (!Number.isInteger(bookingId) || bookingId <= 0) {
      this.status.set('not-found');
      return;
    }

    this.status.set('loading');
    forkJoin({ booking: this.bookingsApi.getBooking(bookingId), rooms: this.roomsApi.getRooms() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ booking, rooms }) => {
          const room = rooms.find((r) => r.id === booking.roomId);
          this.booking.set(booking);
          this.room.set(room ?? { id: booking.roomId, name: booking.roomName, capacity: 0 });
          this.status.set('loaded');
        },
        error: (error: unknown) =>
          this.status.set(error instanceof HttpErrorResponse && error.status === 404 ? 'not-found' : 'error'),
      });
  }

  protected onSaved(): void {
    void this.router.navigateByUrl('/my-bookings', { state: { notice: BOOKING_UPDATED_NOTICE } });
  }
}
