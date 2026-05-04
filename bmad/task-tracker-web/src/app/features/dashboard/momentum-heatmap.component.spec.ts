import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ProgressTrendPoint } from '../../shared/models/progress.models';
import { MomentumHeatmapComponent } from './momentum-heatmap.component';

describe('MomentumHeatmapComponent', () => {
  let fixture: ComponentFixture<MomentumHeatmapComponent>;
  let component: MomentumHeatmapComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MomentumHeatmapComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(MomentumHeatmapComponent);
    component = fixture.componentInstance;
    component.items = buildHeatmapItems([0, 1, 2, 3, 4]);
    fixture.detectChanges();
  });

  it('maps intensity levels deterministically from daily activity', () => {
    const max = component.maxCompletedCount();

    expect(component.intensityLevel(0, max)).toBe(0);
    expect(component.intensityLevel(1, max)).toBe(1);
    expect(component.intensityLevel(2, max)).toBe(2);
    expect(component.intensityLevel(3, max)).toBe(3);
    expect(component.intensityLevel(4, max)).toBe(4);
  });

  it('emits selected day when a heatmap cell is clicked', () => {
    const selectedDays: string[] = [];
    component.daySelected.subscribe((value) => selectedDays.push(value));

    const button = fixture.nativeElement.querySelector('button[data-index="2"]') as HTMLButtonElement;
    button.click();

    expect(selectedDays).toEqual(['2026-04-03T00:00:00Z']);
  });

  it('supports keyboard navigation between cells', () => {
    expect(component.nextFocusIndex('ArrowRight', 0, 10)).toBe(1);
    expect(component.nextFocusIndex('ArrowLeft', 0, 10)).toBe(0);
    expect(component.nextFocusIndex('ArrowDown', 0, 10)).toBe(7);
    expect(component.nextFocusIndex('ArrowUp', 7, 10)).toBe(0);
  });

  it('provides assistive aria labels for each day cell', () => {
    const firstButton = fixture.nativeElement.querySelector('button[data-index="0"]') as HTMLButtonElement;

    expect(firstButton.getAttribute('aria-label')).toContain('2026-04-01');
    expect(firstButton.getAttribute('aria-label')).toContain('completed task(s)');
  });
});

function buildHeatmapItems(completions: number[]): ProgressTrendPoint[] {
  return completions.map((count, index) => ({
    bucketStartUtc: `2026-04-${String(index + 1).padStart(2, '0')}T00:00:00Z`,
    bucketEndUtc: `2026-04-${String(index + 1).padStart(2, '0')}T23:59:59Z`,
    completedTaskCount: count,
    xpGranted: count * 10
  }));
}
