import assert from "node:assert/strict";
import { test } from "node:test";
import "./test-support.mjs";
import { mappingNamed } from "./test-support.mjs";
import * as obligationMappings from "./obligation-mappings.mjs";

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
    journeyPrnNumber: "none",
    journeyAccepted: false,
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
