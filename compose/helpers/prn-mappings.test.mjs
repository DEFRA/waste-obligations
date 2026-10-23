import assert from "node:assert/strict";
import { test } from "node:test";
import "./test-support.mjs";
import { mappingNamed } from "./test-support.mjs";
import * as prns from "./journey-prns.mjs";
import * as prnMappings from "./prn-mappings.mjs";

test("the producer search mapping lists every awaiting PRN and sorts newest first by default", () => {
  const search = mappingNamed(prnMappings.prnMappings, "journey-producer-prns.json");

  assert.equal(search.Response.BodyAsJson.totalItems, 9);
  assert.equal(search.Response.BodyAsJson.items[0].prnNumber, "PRN123");
});

test("the aluminium filter returns only the aluminium PRNs", () => {
  const filtered = mappingNamed(prnMappings.prnMappings, "journey-producer-prns-filter-awaiting-aluminium.json");

  assert.deepEqual(
    filtered.Response.BodyAsJson.items.map((prn) => prn.prnNumber).sort(),
    ["PRN123", "PRN130"],
  );
});

test("the compliance search mapping lists all six awaiting PRNs", () => {
  const search = mappingNamed(prnMappings.prnMappings, "journey-compliance-scheme-prns.json");

  assert.equal(search.Response.BodyAsJson.totalItems, 6);
});

test("every PRN mapping file name is unique", () => {
  const names = prnMappings.prnMappings.map(([name]) => name);

  assert.equal(new Set(names).size, names.length);
});
