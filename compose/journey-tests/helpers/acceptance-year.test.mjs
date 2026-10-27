import assert from "node:assert/strict";
import { test } from "node:test";
import { acceptableIntoYear, acceptanceYears, complianceYearOn } from "./acceptance-year.mjs";

const standard = (obligationYear) => ({ obligationYear: String(obligationYear), decemberWaste: false });
const december = (obligationYear) => ({ obligationYear: String(obligationYear), decemberWaste: true });
const at = (iso) => new Date(iso);

test("a standard note is acceptable only into the current compliance year", () => {
  const now = at("2026-12-15T12:00:00Z");

  assert.deepEqual(acceptanceYears(standard(2026), now), [2026]);
  assert.deepEqual(acceptanceYears(standard(2025), now), []);
  assert.deepEqual(acceptanceYears(standard(2027), now), []);
});

test("December waste offers its own year and the next inside the choice window", () => {
  assert.deepEqual(acceptanceYears(december(2026), at("2026-12-15T12:00:00Z")), [2026, 2027]);
  assert.deepEqual(acceptanceYears(december(2026), at("2027-01-20T12:00:00Z")), [2026, 2027]);
});

test("December waste offers only its own year before the window and only the next year after it", () => {
  assert.deepEqual(acceptanceYears(december(2026), at("2026-06-15T12:00:00Z")), [2026]);
  assert.deepEqual(acceptanceYears(december(2026), at("2027-02-15T12:00:00Z")), [2027]);
  assert.deepEqual(acceptanceYears(december(2024), at("2027-02-15T12:00:00Z")), []);
});

test("December 2025 waste never offers a choice", () => {
  assert.deepEqual(acceptanceYears(december(2025), at("2026-06-15T12:00:00Z")), [2026]);
  assert.deepEqual(acceptanceYears(december(2025), at("2026-12-15T12:00:00Z")), [2026]);
});

test("a note is acceptable into a year when that year is one of its years", () => {
  const now = at("2026-12-15T12:00:00Z");

  assert.equal(acceptableIntoYear(december(2026), 2027, now), true);
  assert.equal(acceptableIntoYear(standard(2026), 2027, now), false);
});

test("the compliance year runs February to January in London time", () => {
  assert.equal(complianceYearOn(at("2027-01-31T23:30:00Z")), 2026);
  assert.equal(complianceYearOn(at("2027-02-01T00:30:00Z")), 2027);
});
