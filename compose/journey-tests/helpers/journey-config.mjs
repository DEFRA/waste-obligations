// Environment settings for the journey WireMock generator.

import { resolveObligationYear } from "./journey-year.mjs";

export const required = (name) => {
    const value = process.env[name];
    if (!value) {
        throw new Error(`${name} must be set`);
    }

    return value;
};

export const directProducerId = required("WASTE_OBLIGATION_ORG_ID");
export const complianceSchemeId = required("WASTE_OBLIGATION_CSO_ORG_ID");
export const submitterId = required("WASTE_OBLIGATION_SUBMITTER_ID");
export const submitterEmail = required("WASTE_OBLIGATION_SUBMITTER_EMAIL");
export const delegatedPersonRole = "Delegated Person";
export const obligationYear = resolveObligationYear(process.env.JOURNEY_TEST_OBLIGATION_YEAR);
