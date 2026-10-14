import { MapPin } from "lucide-react";
import type { AddressResponse } from "@/types/address";
import { Badge } from "../Badge";
import { ContactCard, ContactList } from "./ContactCard";

/** G1a: a person's addresses on a detail page, primary first. */
export function AddressList({ addresses }: { addresses: AddressResponse[] }) {
  if (addresses.length === 0) {
    return (
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
