import type { LucideIcon } from "lucide-react";
import type { ButtonHTMLAttributes } from "react";

interface Props extends Omit<
  ButtonHTMLAttributes<HTMLButtonElement>,
  "children"
> {
  icon: LucideIcon;
  /** Required: an icon alone says nothing to a screen reader. */
  label: string;
  tone?: "neutral" | "info" | "accent" | "bad" | "ok";
  size?: "md" | "lg";
}

/** U1: a square icon button (row actions: view = neutral, edit = info, key = accent, delete = bad). */
export function IconButton({
  icon: Icon,
  label,
  tone = "neutral",
  size = "md",
  className,
  ...rest
}: Props) {
  const classes = [
    "icon-btn",
    size === "lg" ? "icon-btn--lg" : `icon-btn--${tone}`,
    className,
  ]
    .filter(Boolean)
    .join(" ");
  return (
    <button
      type="button"
      className={classes}
      aria-label={label}
      title={label}
      {...rest}
    >
      <Icon size={size === "lg" ? 18 : 16} aria-hidden="true" />
    </button>
  );
}
