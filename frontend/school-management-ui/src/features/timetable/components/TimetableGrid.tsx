import { Coffee } from "lucide-react";
import type { ReactNode } from "react";
import { dayName, type TimetablePeriodResponse } from "../types/timetable.types";

interface Props {
  /** Read by screen readers as the table's name. */
  caption: string;
  /** Day numbers to draw as columns, in order (the API sends them Monday first). */
  days: number[];
  periods: TimetablePeriodResponse[];
  /** The content of one lesson cell (a Teaching period on one day). */
  renderCell: (day: number, period: TimetablePeriodResponse) => ReactNode;
}

/**
 * The weekly grid, shared by a class's timetable and a teacher's. Break periods are one wide row;
 * a Teaching period has a cell per day. What a cell shows (and whether it can be clicked) is
 * up to the page, through renderCell.
 */
export function TimetableGrid({ caption, days, periods, renderCell }: Props) {
  const ordered = [...periods].sort((a, b) => a.sortOrder - b.sortOrder);
  return (
    <div className="tt-wrap">
      <table className="tt-table">
        <caption className="visually-hidden">{caption}</caption>
        <thead>
          <tr>
            <th scope="col" className="tt-time">
              Time
            </th>
            {days.map((d) => (
              <th key={d} scope="col">
                {dayName(d)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {ordered.map((p) =>
            p.kind === "Break" ? (
              <tr key={p.id} className="tt-break">
                <td className="tt-time">
                  {p.startTime}–{p.endTime}
                </td>
                <td colSpan={days.length}>
                  <Coffee size={14} aria-hidden="true" /> {p.label}
                </td>
              </tr>
            ) : (
              <tr key={p.id}>
                <th scope="row" className="tt-time">
                  <strong>{p.label}</strong>
                  <br />
                  {p.startTime}–{p.endTime}
                </th>
                {days.map((d) => (
                  <td key={d} className="tt-cell">
                    {renderCell(d, p)}
                  </td>
                ))}
              </tr>
            ),
          )}
        </tbody>
      </table>
    </div>
  );
}
