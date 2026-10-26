import assert from "node:assert/strict";
import { test } from "node:test";
import { complianceYearOn, resolveObligationYear } from "./journey-year.mjs";

test("the compliance year changes on 1 February, not 1 January", () => {
  assert.equal(complianceYearOn(new Date(Date.UTC(2027, 0, 31, 23, 30))), 2026);
  assert.equal(complianceYearOn(new Date(Date.UTC(2027, 1, 1, 0, 30))), 2027);
});

test("the compliance year follows Europe/London, not UTC, across the year boundary", () => {
  assert.equal(complianceYearOn(new Date(Date.UTC(2026, 0, 31, 23, 59))), 2025);
  assert.equal(complianceYearOn(new Date(Date.UTC(2026, 10, 15))), 2026);
});

test("resolveObligationYear uses the current compliance year when no override is set", () => {
  assert.equal(resolveObligationYear(undefined, new Date(Date.UTC(2027, 2, 10))), "2027");
  assert.equal(resolveObligationYear("", new Date(Date.UTC(2027, 2, 10))), "2027");
});

test("resolveObligationYear uses a four-digit override as given", () => {
  assert.equal(resolveObligationYear("2030", new Date(Date.UTC(2027, 2, 10))), "2030");
});

test("resolveObligationYear rejects an override that is not a four-digit year", () => {
  assert.throws(() => resolveObligationYear("27"), { message: /four-digit year/ });
  assert.throws(() => resolveObligationYear("2026a"), { message: /four-digit year/ });
});
