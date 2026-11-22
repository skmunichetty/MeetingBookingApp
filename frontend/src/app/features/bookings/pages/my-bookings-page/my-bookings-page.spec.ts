import { Location } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { routes } from '../../../../app.routes';
import { AppHeader } from '../../../../core/layout/app-header/app-header';
import { Booking } from '../../models/booking';

const lighthouse: Booking = {
  id: 7,
  roomId: 3,
  roomName: 'Lighthouse',
  guestName: 'Sai',
  meetingTitle: 'Sprint planning',
  date: '2026-12-01',
  startTime: '10:00',
  endTime: '11:00',
};
const cedarLater: Booking = {
  id: 9,
  roomId: 2,
  roomName: 'Cedar',
  guestName: 'Sai',
  meetingTitle: 'One-to-one',
  date: '2026-12-02',
  startTime: '14:00',
  endTime: '14:30',
};
const orionEarlier: Booking = {
  id: 4,
  roomId: 1,
  roomName: 'Orion',
  guestName: 'Alex',
  meetingTitle: 'Stand-up',
  date: '2026-12-01',
  startTime: '09:00',
  endTime: '09:15',
};

describe('MyBookingsPage', () => {
  let httpTesting: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter(routes, withComponentInputBinding())],
    });
    httpTesting = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => httpTesting.verify());

  async function openWith(bookings: Booking[]): Promise<HTMLElement> {
    await harness.navigateByUrl('/my-bookings');
    httpTesting.expectOne('/api/bookings/mine').flush(bookings);
    await harness.fixture.whenStable();
    return page();
  }

  function page(): HTMLElement {
    return harness.routeNativeElement as HTMLElement;
  }

  function rows(): HTMLTableRowElement[] {
    return Array.from(page().querySelectorAll<HTMLTableRowElement>('tbody tr:not(.my-bookings__confirm-row)'));
  }

  function cells(row: HTMLTableRowElement): string[] {
    return Array.from(row.querySelectorAll('td, th'), (cell) => cell.textContent?.trim() ?? '');
  }

  function byLabel(label: string): HTMLElement | null {
    return page().querySelector(`[aria-label="${label}"]`);
  }

  function buttonWithText(text: string): HTMLButtonElement | undefined {
    const scope = page().querySelector('.my-bookings__confirm') ?? page();
    return Array.from(scope.querySelectorAll('button')).find((b) => b.textContent?.trim() === text);
  }

  async function clickText(text: string): Promise<void> {
    buttonWithText(text)!.click();
    await harness.fixture.whenStable();
  }

  async function clickLabel(label: string): Promise<void> {
    byLabel(label)!.click();
    await harness.fixture.whenStable();
  }

  it('shows a table with the agreed columns, sorted earliest first, with times like "10:00 am – 11:00 am"', async () => {
    await openWith([cedarLater, lighthouse, orionEarlier]);

    const headers = Array.from(page().querySelectorAll('thead th'), (th) => th.textContent?.trim());
    expect(headers).toEqual(['Room', 'Meeting title', 'Booked by', 'Date', 'Time', 'Actions']);
    expect(page().textContent).toContain('Bookings made in this browser.');

    expect(rows().map((row) => cells(row).slice(0, 5))).toEqual([
      ['Orion', 'Stand-up', 'Alex', 'Tue, Dec 1, 2026', '9:00 am – 9:15 am'],
      ['Lighthouse', 'Sprint planning', 'Sai', 'Tue, Dec 1, 2026', '10:00 am – 11:00 am'],
      ['Cedar', 'One-to-one', 'Sai', 'Wed, Dec 2, 2026', '2:00 pm – 2:30 pm'],
    ]);
  });

  it('gives each row labelled Edit and Cancel icon actions with tooltips', async () => {
    await openWith([lighthouse]);

    const edit = byLabel('Edit Sprint planning in Lighthouse');
    const cancel = byLabel('Cancel Sprint planning in Lighthouse');
    expect(edit?.tagName).toBe('A');
    expect(edit?.getAttribute('href')).toBe('/my-bookings/7/edit');
    expect(edit?.querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');
    expect(edit?.querySelector('.my-bookings__tooltip')?.textContent).toBe('Edit booking');
    expect(cancel?.tagName).toBe('BUTTON');
    expect(cancel?.querySelector('.my-bookings__tooltip')?.textContent).toBe('Cancel booking');
  });

  function expectSingleBackLinkAboveHeading(): void {
    const roomLinks = Array.from(page().querySelectorAll('a[href="/rooms"]'));
    expect(roomLinks.map((a) => a.textContent?.trim())).toEqual(['← Back to meeting rooms']);
    const heading = page().querySelector('h1')!;
    expect(roomLinks[0].compareDocumentPosition(heading) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  }

  it('keeps the loading, empty and error states, with "Back to meeting rooms" shown in every one', async () => {
    await harness.navigateByUrl('/my-bookings');
    expect(page().querySelector('[role="status"]')?.textContent).toContain('Loading your bookings');
    expectSingleBackLinkAboveHeading();

    httpTesting.expectOne('/api/bookings/mine').error(new ProgressEvent('error'));
    await harness.fixture.whenStable();
    expect(page().querySelector('[role="alert"]')?.textContent).toContain("couldn't load your bookings");
    expectSingleBackLinkAboveHeading();

    await clickText('Retry');
    httpTesting.expectOne('/api/bookings/mine').flush([]);
    await harness.fixture.whenStable();

    expect(page().querySelector('table')).toBeNull();
    expect(page().textContent).toContain('You have no upcoming bookings in this browser.');
    expectSingleBackLinkAboveHeading();
  });

  it('shows "Back to meeting rooms" above a table with rows, and it opens the room list with its capacity filter', async () => {
    await openWith([lighthouse]);
    expect(rows()).toHaveLength(1);
    expectSingleBackLinkAboveHeading();

    page().querySelector<HTMLAnchorElement>('a.back-link')!.click();
    (await vi.waitFor(() => httpTesting.expectOne('/api/rooms'))).flush([
      { id: 2, name: 'Cedar', capacity: 4 },
      { id: 3, name: 'Lighthouse', capacity: 8 },
    ]);
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/rooms');
    expect(page().querySelector('h1')?.textContent).toBe('Meeting rooms');
    expect(page().querySelector('#room-capacity-filter')).not.toBeNull();
    expect(page().querySelectorAll('app-room-card')).toHaveLength(2);
  });

  it('confirms with the room, date and time, and "Keep booking" sends nothing', async () => {
    await openWith([lighthouse]);

    await clickLabel('Cancel Sprint planning in Lighthouse');

    const confirmation = page().querySelector('#cancel-confirmation-7');
    expect(confirmation?.textContent).toContain('Lighthouse');
    expect(confirmation?.textContent).toContain('Tuesday, December 1, 2026');
    expect(confirmation?.textContent).toContain('10:00 am – 11:00 am');
    expect(buttonWithText('Keep booking')).toBeDefined();
    expect(buttonWithText('Cancel booking')).toBeDefined();

    await clickText('Keep booking');

    httpTesting.expectNone((request) => request.method === 'DELETE');
    expect(page().querySelector('#cancel-confirmation-7')).toBeNull();
    expect(rows()).toHaveLength(1);
  });

  it('sends one DELETE, disables repeat clicks, and removes the row only after the API confirms', async () => {
    await openWith([lighthouse, cedarLater]);
    await clickLabel('Cancel Sprint planning in Lighthouse');

    await clickText('Cancel booking');
    buttonWithText('Cancel booking')!.click();
    await harness.fixture.whenStable();

    const request = httpTesting.expectOne('/api/bookings/7');
    expect(request.request.method).toBe('DELETE');
    expect(buttonWithText('Cancel booking')?.disabled).toBe(true);
    expect(buttonWithText('Keep booking')?.disabled).toBe(true);
    expect(rows()).toHaveLength(2);

    request.flush(null, { status: 204, statusText: 'No Content' });
    await harness.fixture.whenStable();

    expect(rows().map((row) => cells(row)[1])).toEqual(['One-to-one']);
    expect(page().textContent).toContain('Booking cancelled: Sprint planning in Lighthouse');
  });

  it('keeps the row with an actionable error when cancelling fails, and the retry works', async () => {
    await openWith([lighthouse]);
    await clickLabel('Cancel Sprint planning in Lighthouse');
    await clickText('Cancel booking');

    httpTesting.expectOne('/api/bookings/7').error(new ProgressEvent('error'));
    await harness.fixture.whenStable();

    expect(page().querySelector('#cancel-confirmation-7 [role="alert"]')?.textContent).toContain(
      'your booking is still active',
    );
    expect(rows()).toHaveLength(1);
    expect(buttonWithText('Cancel booking')?.disabled).toBe(false);

    await clickText('Cancel booking');
    httpTesting.expectOne('/api/bookings/7').flush(null, { status: 204, statusText: 'No Content' });
    await harness.fixture.whenStable();

    expect(page().querySelector('table')).toBeNull();
    expect(page().textContent).toContain('You have no upcoming bookings in this browser.');
  });

  it('announces "Booking updated" once, and a reload does not repeat it', async () => {
    const navigation = TestBed.inject(Router).navigateByUrl('/my-bookings', { state: { notice: 'Booking updated' } });
    (await vi.waitFor(() => httpTesting.expectOne('/api/bookings/mine'))).flush([lighthouse]);
    await navigation;
    await harness.fixture.whenStable();

    expect(page().querySelector('[aria-live="polite"]')?.textContent?.trim()).toBe('Booking updated');
    expect(TestBed.inject(Location).getState()).not.toHaveProperty('notice');
  });

  it('is reachable from the "My bookings" header link, which marks the current page', async () => {
    const header = TestBed.createComponent(AppHeader);
    await header.whenStable();
    const link = (header.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('nav a');
    expect(link?.textContent?.trim()).toBe('My bookings');

    link!.click();
    (await vi.waitFor(() => httpTesting.expectOne('/api/bookings/mine'))).flush([]);
    await harness.fixture.whenStable();
    await header.whenStable();

    expect(TestBed.inject(Router).url).toBe('/my-bookings');
    expect(link?.getAttribute('aria-current')).toBe('page');
  });
});
