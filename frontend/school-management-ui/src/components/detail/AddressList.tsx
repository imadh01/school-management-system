import { MapPin } from "lucide-react";
import type { AddressResponse } from "@/types/address";
import { Badge } from "../Badge";
import { ContactCard, ContactList } from "./ContactCard";

/** G1a: a person's addresses on a detail page, primary first; the old one-line address if it was not copied. */
export function AddressList({
  addresses,
  legacyAddress,
}: {
  addresses: AddressResponse[];
  legacyAddress?: string | null;
}) {
  if (addresses.length === 0) {
    return legacyAddress ? (
      <p style={{ margin: 0 }}>
        {legacyAddress}{" "}
        <span className="cell-muted">
          (old format: edit to add the country)
        </span>
      </p>
    ) : (
      <p className="cell-muted" style={{ margin: 0 }}>
        No address recorded.
      </p>
    );
  }
  return (
    <ContactList>
      {addresses.map((a) => (
        <ContactCard
          key={a.id}
          name={`${a.addressType} address`}
          badges={a.isPrimary ? <Badge tone="info">Primary</Badge> : undefined}
        >
          <div
            className="contact-card__line"
            style={{ alignItems: "flex-start" }}
          >
            <MapPin
              size={14}
              aria-hidden="true"
              style={{ marginTop: 3, flexShrink: 0 }}
            />
            <span>
              {a.line1}
              {a.line2 && (
                <>
                  <br />
                  {a.line2}
                </>
              )}
              <br />
              {[a.city, a.regionName, a.postalCode].filter(Boolean).join(", ")}
              <br />
              {a.countryName}
            </span>
          </div>
        </ContactCard>
      ))}
    </ContactList>
  );
}
