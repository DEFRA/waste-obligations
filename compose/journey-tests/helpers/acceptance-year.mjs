// Which years a note can be accepted into, for the journey WireMock stubs that stand in for the common backend's
// acceptableIntoYear filter. Mirrors the backend's AcceptanceYearRule: most notes only go into the current compliance
// year, December waste may also, or instead, go into the next.

import { complianceYearOn } from "./journey-year.mjs";

export { complianceYearOn };

const DECEMBER = 11;
const FEBRUARY = 1;
const DECEMBER_WASTE_NO_CHOICE_YEAR = 2025;
const DECEMBER_WASTE_NO_CHOICE_UNTIL_YEAR = 2026;

export const acceptanceYears = (prn, now = new Date()) => {
    const prnYear = Number(prn.obligationYear);
    const thisComplianceYear = complianceYearOn(now);

    if (!Number.isInteger(prnYear) || prnYear > thisComplianceYear) {
        return [];
    }

    return prn.decemberWaste
        ? decemberWasteYears(prnYear, thisComplianceYear, now)
        : standardYears(prnYear, thisComplianceYear);
};

const standardYears = (prnYear, thisComplianceYear) =>
    prnYear === thisComplianceYear ? [prnYear] : [];

const decemberWasteYears = (prnYear, thisComplianceYear, now) => {
    if (
        prnYear === DECEMBER_WASTE_NO_CHOICE_YEAR &&
        thisComplianceYear <= DECEMBER_WASTE_NO_CHOICE_UNTIL_YEAR
    ) {
        return [thisComplianceYear];
    }

    const windowStart = Date.UTC(prnYear, DECEMBER, 1);
    const windowEnd = Date.UTC(prnYear + 1, FEBRUARY, 1);
    if (now.getTime() >= windowStart && now.getTime() < windowEnd) {
        return [prnYear, prnYear + 1];
    }
    if (prnYear === thisComplianceYear) {
        return [prnYear];
    }

    return prnYear + 1 === thisComplianceYear ? [thisComplianceYear] : [];
};

export const acceptableIntoYear = (prn, year, now = new Date()) =>
    acceptanceYears(prn, now).includes(year);
