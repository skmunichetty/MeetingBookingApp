import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'rooms' },
  {
    path: 'rooms',
    title: 'Meeting rooms',
    loadComponent: () =>
      import('./features/rooms/pages/rooms-page/rooms-page').then((m) => m.RoomsPage),
  },
  {
    path: 'rooms/:id/book',
    title: 'Book a room',
    loadComponent: () =>
      import('./features/bookings/pages/booking-page/booking-page').then((m) => m.BookingPage),
  },
  {
    path: 'my-bookings',
    title: 'My bookings',
    loadComponent: () =>
      import('./features/bookings/pages/my-bookings-page/my-bookings-page').then((m) => m.MyBookingsPage),
  },
  {
    path: 'my-bookings/:id/edit',
    title: 'Edit booking',
    loadComponent: () =>
      import('./features/bookings/pages/edit-booking-page/edit-booking-page').then((m) => m.EditBookingPage),
  },
];
