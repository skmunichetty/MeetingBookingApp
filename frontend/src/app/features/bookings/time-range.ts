import { formatDate } from '@angular/common';
import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';

export function formatTimeRange(startTime: string, endTime: string, locale: string): string {
  return `${formatClockTime(startTime, locale)} – ${formatClockTime(endTime, locale)}`;
}

function formatClockTime(time: string, locale: string): string {
  const [hours, minutes] = time.split(':').map(Number);
  return formatDate(new Date(2000, 0, 1, hours, minutes), 'h:mm a', locale).toLowerCase();
}

@Pipe({ name: 'timeRange' })
export class TimeRangePipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);

  transform(booking: { startTime: string; endTime: string }): string {
    return formatTimeRange(booking.startTime, booking.endTime, this.locale);
  }
}
