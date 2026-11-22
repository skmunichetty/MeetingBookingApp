import { DatePipe, Location, formatDate } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  LOCALE_ID,
  afterNextRender,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';

import { BookingsApi } from '../../data-access/bookings-api';
import { Booking } from '../../models/booking';
import { TimeRangePipe, formatTimeRange } from '../../time-range';

type LoadStatus = 'loading' | 'loaded' | 'error';

function byDateAndStartTime(a: Booking, b: Booking): number {
  return a.date.localeCompare(b.date) || a.startTime.localeCompare(b.startTime) || a.id - b.id;
}

@Component({
  selector: 'app-my-bookings-page',
  imports: [DatePipe, RouterLink, TimeRangePipe],
  templateUrl: './my-bookings-page.html',
  styleUrl: './my-bookings-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyBookingsPage {
  private readonly bookingsApi = inject(BookingsApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly locale = inject(LOCALE_ID);

  protected readonly status = signal<LoadStatus>('loading');
  private readonly bookings = signal<Booking[]>([]);
  protected readonly sortedBookings = computed(() => [...this.bookings()].sort(byDateAndStartTime));

  protected readonly confirmingId = signal<number | null>(null);
  protected readonly cancellingIds = signal<ReadonlySet<number>>(new Set());
  protected readonly cancelErrors = signal<ReadonlyMap<number, string>>(new Map());
  protected readonly notice = signal<string | null>(null);

  constructor() {
    const notice = inject(Router).currentNavigation()?.extras.state?.['notice'];
    if (typeof notice === 'string') {
      const location = inject(Location);
      afterNextRender(() => {
        this.notice.set(notice);
        const { notice: _shown, ...state } = (location.getState() ?? {}) as Record<string, unknown>;
        location.replaceState(location.path(), '', state);
      });
    }

    this.loadBookings();
  }

  protected loadBookings(): void {
    this.status.set('loading');
    this.confirmingId.set(null);
    this.cancelErrors.set(new Map());

    this.bookingsApi
      .getMyBookings()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (bookings) => {
          this.bookings.set(bookings);
          this.status.set('loaded');
        },
        error: () => this.status.set('error'),
      });
  }

  protected timeRange(booking: Booking): string {
    return formatTimeRange(booking.startTime, booking.endTime, this.locale);
  }

  protected longDate(booking: Booking): string {
    return formatDate(booking.date, 'fullDate', this.locale);
  }

  protected isCancelling(bookingId: number): boolean {
    return this.cancellingIds().has(bookingId);
  }

  protected askToCancel(booking: Booking): void {
    this.confirmingId.set(booking.id);
    this.notice.set(null);
    this.setError(booking.id, null);
    this.focusAfterRender(`#keep-booking-${booking.id}`);
  }

  protected keepBooking(booking: Booking): void {
    this.confirmingId.set(null);
    this.setError(booking.id, null);
    this.focusAfterRender(`#cancel-booking-${booking.id}`);
  }

  protected confirmCancel(booking: Booking): void {
    if (this.isCancelling(booking.id)) {
      return;
    }

    this.updateCancelling(booking.id, true);
    this.setError(booking.id, null);

    this.bookingsApi
      .cancelBooking(booking.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.updateCancelling(booking.id, false);
          this.confirmingId.set(null);
          this.bookings.update((bookings) => bookings.filter((b) => b.id !== booking.id));
          this.notice.set(
            `Booking cancelled: ${booking.meetingTitle} in ${booking.roomName}, ${this.longDate(booking)}, ${this.timeRange(booking)}.`,
          );
          this.focusAfterRender('#my-bookings-heading');
        },
        error: (error: unknown) => {
          this.updateCancelling(booking.id, false);
          this.setError(booking.id, describeCancelError(error));
        },
      });
  }

  private updateCancelling(bookingId: number, cancelling: boolean): void {
    this.cancellingIds.update((ids) => {
      const next = new Set(ids);
      if (cancelling) {
        next.add(bookingId);
      } else {
        next.delete(bookingId);
      }
      return next;
    });
  }

  private setError(bookingId: number, message: string | null): void {
    this.cancelErrors.update((errors) => {
      const next = new Map(errors);
      if (message) {
        next.set(bookingId, message);
      } else {
        next.delete(bookingId);
      }
      return next;
    });
  }

  private focusAfterRender(selector: string): void {
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus(), {
      injector: this.injector,
    });
  }
}

function describeCancelError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    switch (error.status) {
      case 0:
        return "We couldn't reach the server, so your booking is still active. Check your connection and try again.";
      case 400:
        return 'Your session needs refreshing. Refresh the list, then try again.';
      case 404:
        return "This booking couldn't be found. It may already have been cancelled. Refresh the list to check.";
    }
  }
  return "Cancelling didn't work, so your booking is still active. Please try again.";
}
