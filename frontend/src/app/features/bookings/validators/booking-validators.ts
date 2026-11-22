import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export const notBlankValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = typeof control.value === 'string' ? control.value : '';
  return value.length > 0 && value.trim().length === 0 ? { blank: true } : null;
};

export const endTimeAfterStartTimeValidator: ValidatorFn = (
  group: AbstractControl,
): ValidationErrors | null => {
  const startTime = group.get('startTime')?.value as string | undefined;
  const endTime = group.get('endTime')?.value as string | undefined;

  if (!startTime || !endTime) {
    return null;
  }

  return endTime > startTime ? null : { endTimeNotAfterStart: true };
};
