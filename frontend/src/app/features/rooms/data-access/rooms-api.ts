import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Room } from '../models/room';

@Injectable({ providedIn: 'root' })
export class RoomsApi {
  private readonly http = inject(HttpClient);

  getRooms(): Observable<Room[]> {
    return this.http.get<Room[]>('/api/rooms');
  }
}
