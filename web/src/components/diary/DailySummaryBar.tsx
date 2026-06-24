import type { DailySummary, EntryNutrition } from '../../api/diary';

/** Props for {@link DailySummaryBar}. */
interface DailySummaryBarProps {
  totals: EntryNutrition;
  goals?: DailySummary['goals'];
}

/** One nutrient cell: its consumed value, label/unit, and optional goal. */
interface NutrientCell {
  label: string;
  unit: string;
  consumed: number;
  goal?: number;
}

/**
 * Running daily nutrition summary: calories plus the three macros, each shown
 * against its goal when one is available. Consumed values that exceed the goal
 * are visually flagged.
 */
export default function DailySummaryBar({ totals, goals }: DailySummaryBarProps) {
  const cells: NutrientCell[] = [
    { label: 'Calories', unit: 'kcal', consumed: totals.calories ?? 0, goal: goals?.calories },
    {
      label: 'Protein',
      unit: 'g',
      consumed: totals.proteinGrams ?? 0,
      goal: goals?.proteinGrams,
    },
    {
      label: 'Carbs',
      unit: 'g',
      consumed: totals.carbohydrateGrams ?? 0,
      goal: goals?.carbohydrateGrams,
    },
    { label: 'Fat', unit: 'g', consumed: totals.fatGrams ?? 0, goal: goals?.fatGrams },
  ];

  return (
    <div className="card diary-summary-bar">
      {cells.map((cell) => {
        const consumed = Math.round(cell.consumed);
        const over = cell.goal !== undefined && cell.consumed > cell.goal;
        return (
          <div className="diary-summary-nutrient" key={cell.label}>
            <span
              className={`diary-summary-value${over ? ' diary-summary-value-over' : ''}`}
            >
              {consumed}
            </span>
            {cell.goal !== undefined && (
              <span className="diary-summary-goal">/ {Math.round(cell.goal)}</span>
            )}
            <span className="diary-summary-label">
              {cell.label} {cell.unit}
            </span>
          </div>
        );
      })}
    </div>
  );
}
