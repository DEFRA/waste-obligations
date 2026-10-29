import assert from "node:assert/strict";
import { test } from "node:test";
import "./test-support.mjs";
import { mappingNamed } from "./test-support.mjs";
import * as obligationMappings from "./obligation-mappings.mjs";
import { prnMappings } from "./prn-mappings.mjs";
import { PRN_OBLIGATION_YEAR } from "./journey-prns.mjs";

test("the starting-state obligation calculation has lower priority than the state-specific one", () => {
  const unconditional = mappingNamed(obligationMappings.obligationMappings, "journey-producer-obligations.json");
  const accepted = mappingNamed(obligationMappings.obligationMappings, "journey-producer-obligations-bulk-accepted.json");

  assert.equal(unconditional.WhenStateIs, undefined);
  assert.equal(accepted.WhenStateIs, "PaperPrnAccepted");
  assert.ok(accepted.Priority < unconditional.Priority);
});

test("the compliance awaiting count includes PERN458 before the accept and drops one after it", () => {
  const before = mappingNamed(obligationMappings.obligationMappings, "journey-compliance-scheme-obligations.json");
  const after = mappingNamed(obligationMappings.obligationMappings, "journey-compliance-scheme-obligations-accepted.json");
  const aluminium = (mapping) => mapping.Response.BodyAsJson.obligationData.find((row) => row.materialName === "Aluminium");

  assert.equal(before.Response.BodyAsJson.numberOfPrnsAwaitingAcceptance, 6);
  assert.equal(after.Response.BodyAsJson.numberOfPrnsAwaitingAcceptance, 5);
  assert.equal(aluminium(before).tonnageAwaitingAcceptance, 140);
  assert.equal(aluminium(after).tonnageAwaitingAcceptance, 140);
});

test("the unreachable repeat status-update mapping is no longer generated", () => {
  const names = obligationMappings.obligationMappings.map(([name]) => name);

  assert.ok(!names.includes("journey-producer-prn-status-update-repeat.json"));
});

test("status updates move their scenario into the accepted state", () => {
  const producer = mappingNamed(obligationMappings.obligationMappings, "journey-producer-prn-status-update.json");
  const compliance = mappingNamed(obligationMappings.obligationMappings, "journey-compliance-scheme-prn-status-update.json");

  assert.equal(producer.Scenario, "journey-producer-bulk-accept");
  assert.equal(producer.SetStateTo, "PaperPrnAccepted");
  assert.equal(compliance.Scenario, "journey-compliance-scheme-prn-accept");
  assert.equal(compliance.SetStateTo, "PaperPrnAccepted");
});

test("obligation mapping file names are unique", () => {
  const names = obligationMappings.obligationMappings.map(([name]) => name);

  assert.equal(new Set(names).size, names.length);
});

test("an obligation reports Met once accepted tonnage reaches its target, and NotMet below it", () => {
  const calculation = obligationMappings.obligationsFor({
    organisationId: "org-1",
    listed: [],
    accepted: [{ prnNumber: "P1", materialName: "Paper/board", tonnageValue: 500 }],
  });
  const row = (materialName) => calculation.obligationData.find((item) => item.materialName === materialName);

  assert.equal(row("Paper").status, "Met");
  assert.equal(row("Paper").tonnageOutstanding, 0);
  assert.equal(row("Plastic").status, "NotMet");
  assert.equal(row("Plastic").tonnageOutstanding, 600);
});

test("obligationMaterial maps each PRN material to its obligation material and keeps unknown names", () => {
  assert.equal(obligationMappings.obligationMaterial("Paper/board"), "Paper");
  assert.equal(obligationMappings.obligationMaterial("Fibre"), "Paper");
  assert.equal(obligationMappings.obligationMaterial("Glass Other"), "Glass");
  assert.equal(obligationMappings.obligationMaterial("Glass Re-melt"), "GlassRemelt");
  assert.equal(obligationMappings.obligationMaterial("Aluminium"), "Aluminium");
});

test("sumTonnage adds up only the PRNs whose obligation material matches", () => {
  const prns = [
    { materialName: "Paper/board", tonnageValue: 15 },
    { materialName: "Fibre", tonnageValue: 35 },
    { materialName: "Plastic", tonnageValue: 40 },
  ];

  assert.equal(obligationMappings.sumTonnage(prns, "Paper"), 50);
  assert.equal(obligationMappings.sumTonnage(prns, "Wood"), 0);
});

// The body the API sends the common backend for an accept: one update per PRN.
const statusUpdateBody = (prns, { obligationYear = PRN_OBLIGATION_YEAR, status = "ACCEPTED" } = {}) =>
  JSON.stringify(
    prns.map((prn) => ({
      prnId: prn.externalId,
      status,
      obligationYear,
      expectedStatus: "AWAITINGACCEPTANCE",
    })),
  );

const journeyBodyPattern = (name) =>
  new RegExp(mappingNamed(obligationMappings.obligationMappings, name).Request.Body.Matcher.Pattern);

test("the journey status update matches an accept of exactly the journey's PRNs, in any order", () => {
  const pattern = journeyBodyPattern("journey-producer-prn-status-update-journey.json");
  const prns = obligationMappings.producerJourneyPrns;

  assert.ok(pattern.test(statusUpdateBody(prns)));
  assert.ok(pattern.test(statusUpdateBody([...prns].reverse())));
});

test("the journey status update does not match a different selection, year or status", () => {
  const pattern = journeyBodyPattern("journey-producer-prn-status-update-journey.json");
  const prns = obligationMappings.producerJourneyPrns;
  const otherPrn = { externalId: "1f6b9a52-6a0e-4d53-9a4e-6c1f7c2d8e01" };

  assert.ok(!pattern.test(statusUpdateBody(prns.slice(0, 1))), "the single paper/board accept");
  assert.ok(!pattern.test(statusUpdateBody(prns.slice(1))), "a PRN missing");
  assert.ok(!pattern.test(statusUpdateBody([...prns, otherPrn])), "an extra PRN");
  assert.ok(!pattern.test(statusUpdateBody([...prns.slice(1), otherPrn])), "a different PRN");
  assert.ok(!pattern.test(statusUpdateBody(prns, { obligationYear: "2025" })), "another year");
  assert.ok(!pattern.test(statusUpdateBody(prns, { status: "REJECTED" })), "a reject");
});

test("the compliance journey status update names its PERN", () => {
  const pattern = journeyBodyPattern("journey-compliance-scheme-prn-status-update-journey.json");

  assert.ok(obligationMappings.complianceJourneyPrns.some((prn) => prn.isExport));
  assert.ok(pattern.test(statusUpdateBody(obligationMappings.complianceJourneyPrns)));
});

test("the journey status update wins over the catch-all one, which stays for a single accept", () => {
  const catchAll = mappingNamed(obligationMappings.obligationMappings, "journey-producer-prn-status-update.json");
  const journey = mappingNamed(obligationMappings.obligationMappings, "journey-producer-prn-status-update-journey.json");

  assert.equal(catchAll.Request.Body, undefined);
  assert.equal(journey.SetStateTo, "JourneyPrnsAccepted");
  assert.ok(journey.Priority < catchAll.Priority);
});

test("in the journey state the obligations count the journey's PRNs as accepted", () => {
  const before = mappingNamed(obligationMappings.obligationMappings, "journey-producer-obligations.json");
  const after = mappingNamed(obligationMappings.obligationMappings, "journey-producer-obligations-journey-accepted.json");
  const row = (mapping, material) =>
    mapping.Response.BodyAsJson.obligationData.find((item) => item.materialName === material);

  assert.equal(after.WhenStateIs, "JourneyPrnsAccepted");
  assert.equal(
    after.Response.BodyAsJson.numberOfPrnsAwaitingAcceptance,
    before.Response.BodyAsJson.numberOfPrnsAwaitingAcceptance - 4,
  );
  for (const [material, tonnage] of [["Paper", 50], ["Glass", 75], ["GlassRemelt", 220]]) {
    assert.equal(row(after, material).tonnageAccepted, row(before, material).tonnageAccepted + tonnage, material);
    assert.equal(
      row(after, material).tonnageAwaitingAcceptance,
      row(before, material).tonnageAwaitingAcceptance - tonnage,
      material,
    );
  }
});

test("in the journey state the searches move the journey's PRNs from awaiting to accepted", () => {
  const numbers = (mapping) => mapping.Response.BodyAsJson.items.map((prn) => prn.prnNumber);
  const awaiting = mappingNamed(obligationMappings.obligationMappings, "journey-compliance-scheme-prns-journey-accepted-awaiting.json");
  const accepted = mappingNamed(obligationMappings.obligationMappings, "journey-compliance-scheme-prns-journey-accepted-accepted.json");

  for (const number of obligationMappings.COMPLIANCE_JOURNEY_PRN_NUMBERS) {
    assert.ok(!numbers(awaiting).includes(number), number);
    assert.ok(numbers(accepted).includes(number), number);
  }
  assert.deepEqual(numbers(awaiting), ["PRN456", "PRN462"]);
  assert.equal(numbers(accepted)[0], "PERN457");
  assert.ok(accepted.Response.BodyAsJson.items.every((prn) => prn.prnStatus === "ACCEPTED"));
});

test("the journey state's searches win over every other search for the organisation", () => {
  const searches = [...prnMappings, ...obligationMappings.obligationMappings]
    .map(([, mapping]) => mapping)
    .filter((mapping) => mapping.Request.Path.Matchers[0].Pattern === "/api/v1/prn/search");
  const inJourneyState = searches.filter((mapping) => mapping.WhenStateIs === "JourneyPrnsAccepted");
  const others = searches.filter((mapping) => mapping.WhenStateIs === undefined);

  assert.equal(inJourneyState.length, 4);
  for (const mapping of inJourneyState) {
    assert.ok(others.every((other) => mapping.Priority < other.Priority));
  }
});

test("every state-specific read keeps its scenario in that state", () => {
  const reads = obligationMappings.obligationMappings
    .map(([, mapping]) => mapping)
    .filter((mapping) => mapping.WhenStateIs !== undefined);

  assert.ok(reads.length > 0);
  for (const mapping of reads) {
    assert.equal(mapping.SetStateTo, mapping.WhenStateIs);
  }
});
