import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Booking } from '../../models/booking';
import { BookingForm } from './booking-form';

const savedBooking: Booking = {
  id: 42,
  roomId: 1,
  roomName: 'Orion',
  guestName: 'Sai',
  meetingTitle: 'Sprint planning',
  date: '2026-12-01',
  startTime: '10:00',
  endTime: '11:00',
};

describe('BookingForm', () => {
  let fixture: ComponentFixture<BookingForm>;
  let element: HTMLElement;
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpTesting = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(BookingForm);
    fixture.componentRef.setInput('room', { id: 1, name: 'Orion', capacity: 12 });
    await fixture.whenStable();
    element = fixture.nativeElement as HTMLElement;
  });

  afterEach(() => httpTesting.verify());

  function field(labelText: string): HTMLInputElement {
    const label = Array.from(element.querySelectorAll('label')).find(
      (l) => l.textContent?.trim() === labelText,
    );
    const input = label && element.querySelector<HTMLInputElement>(`#${label.htmlFor}`);
    if (!input) {
      throw new Error(`No input is labelled "${labelText}"`);
    }
    return input;
  }

  async function enter(labelText: string, value: string): Promise<void> {
    const input = field(labelText);
    input.value = value;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
    await fixture.whenStable();
  }

  async function fillValidBooking(times: { start: string; end: string } = { start: '10:00', end: '11:00' }) {
    await enter('Your name', '  Sai ');
    await enter('Meeting title', '  Sprint planning  ');
    await enter('Date', '2026-12-01');
    await enter('Start time', times.start);
    await enter('End time', times.end);
  }

  function button(text: string): HTMLButtonElement | undefined {
    return Array.from(element.querySelectorAll('button')).find((b) => b.textContent?.trim() === text);
  }

  async function click(text: string): Promise<void> {
    button(text)!.click();
    await fixture.whenStable();
  }

  function errorFor(labelText: string): string | undefined {
    const id = field(labelText).getAttribute('aria-describedby');
    return id ? element.querySelector(`#${id}`)?.textContent?.trim() : undefined;
  }

  function section(headingId: string): HTMLElement | null {
    return element.querySelector(`section[aria-labelledby="${headingId}"]`);
  }

  async function answerGuestSession(): Promise<void> {
    httpTesting.expectOne('/api/guest-session').flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
  }

  function detailValues(container: HTMLElement | null): (string | undefined)[] {
    return Array.from(container?.querySelectorAll('dd') ?? [], (dd) => dd.textContent?.trim());
  }

  it('requires a name and rejects one that is only spaces', async () => {
    await click('Preview booking');
    expect(errorFor('Your name')).toBe('Enter your name.');

    await enter('Your name', '   ');
    expect(errorFor('Your name')).toBe("Your name can't be only spaces.");
    expect(field('Your name').getAttribute('aria-invalid')).toBe('true');
  });

  it('rejects a title that contains only spaces', async () => {
    await enter('Meeting title', '   ');

    expect(errorFor('Meeting title')).toBe("The meeting title can't be only spaces.");
  });

  it('flags equal and reversed times, and clears the error when the end time is corrected', async () => {
    await enter('Start time', '10:00');

    await enter('End time', '10:00');
    expect(errorFor('End time')).toBe('End time must be later than the start time.');

    await enter('End time', '09:30');
    expect(errorFor('End time')).toBe('End time must be later than the start time.');

    await enter('End time', '11:00');
    expect(errorFor('End time')).toBeUndefined();
  });

  it('previews trimmed details including the guest name, and clears the preview on edit', async () => {
    await fillValidBooking();
    await click('Preview booking');

    const preview = section('booking-preview-heading');
    expect(detailValues(preview)).toEqual([
      'Sai',
      'Orion',
      'Sprint planning',
      'Tuesday, December 1, 2026',
      '10:00–11:00',
    ]);
    expect(preview?.textContent).toContain('Not reserved yet');
    expect(button('Confirm booking')).toBeDefined();
    httpTesting.expectNone('/api/bookings');

    await enter('Meeting title', 'Retro');
    expect(section('booking-preview-heading')).toBeNull();
  });

  it('sends one POST, disables Confirm while saving, and shows the saved booking only after the API confirms', async () => {
    await fillValidBooking();
    await click('Preview booking');

    await click('Confirm booking');
    button('Confirm booking')?.click();
    await fixture.whenStable();

    httpTesting.expectNone('/api/bookings');
    await answerGuestSession();
    const request = httpTesting.expectOne('/api/bookings');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      roomId: 1,
      guestName: 'Sai',
      meetingTitle: 'Sprint planning',
      date: '2026-12-01',
      startTime: '10:00',
      endTime: '11:00',
    });
    expect(button('Confirm booking')?.disabled).toBe(true);
    expect(field('Your name').closest('fieldset')?.disabled).toBe(true);
    expect(element.textContent).toContain('Saving your booking…');
    expect(section('booking-confirmed-heading')).toBeNull();

    request.flush(savedBooking, { status: 201, statusText: 'Created' });
    await fixture.whenStable();

    const confirmation = section('booking-confirmed-heading');
    expect(confirmation?.querySelector('h2')?.textContent?.trim()).toBe('Booking confirmed');
    expect(confirmation?.textContent).toContain('Booking reference #42');
    expect(detailValues(confirmation)).toEqual([
      'Sai',
      'Orion',
      'Sprint planning',
      'Tuesday, December 1, 2026',
      '10:00–11:00',
    ]);
    expect(element.querySelector('form')).toBeNull();
    expect(confirmation?.querySelector('a')?.textContent?.trim()).toBe('View my bookings');
    expect(confirmation?.querySelector('a')?.getAttribute('href')).toBe('/my-bookings');
  });

  it('keeps every value and asks for another time when the slot is taken', async () => {
    await fillValidBooking({ start: '10:30', end: '11:30' });
    await click('Preview booking');
    await click('Confirm booking');
    await answerGuestSession();

    httpTesting
      .expectOne('/api/bookings')
      .flush(
        { title: 'That time is already booked.' },
        { status: 409, statusText: 'Conflict' },
      );
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'That time is already booked for Orion',
    );
    expect(section('booking-preview-heading')).toBeNull();
    expect(section('booking-confirmed-heading')).toBeNull();
    expect(field('Your name').value).toBe('  Sai ');
    expect(field('Meeting title').value).toBe('  Sprint planning  ');
    expect(field('Date').value).toBe('2026-12-01');
    expect(field('Start time').value).toBe('10:30');
    expect(field('End time').value).toBe('11:30');
    expect(field('Start time').closest('fieldset')?.disabled).toBe(false);

    await enter('Start time', '11:30');
    await enter('End time', '12:30');
    expect(element.querySelector('[role="alert"]')).toBeNull();
  });

  it('keeps the preview and lets the visitor retry when the server is unreachable', async () => {
    await fillValidBooking();
    await click('Preview booking');
    await click('Confirm booking');
    await answerGuestSession();

    httpTesting.expectOne('/api/bookings').error(new ProgressEvent('error'));
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('nothing was booked');
    expect(section('booking-preview-heading')).not.toBeNull();
    expect(button('Confirm booking')?.disabled).toBe(false);
  });
});
