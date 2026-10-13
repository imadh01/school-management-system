import { Search } from "lucide-react";

/** U2: the search field at the end of a FilterBar. */
export function SearchBox({
  value,
  onChange,
  label,
  placeholder,
}: {
  value: string;
  onChange: (value: string) => void;
  /** Read by screen readers ("Search parents"). */
  label: string;
  placeholder: string;
}) {
  return (
    <label className="search-box search-box--inline">
      <Search size={16} aria-hidden="true" />
      <span className="visually-hidden">{label}</span>
      <input
        type="search"
        placeholder={placeholder}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    </label>
  );
}
