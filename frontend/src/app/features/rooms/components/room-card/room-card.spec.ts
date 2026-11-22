import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { RoomCard } from './room-card';

describe('RoomCard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
  });

  it('renders the room name and capacity', async () => {
    const fixture = TestBed.createComponent(RoomCard);
    fixture.componentRef.setInput('room', { id: 7, name: 'Cedar', capacity: 4 });
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('h2')?.textContent).toBe('Cedar');
    expect(element.querySelector('.room-card__capacity')?.textContent).toContain('Capacity: 4 people');
  });

  it('links to the booking page for that room', async () => {
    const fixture = TestBed.createComponent(RoomCard);
    fixture.componentRef.setInput('room', { id: 7, name: 'Cedar', capacity: 4 });
    await fixture.whenStable();

    const link = (fixture.nativeElement as HTMLElement).querySelector('a');
    expect(link?.textContent?.trim()).toBe('Book room');
    expect(link?.getAttribute('href')).toBe('/rooms/7/book');
    expect(link?.getAttribute('aria-label')).toBe('Book room Cedar');
  });
});
