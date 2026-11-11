import assert from "node:assert/strict";
import { test } from "node:test";
import "./test-support.mjs";
import * as config from "./journey-config.mjs";
import * as prns from "./journey-prns.mjs";

test("the producer has eleven awaiting PRNs for its own organisation, nine standard and two December waste", () => {
  assert.equal(prns.producerPrns.length, 11);
  assert.equal(new Set(prns.producerPrns.map((prn) => prn.prnNumber)).size, 11);
  for (const prn of prns.producerPrns) {
    assert.equal(prn.organisationId, config.directProducerId);
    assert.equal(prn.prnStatus, "AWAITINGACCEPTANCE");
  }
  for (const prn of prns.producerPrns.slice(0, 9)) {
    assert.equal(prn.obligationYear, config.obligationYear);
    assert.equal(prn.decemberWaste, false);
  }
});

test("the December waste notes are from the journey year and the year before, and the oldest on the list", () => {
  const [current, carriedOver] = prns.producerDecemberWastePrns;

  assert.deepEqual(prns.producerDecemberWastePrns.map((prn) => prn.prnNumber), ["PRN870", "PRN871"]);
  assert.equal(current.obligationYear, config.obligationYear);
  assert.equal(carriedOver.obligationYear, String(Number(config.obligationYear) - 1));
  for (const prn of prns.producerDecemberWastePrns) {
    assert.equal(prn.decemberWaste, true);
    assert.equal(prn.prnStatus, "AWAITINGACCEPTANCE");
    assert.ok(prns.producerPrns.slice(0, 9).every((standard) => standard.issueDate > prn.issueDate));
  }
  assert.deepEqual(prns.complianceDecemberWastePrns.map((prn) => prn.prnNumber), ["PRN872", "PRN873"]);
  for (const prn of prns.complianceDecemberWastePrns) {
    assert.equal(prn.organisationId, config.complianceSchemeId);
    assert.equal(prn.decemberWaste, true);
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

test("the compliance awaiting search lists eight PRNs, starting with PRN456 and PERN458", () => {
  const items = prns.complianceAwaitingSearchItems;

  assert.deepEqual(
    items.map((prn) => prn.prnNumber),
    ["PRN456", "PERN458", "PRN459", "PRN460", "PRN461", "PRN462", "PRN872", "PRN873"],
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
