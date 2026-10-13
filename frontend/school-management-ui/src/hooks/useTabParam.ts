import { useSearchParams } from "react-router-dom";

/**
 * The open tab of a detail page, kept in the address (?tab=health) so a link or a reload opens
 * the same tab. The first tab is the default and is not written to the address.
 */
export function useTabParam(
  tabIds: readonly string[],
): [string, (tab: string) => void] {
  const [params, setParams] = useSearchParams();
  const requested = params.get("tab");
  const active =
    requested && tabIds.includes(requested) ? requested : tabIds[0];
  const select = (tab: string) =>
    setParams(tab === tabIds[0] ? {} : { tab }, { replace: true });
  return [active, select];
}
