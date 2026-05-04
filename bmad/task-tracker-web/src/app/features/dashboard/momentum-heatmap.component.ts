import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ProgressTrendPoint } from '../../shared/models/progress.models';

@Component({
  selector: 'app-momentum-heatmap',
  standalone: true,
  template: `
    <section class="heatmap-panel" role="region" aria-labelledby="heatmap-heading">
      <h3 id="heatmap-heading">Monthly activity heatmap</h3>
      <p class="heatmap-caption">Keyboard: use arrow keys to move between days, then press Enter.</p>

      @if (items.length === 0) {
        <p class="heatmap-empty">No daily activity available.</p>
      } @else {
        <div class="heatmap-grid" role="grid" aria-label="Task activity heatmap by day">
          @for (item of items; track item.bucketStartUtc; let index = $index) {
            <button
              type="button"
              class="heatmap-cell"
              role="gridcell"
              [attr.data-intensity]="intensityLevel(item.completedTaskCount, maxCompletedCount())"
              [attr.data-index]="index"
              [attr.aria-label]="cellAriaLabel(item)"
              (click)="selectDay(item.bucketStartUtc)"
              (keydown)="onCellKeydown($event, index)"
            >
              <span class="heatmap-cell-date">{{ dateLabel(item.bucketStartUtc) }}</span>
              <span class="heatmap-cell-count">{{ item.completedTaskCount }}</span>
            </button>
          }
        </div>
      }
    </section>
  `,
  styles: [
    `
      .heatmap-panel {
        border-radius: 0.75rem;
        border: 1px solid #355d79;
        background: #0f1d31;
        padding: 0.8rem;
      }

      h3 {
        margin: 0;
        font-size: 0.95rem;
      }

      .heatmap-caption {
        margin: 0.35rem 0 0;
        color: #c2d8e7;
        font-size: 0.8rem;
      }

      .heatmap-empty {
        margin: 0.7rem 0 0;
        color: #c2d8e7;
      }

      .heatmap-grid {
        margin-top: 0.7rem;
        display: grid;
        grid-template-columns: repeat(7, minmax(0, 1fr));
        gap: 0.45rem;
      }

      .heatmap-cell {
        border: 1px solid #355d79;
        border-radius: 0.55rem;
        background: #15273d;
        color: #ecf6ff;
        min-height: 3.1rem;
        padding: 0.3rem;
        display: grid;
        align-content: space-between;
        text-align: left;
      }

      .heatmap-cell[data-intensity='0'] {
        background: #1a2b40;
      }

      .heatmap-cell[data-intensity='1'] {
        background: #1f3a49;
      }

      .heatmap-cell[data-intensity='2'] {
        background: #21514f;
      }

      .heatmap-cell[data-intensity='3'] {
        background: #2f6a4b;
      }

      .heatmap-cell[data-intensity='4'] {
        background: #3a8345;
      }

      .heatmap-cell:hover,
      .heatmap-cell:focus-visible {
        border-color: #9fe9b8;
        outline: none;
      }

      .heatmap-cell-date {
        font-size: 0.72rem;
        color: #c4d8e8;
      }

      .heatmap-cell-count {
        font-weight: 700;
      }

      @media (max-width: 768px) {
        .heatmap-grid {
          grid-template-columns: repeat(5, minmax(0, 1fr));
        }
      }
    `
  ]
})
export class MomentumHeatmapComponent {
  @Input() items: ProgressTrendPoint[] = [];
  @Output() daySelected = new EventEmitter<string>();

  dateLabel(utcIsoDateTime: string): string {
    return utcIsoDateTime.slice(8, 10);
  }

  maxCompletedCount(): number {
    return this.items.reduce((max, item) => Math.max(max, item.completedTaskCount), 0);
  }

  intensityLevel(completedTaskCount: number, maxCount: number): number {
    if (completedTaskCount <= 0 || maxCount <= 0) {
      return 0;
    }

    const ratio = completedTaskCount / maxCount;
    if (ratio <= 0.25) {
      return 1;
    }

    if (ratio <= 0.5) {
      return 2;
    }

    if (ratio <= 0.75) {
      return 3;
    }

    return 4;
  }

  cellAriaLabel(item: ProgressTrendPoint): string {
    const date = item.bucketStartUtc.slice(0, 10);
    return `${date}: ${item.completedTaskCount} completed task(s), ${item.xpGranted} XP earned. Open day detail.`;
  }

  selectDay(utcIsoDateTime: string): void {
    this.daySelected.emit(utcIsoDateTime);
  }

  onCellKeydown(event: KeyboardEvent, index: number): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.selectDay(this.items[index].bucketStartUtc);
      return;
    }

    const nextIndex = this.nextFocusIndex(event.key, index, this.items.length);
    if (nextIndex === index) {
      return;
    }

    event.preventDefault();
    const target = event.currentTarget as HTMLElement | null;
    const grid = target?.closest('.heatmap-grid');
    const nextCell = grid?.querySelector<HTMLElement>(`button[data-index="${nextIndex}"]`);
    nextCell?.focus();
  }

  nextFocusIndex(key: string, index: number, total: number): number {
    const columns = 7;

    if (key === 'ArrowRight') {
      return Math.min(total - 1, index + 1);
    }

    if (key === 'ArrowLeft') {
      return Math.max(0, index - 1);
    }

    if (key === 'ArrowDown') {
      return Math.min(total - 1, index + columns);
    }

    if (key === 'ArrowUp') {
      return Math.max(0, index - columns);
    }

    return index;
  }
}
