import assert from "node:assert/strict";
import { test } from "node:test";
import "./test-support.mjs";
import * as config from "./journey-config.mjs";
import * as prns from "./journey-prns.mjs";

test("the producer has nine awaiting PRNs for its own organisation, all with the current obligation year", () => {
  assert.equal(prns.producerPrns.length, 9);
  assert.equal(new Set(prns.producerPrns.map((prn) => prn.prnNumber)).size, 9);
  for (const prn of prns.producerPrns) {
    assert.equal(prn.organisationId, config.directProducerId);
    assert.equal(prn.prnStatus, "AWAITINGACCEPTANCE");
    assert.equal(prn.obligationYear, config.obligationYear);
    assert.equal(prn.decemberWaste, false);
  }
});

test("prnFixture builds a standard awaiting PRN with the values it is given", () => {
  const prn = prns.prnFixture("PRN999", "ext-1", "Steel", 42, "2026-01-02T03:04:05Z");

  assert.equal(prn.prnNumber, "PRN999");
  assert.equal(prn.externalId, "ext-1");
  assert.equal(prn.materialName, "Steel");
  assert.equal(prn.tonnageValue, 42);
  assert.equal(prn.issueDate, "2026-01-02T03:04:05Z");
  assert.equal(prn.createdOn, prn.issueDate);
  assert.equal(prn.isExport, false);
});

test("the compliance awaiting search lists six PRNs, starting with PRN456 and PERN458", () => {
  const items = prns.complianceAwaitingSearchItems;

  assert.deepEqual(
    items.map((prn) => prn.prnNumber),
    ["PRN456", "PERN458", "PRN459", "PRN460", "PRN461", "PRN462"],
  );
  assert.equal(items[0].organisationId, config.complianceSchemeId);
  assert.equal(items[1].isExport, true);
});

test("the compliance obligation inputs exclude PERN458 only where they must (the list itself keeps it)", () => {
  assert.ok(!prns.complianceAwaitingPrns.some((prn) => prn.prnNumber === "PERN458"));
  assert.ok(prns.complianceAwaitingSearchItems.some((prn) => prn.prnNumber === "PERN458"));
});

test("the rejected PDF fixtures use numbers that are not awaiting", () => {
  const awaitingNumbers = new Set(prns.producerPrns.map((prn) => prn.prnNumber));

  assert.equal(prns.rejectedProducerPrn.prnStatus, "REJECTED");
  assert.equal(prns.rejectedCompliancePrn.prnStatus, "REJECTED");
  assert.ok(!awaitingNumbers.has(prns.rejectedProducerPrn.prnNumber));
});

test("accepted fixtures carry their accepted status", () => {
  assert.equal(prns.acceptedProducerPrn.prnStatus, "ACCEPTED");
  assert.equal(prns.acceptedCompliancePrn.prnStatus, "ACCEPTED");
  assert.equal(prns.acceptedCompliancePrn.isExport, true);
});
