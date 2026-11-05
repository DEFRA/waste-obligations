// The obligation year the journey stubs answer for. The compliance year runs from
// February to January in Europe/London, so January belongs to the previous year.
// This matches getComplianceYear in the journey tests, so the stubs stay in step with the clock.
const londonYearMonth = new Intl.DateTimeFormat("en-GB", {
    timeZone: "Europe/London",
    year: "numeric",
    month: "numeric",
});

export const complianceYearOn = (date = new Date()) => {
    const parts = londonYearMonth.formatToParts(date);
    const year = Number(parts.find((part) => part.type === "year").value);
    const month = Number(parts.find((part) => part.type === "month").value);

    return month === 1 ? year - 1 : year;
};

// JOURNEY_TEST_OBLIGATION_YEAR fixes the year for a run. Without it, the current compliance year is used.
export const resolveObligationYear = (override, date = new Date()) => {
    if (override === undefined || override === "") {
        return String(complianceYearOn(date));
    }
    if (!/^\d{4}$/.test(override)) {
        throw new Error(`JOURNEY_TEST_OBLIGATION_YEAR must be a four-digit year, got "${override}"`);
    }

    return override;
};
