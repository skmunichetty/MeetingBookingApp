import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  OnInit,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { Room } from '../../../rooms/models/room';
import { BookingsApi } from '../../data-access/bookings-api';
import { Booking } from '../../models/booking';
import { BookingPreview } from '../../models/booking-preview';
import { endTimeAfterStartTimeValidator, notBlankValidator } from '../../validators/booking-validators';

interface BookingFormControls {
  guestName: FormControl<string>;
  title: FormControl<string>;
  date: FormControl<string>;
  startTime: FormControl<string>;
  endTime: FormControl<string>;
}

type SaveProblem =
  | { kind: 'conflict' }
  | { kind: 'not-found' }
  | { kind: 'rejected'; messages: string[] }
  | { kind: 'unreachable' };

const GUEST_NAME_MAX_LENGTH = 100;
const MEETING_TITLE_MAX_LENGTH = 200;

@Component({
  selector: 'app-booking-form',
  imports: [ReactiveFormsModule, DatePipe, RouterLink],
  templateUrl: './booking-form.html',
  styleUrl: './booking-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingForm implements OnInit {
  readonly room = input.required<Room>();
  readonly booking = input<Booking | null>(null);
  readonly saved = output<Booking>();

  protected readonly isEditing = computed(() => this.booking() !== null);

  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly bookingsApi = inject(BookingsApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);

  private readonly startTimeInput = viewChild<ElementRef<HTMLInputElement>>('startTimeInput');
  private readonly confirmationHeading = viewChild<ElementRef<HTMLElement>>('confirmationHeading');

  protected readonly guestNameMaxLength = GUEST_NAME_MAX_LENGTH;
  protected readonly meetingTitleMaxLength = MEETING_TITLE_MAX_LENGTH;

  protected readonly form: FormGroup<BookingFormControls> = this.formBuilder.group(
    {
      guestName: ['', [Validators.required, notBlankValidator, Validators.maxLength(GUEST_NAME_MAX_LENGTH)]],
      title: ['', [Validators.required, notBlankValidator, Validators.maxLength(MEETING_TITLE_MAX_LENGTH)]],
      date: ['', Validators.required],
      startTime: ['', Validators.required],
      endTime: ['', Validators.required],
    },
    { validators: endTimeAfterStartTimeValidator },
  );

  protected readonly submitted = signal(false);
  protected readonly preview = signal<BookingPreview | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveProblem = signal<SaveProblem | null>(null);
  protected readonly rejectionMessages = computed(() => {
    const problem = this.saveProblem();
    return problem?.kind === 'rejected' ? problem.messages : [];
  });
  protected readonly confirmed = signal<Booking | null>(null);

  constructor() {
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.preview.set(null);
      this.saveProblem.set(null);
    });
  }

  ngOnInit(): void {
    const booking = this.booking();
    if (booking) {
      this.form.reset({
        guestName: booking.guestName,
        title: booking.meetingTitle,
        date: booking.date,
        startTime: booking.startTime,
        endTime: booking.endTime,
      });
    }
  }

  protected submit(): void {
    if (this.isEditing()) {
      this.saveChanges();
    } else {
      this.previewBooking();
    }
  }

  protected showError(field: keyof BookingFormControls): boolean {
    const control = this.form.controls[field];
    return control.invalid && (control.touched || this.submitted());
  }

  protected showTimeRangeError(): boolean {
    return (
      this.form.hasError('endTimeNotAfterStart') &&
      (this.form.controls.endTime.touched || this.submitted())
    );
  }

  protected previewBooking(): void {
    this.submitted.set(true);
    this.saveProblem.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.preview.set(null);
      return;
    }

    const { guestName, title, date, startTime, endTime } = this.form.getRawValue();
    const room = this.room();
    this.preview.set({
      roomId: room.id,
      roomName: room.name,
      guestName: guestName.trim(),
      meetingTitle: title.trim(),
      date,
      startTime,
      endTime,
    });
  }

  protected confirmBooking(): void {
    const preview = this.preview();
    if (!preview || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.saveProblem.set(null);

    const { roomName: _roomName, ...request } = preview;
    this.bookingsApi
      .createBooking(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (booking) => {
          this.saving.set(false);
          this.preview.set(null);
          this.confirmed.set(booking);
          this.focusAfterRender(() => this.confirmationHeading());
        },
        error: (error: unknown) => {
          this.saving.set(false);
          const problem = toSaveProblem(error);
          this.saveProblem.set(problem);
          if (problem.kind === 'conflict') {
            this.preview.set(null);
            this.focusAfterRender(() => this.startTimeInput());
          }
        },
      });
  }

  protected saveChanges(): void {
    const booking = this.booking();
    this.submitted.set(true);
    this.saveProblem.set(null);

    if (!booking || this.saving()) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const { guestName, title, date, startTime, endTime } = this.form.getRawValue();
    this.bookingsApi
      .updateBooking(booking.id, { guestName: guestName.trim(), meetingTitle: title.trim(), date, startTime, endTime })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.saving.set(false);
          this.saved.emit(updated);
        },
        error: (error: unknown) => {
          this.saving.set(false);
          const problem = toSaveProblem(error);
          this.saveProblem.set(problem);
          if (problem.kind === 'conflict') {
            this.focusAfterRender(() => this.startTimeInput());
          }
        },
      });
  }

  protected bookAnotherTime(): void {
    this.confirmed.set(null);
    this.submitted.set(false);
    this.form.reset();
  }

  private focusAfterRender(target: () => ElementRef<HTMLElement> | undefined): void {
    afterNextRender(() => target()?.nativeElement.focus(), { injector: this.injector });
  }
}

function toSaveProblem(error: unknown): SaveProblem {
  if (error instanceof HttpErrorResponse) {
    switch (error.status) {
      case 409:
        return { kind: 'conflict' };
      case 404:
        return { kind: 'not-found' };
      case 400: {
        const errors = (error.error as { errors?: Record<string, string[]> } | null)?.errors ?? {};
        return { kind: 'rejected', messages: Object.values(errors).flat() };
      }
    }
  }
  return { kind: 'unreachable' };
}
