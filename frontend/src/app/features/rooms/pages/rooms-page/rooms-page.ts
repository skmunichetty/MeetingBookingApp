import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { RoomCapacityFilter } from '../../components/room-capacity-filter/room-capacity-filter';
import { RoomCard } from '../../components/room-card/room-card';
import { RoomsApi } from '../../data-access/rooms-api';
import { Room } from '../../models/room';

type LoadStatus = 'loading' | 'loaded' | 'error';

@Component({
  selector: 'app-rooms-page',
  imports: [RoomCard, RoomCapacityFilter],
  templateUrl: './rooms-page.html',
  styleUrl: './rooms-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomsPage {
  private readonly roomsApi = inject(RoomsApi);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly status = signal<LoadStatus>('loading');
  protected readonly rooms = signal<Room[]>([]);

  protected readonly minimumCapacity = signal<number | null>(null);

  protected readonly visibleRooms = computed(() => {
    const minimum = this.minimumCapacity();
    const rooms = this.rooms();
    return minimum === null ? rooms : rooms.filter((room) => room.capacity >= minimum);
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

  protected setMinimumCapacity(minimum: number | null): void {
    this.minimumCapacity.set(minimum);
  }

  protected clearFilter(): void {
    this.minimumCapacity.set(null);
  }
}
