import {
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
} from "react";
import { Check, ChevronDown, Search } from "lucide-react";
import type { ControlProps } from "./Field";

export interface SearchableOption {
  value: string | number;
  label: string;
}

/** Lists with this many entries (including the empty one) or more get a search box. */
const SEARCH_FROM = 6;

interface Props extends Partial<ControlProps> {
  options: readonly SearchableOption[];
  value: string | number | null | undefined;
  onChange: (value: string) => void;
  /** First entry with value "" (for example "Not specified" or "All classes"). */
  emptyLabel?: string;
  disabled?: boolean;
  /** Used when there is no visible <label> for the control. */
  ariaLabel?: string;
  /** Extra class for the native (short-list) <select>. */
  className?: string;
}

/**
 * A drop-down with a search box. Short lists stay a plain <select> (a search box on three options
 * only gets in the way); from six entries up the list opens in a panel with a "Search…" field at
 * the top. Same props and the same string-in/string-out contract as <select>, so any page can swap
 * one for the other.
 */
export function SearchableSelect(props: Props) {
  const all = useMemo<SearchableOption[]>(
    () =>
      props.emptyLabel !== undefined
        ? [{ value: "", label: props.emptyLabel }, ...props.options]
        : [...props.options],
    [props.options, props.emptyLabel],
  );
  return all.length < SEARCH_FROM ? (
    <NativeSelect {...props} all={all} />
  ) : (
    <ComboSelect {...props} all={all} />
  );
}

function NativeSelect({
  all,
  value,
  onChange,
  disabled,
  ariaLabel,
  className,
  id,
  ...aria
}: Props & { all: SearchableOption[] }) {
  return (
    <select
      id={id}
      className={className}
      value={value ?? ""}
      disabled={disabled}
      aria-label={ariaLabel}
      aria-invalid={aria["aria-invalid"]}
      aria-describedby={aria["aria-describedby"]}
      aria-required={aria["aria-required"]}
      onChange={(e) => onChange(e.target.value)}
    >
      {all.map((o) => (
        <option key={o.value} value={o.value}>
          {o.label}
        </option>
      ))}
    </select>
  );
}

function ComboSelect({
  all,
  value,
  onChange,
  disabled,
  ariaLabel,
  id,
  ...aria
}: Props & { all: SearchableOption[] }) {
  const autoId = useId();
  const listId = `${autoId}-list`;
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [active, setActive] = useState(0);
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);

  const current = all.find((o) => String(o.value) === String(value ?? ""));
  const shown = useMemo(() => {
    const q = query.trim().toLowerCase();
    return q ? all.filter((o) => o.label.toLowerCase().includes(q)) : all;
  }, [all, query]);

  // Close when the user clicks anywhere else.
  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, [open]);

  useEffect(() => {
    if (open) searchRef.current?.focus();
  }, [open]);

  const openList = () => {
    if (disabled) return;
    setQuery("");
    const at = all.findIndex((o) => String(o.value) === String(value ?? ""));
    setActive(Math.max(at, 0));
    setOpen(true);
  };

  const choose = (o: SearchableOption) => {
    onChange(String(o.value));
    setOpen(false);
    triggerRef.current?.focus();
  };

  const onSearchKey = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setActive((i) => Math.min(i + 1, shown.length - 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActive((i) => Math.max(i - 1, 0));
    } else if (e.key === "Enter") {
      e.preventDefault(); // never submits the surrounding form
      const o = shown[active];
      if (o) choose(o);
    } else if (e.key === "Escape") {
      e.preventDefault();
      e.stopPropagation(); // do not also close a dialog this select sits in
      setOpen(false);
      triggerRef.current?.focus();
    } else if (e.key === "Tab") {
      setOpen(false);
    }
  };

  return (
    <div className="ss" ref={rootRef}>
      <button
        type="button"
        id={id}
        ref={triggerRef}
        className="ss__trigger"
        role="combobox"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listId}
        aria-label={ariaLabel}
        aria-invalid={aria["aria-invalid"]}
        aria-describedby={aria["aria-describedby"]}
        aria-required={aria["aria-required"]}
        disabled={disabled}
        onClick={() => (open ? setOpen(false) : openList())}
        onKeyDown={(e) => {
          if (e.key === "ArrowDown" || e.key === "ArrowUp") {
            e.preventDefault();
            openList();
          }
        }}
      >
        <span className={current?.value === "" ? "ss__placeholder" : undefined}>
          {current?.label ?? "Select…"}
        </span>
        <ChevronDown size={16} aria-hidden="true" />
      </button>
      {open && (
        <div className="ss__panel">
          <div className="ss__search">
            <Search size={14} aria-hidden="true" />
            <input
              ref={searchRef}
              type="text"
              value={query}
              placeholder="Search…"
              aria-label="Search the list"
              aria-controls={listId}
              aria-activedescendant={
                shown[active] ? `${autoId}-opt-${active}` : undefined
              }
              autoComplete="off"
              onChange={(e) => {
                setQuery(e.target.value);
                setActive(0);
              }}
              onKeyDown={onSearchKey}
            />
          </div>
          <ul className="ss__list" id={listId} role="listbox">
            {shown.length === 0 && <li className="ss__empty">No matches</li>}
            {shown.map((o, i) => {
              const selected = String(o.value) === String(value ?? "");
              return (
                <li
                  key={o.value}
                  id={`${autoId}-opt-${i}`}
                  role="option"
                  aria-selected={selected}
                  className={`ss__option${i === active ? " ss__option--active" : ""}`}
                  onMouseEnter={() => setActive(i)}
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => choose(o)}
                >
                  <span>{o.label}</span>
                  {selected && <Check size={14} aria-hidden="true" />}
                </li>
              );
            })}
          </ul>
        </div>
      )}
    </div>
  );
}
