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

test("the paper and fibre filter returns only the paper/board and fibre PRNs", () => {
  const filtered = mappingNamed(prnMappings.prnMappings, "journey-producer-prns-filter-awaiting-paperfiber.json");

  assert.deepEqual(filtered.Response.BodyAsJson.items.map((prn) => prn.prnNumber).sort(), ["PRN128", "PRN132"]);
});

test("the wood filter is empty because no wood PRN is awaiting", () => {
  const filtered = mappingNamed(prnMappings.prnMappings, "journey-producer-prns-filter-awaiting-wood.json");

  assert.equal(filtered.Response.BodyAsJson.totalItems, 0);
});

test("the tonnage sorts put the largest and smallest PRNs first", () => {
  const descending = mappingNamed(prnMappings.prnMappings, "journey-producer-prns-sort-tonnage-desc.json");
  const ascending = mappingNamed(prnMappings.prnMappings, "journey-producer-prns-sort-tonnage-asc.json");

  assert.equal(descending.Response.BodyAsJson.items[0].prnNumber, "PRN127");
  assert.equal(ascending.Response.BodyAsJson.items[0].prnNumber, "PRN128");
});
