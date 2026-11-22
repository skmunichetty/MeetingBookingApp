import { ChangeDetectionStrategy, Component, input, output } from "@angular/core";

@Component ({
    selector: 'app-room-capacity-filter',
    templateUrl: './room-capacity-filter.html',
    styleUrl: './room-capacity-filter.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})

export class RoomCapacityFilter {
    readonly minimumCapacity = input<number | null>(null);

    readonly minimumCapacityChange = output<number | null>();

    protected readonly thresholds = [4, 8, 12, 20] as const;

    protected selectMinimum(value: string): void {
        this.minimumCapacityChange.emit(value === '' ? null : Number(value));
    }
}
