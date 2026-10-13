import type { ReactNode } from "react";

export interface ReviewItem {
  label: ReactNode;
  value: ReactNode;
}

/** U2: the "check before you confirm" summary in a pop-up (enrolment review). Empty values are left out. */
export function ReviewList({
  title,
  items,
}: {
  title: string;
  items: ReviewItem[];
}) {
  const shown = items.filter(
    (i) => i.value !== null && i.value !== undefined && i.value !== "",
  );
  return (
    <>
      <h3 className="review-heading">{title}</h3>
      <dl className="review-list">
        {shown.map((item, i) => (
          <div key={i} style={{ display: "contents" }}>
            <dt>{item.label}</dt>
            <dd>{item.value}</dd>
          </div>
        ))}
      </dl>
    </>
  );
}
