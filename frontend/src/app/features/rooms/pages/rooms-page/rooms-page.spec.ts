import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Room } from '../../models/room';
import { RoomsPage } from './rooms-page';

describe('RoomsPage', () => {
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('shows an error and recovers when Retry is clicked', async () => {
    const fixture = TestBed.createComponent(RoomsPage);
    const element = fixture.nativeElement as HTMLElement;

    httpTesting
      .expectOne('/api/rooms')
      .flush('Server unavailable', { status: 503, statusText: 'Service Unavailable' });
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain("couldn't load");
    const retryButton = element.querySelector<HTMLButtonElement>('button');
    expect(retryButton?.textContent).toContain('Retry');

    retryButton!.click();
    const rooms: Room[] = [
      { id: 2, name: 'Cedar', capacity: 4 },
      { id: 3, name: 'Lighthouse', capacity: 8 },
    ];
    httpTesting.expectOne('/api/rooms').flush(rooms);
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')).toBeNull();
    const names = Array.from(element.querySelectorAll('app-room-card h2'), (h) => h.textContent);
    expect(names).toEqual(['Cedar', 'Lighthouse']);
  });
});
