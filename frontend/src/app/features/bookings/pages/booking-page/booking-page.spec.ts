import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { routes } from '../../../../app.routes';
import { Room } from '../../../rooms/models/room';

const rooms: Room[] = [
  { id: 2, name: 'Cedar', capacity: 4 },
  { id: 3, name: 'Lighthouse', capacity: 8 },
  { id: 1, name: 'Orion', capacity: 12 },
];

describe('BookingPage', () => {
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(routes, withComponentInputBinding()),
      ],
    });
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  async function openUrl(url: string): Promise<{ harness: RouterTestingHarness; element: HTMLElement }> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    return { harness, element: harness.routeNativeElement as HTMLElement };
  }

  it('shows the room from a directly opened booking URL', async () => {
    const { harness, element } = await openUrl('/rooms/3/book');

    httpTesting.expectOne('/api/rooms').flush(rooms);
    await harness.fixture.whenStable();

    expect(element.querySelector('h2#selected-room-name')?.textContent).toBe('Lighthouse');
    expect(element.textContent).toContain('Capacity: 8 people');
    expect(element.querySelector('a.booking-page__back')?.getAttribute('href')).toBe('/rooms');
    expect(element.querySelector('form')).not.toBeNull();
  });

  it('explains when no room has the ID in the URL', async () => {
    const { harness, element } = await openUrl('/rooms/99/book');

    httpTesting.expectOne('/api/rooms').flush(rooms);
    await harness.fixture.whenStable();

    expect(element.querySelector('[role="status"]')?.textContent).toContain(
      'We couldn\'t find a room with ID “99”',
    );
    expect(element.querySelector('form')).toBeNull();
  });

  it('shows a load error with Retry, then the room after retrying', async () => {
    const { harness, element } = await openUrl('/rooms/1/book');

    httpTesting
      .expectOne('/api/rooms')
      .flush('Server unavailable', { status: 503, statusText: 'Service Unavailable' });
    await harness.fixture.whenStable();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain("couldn't load the room details");

    element.querySelector<HTMLButtonElement>('.booking-page__button')!.click();
    httpTesting.expectOne('/api/rooms').flush(rooms);
    await harness.fixture.whenStable();

    expect(element.querySelector('[role="alert"]')).toBeNull();
    expect(element.querySelector('h2#selected-room-name')?.textContent).toBe('Orion');
  });
});
