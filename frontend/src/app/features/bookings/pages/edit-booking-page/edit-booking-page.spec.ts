import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { routes } from '../../../../app.routes';
import { Room } from '../../../rooms/models/room';
import { Booking } from '../../models/booking';

const rooms: Room[] = [
  { id: 2, name: 'Cedar', capacity: 4 },
  { id: 3, name: 'Lighthouse', capacity: 8 },
  { id: 1, name: 'Orion', capacity: 12 },
];
const saved: Booking = {
  id: 7,
  roomId: 3,
  roomName: 'Lighthouse',
  guestName: 'Sai',
  meetingTitle: 'Sprint planning',
  date: '2026-12-01',
  startTime: '10:00',
  endTime: '11:00',
};

describe('EditBookingPage', () => {
  let httpTesting: HttpTestingController;
  let harness: RouterTestingHarness;
  let router: Router;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter(routes, withComponentInputBinding())],
    });
    httpTesting = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => httpTesting.verify());

  async function openEditPage(booking: Booking | 'not-found' = saved): Promise<HTMLElement> {
    await harness.navigateByUrl('/my-bookings/7/edit');
    const bookingRequest = httpTesting.expectOne('/api/bookings/7');
    httpTesting.expectOne('/api/rooms').flush(rooms);
    if (booking === 'not-found') {
      bookingRequest.flush({ title: 'Booking not found.' }, { status: 404, statusText: 'Not Found' });
    } else {
      bookingRequest.flush(booking);
    }
    await harness.fixture.whenStable();
    return harness.routeNativeElement as HTMLElement;
  }

  function field(page: HTMLElement, labelText: string): HTMLInputElement {
    const label = Array.from(page.querySelectorAll('label')).find((l) => l.textContent?.trim() === labelText);
    return page.querySelector<HTMLInputElement>(`#${label!.htmlFor}`)!;
  }

  async function enter(page: HTMLElement, labelText: string, value: string): Promise<void> {
    const input = field(page, labelText);
    input.value = value;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
    await harness.fixture.whenStable();
  }

  async function saveChanges(page: HTMLElement): Promise<void> {
    Array.from(page.querySelectorAll('button')).find((b) => b.textContent?.trim() === 'Save changes')!.click();
    await harness.fixture.whenStable();
  }

  it('opens directly (as after a refresh) with the saved details prefilled and the room fixed', async () => {
    const page = await openEditPage();

    expect(page.querySelector('h1')?.textContent).toBe('Edit booking');
    expect(page.querySelector('#selected-room-name')?.textContent).toBe('Lighthouse');
    expect(field(page, 'Your name').value).toBe('Sai');
    expect(field(page, 'Meeting title').value).toBe('Sprint planning');
    expect(field(page, 'Date').value).toBe('2026-12-01');
    expect(field(page, 'Start time').value).toBe('10:00');
    expect(field(page, 'End time').value).toBe('11:00');
    expect(page.querySelector('label[for="booking-room"], select')).toBeNull();

    const buttons = Array.from(page.querySelectorAll('button'), (b) => b.textContent?.trim());
    expect(buttons).toContain('Save changes');
    expect(buttons).not.toContain('Preview booking');
    const back = Array.from(page.querySelectorAll('a')).find((a) => a.textContent?.trim() === 'Back without saving');
    expect(back?.getAttribute('href')).toBe('/my-bookings');
  });

  it('saves a title-only edit with the original time slot and returns to My bookings announcing it', async () => {
    const page = await openEditPage();

    await enter(page, 'Meeting title', '  Team planning ');
    await saveChanges(page);

    const request = httpTesting.expectOne('/api/bookings/7');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      guestName: 'Sai',
      meetingTitle: 'Team planning',
      date: '2026-12-01',
      startTime: '10:00',
      endTime: '11:00',
    });
    expect(field(page, 'Meeting title').closest('fieldset')?.disabled).toBe(true);

    request.flush({ ...saved, meetingTitle: 'Team planning' });
    (await vi.waitFor(() => httpTesting.expectOne('/api/bookings/mine'))).flush([{ ...saved, meetingTitle: 'Team planning' }]);
    await harness.fixture.whenStable();

    expect(router.url).toBe('/my-bookings');
    const myBookings = harness.routeNativeElement as HTMLElement;
    expect(myBookings.querySelector('[aria-live="polite"]')?.textContent?.trim()).toBe('Booking updated');
    expect(myBookings.querySelector('tbody th')?.textContent?.trim()).toBe('Team planning');
  });

  it('keeps the edits and explains a conflicting time change without leaving the page', async () => {
    const page = await openEditPage();

    await enter(page, 'Start time', '13:30');
    await enter(page, 'End time', '14:30');
    await saveChanges(page);

    httpTesting
      .expectOne('/api/bookings/7')
      .flush({ title: 'That time is already booked.' }, { status: 409, statusText: 'Conflict' });
    await harness.fixture.whenStable();

    expect(router.url).toBe('/my-bookings/7/edit');
    expect(page.querySelector('[role="alert"]')?.textContent).toContain("Your booking hasn't changed");
    expect(field(page, 'Start time').value).toBe('13:30');
    expect(field(page, 'End time').value).toBe('14:30');
    expect(field(page, 'Start time').closest('fieldset')?.disabled).toBe(false);
  });

  it('does not send invalid changes: the shared validators still apply', async () => {
    const page = await openEditPage();

    await enter(page, 'Your name', '   ');
    await enter(page, 'End time', '09:00');
    await saveChanges(page);

    httpTesting.expectNone('/api/bookings/7');
    expect(page.textContent).toContain("Your name can't be only spaces.");
    expect(page.textContent).toContain('End time must be later than the start time.');
  });

  it("explains when the booking isn't this browser's (or no longer exists)", async () => {
    const page = await openEditPage('not-found');

    expect(page.querySelector('[role="status"]')?.textContent).toContain("couldn't find this booking in this browser");
    expect(page.querySelector('form')).toBeNull();
  });
});
