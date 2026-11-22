import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';

import { RoomsApi } from '../../../rooms/data-access/rooms-api';
import { Room } from '../../../rooms/models/room';
import { BookingForm } from '../../components/booking-form/booking-form';

type LoadStatus = 'loading' | 'loaded' | 'error';

@Component({
  selector: 'app-booking-page',
  imports: [RouterLink, BookingForm],
  templateUrl: './booking-page.html',
  styleUrl: './booking-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingPage {
  private readonly roomsApi = inject(RoomsApi);
  private readonly destroyRef = inject(DestroyRef);

  readonly id = input.required<string>();

  protected readonly status = signal<LoadStatus>('loading');
  private readonly rooms = signal<Room[]>([]);

  protected readonly room = computed(() => {
    const id = Number(this.id());
    return this.rooms().find((room) => room.id === id);
  });

  constructor() {
    this.loadRooms();
  }

  protected loadRooms(): void {
    this.status.set('loading');

    this.roomsApi
      .getRooms()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (rooms) => {
          this.rooms.set(rooms);
          this.status.set('loaded');
        },
        error: () => this.status.set('error'),
      });
  }
}
