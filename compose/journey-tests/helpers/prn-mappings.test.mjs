import assert from "node:assert/strict";
import { test } from "node:test";
import "./test-support.mjs";
import { mappingNamed } from "./test-support.mjs";
import * as prns from "./journey-prns.mjs";
import * as prnMappings from "./prn-mappings.mjs";

test("the producer search mapping lists every awaiting PRN and sorts newest first by default", () => {
  const search = mappingNamed(prnMappings.prnMappings, "journey-producer-prns.json");

  assert.equal(search.Response.BodyAsJson.totalItems, 11);
  assert.equal(search.Response.BodyAsJson.items[0].prnNumber, "PRN123");
});

test("the aluminium filter returns only the aluminium PRNs", () => {
  const filtered = mappingNamed(prnMappings.prnMappings, "journey-producer-prns-filter-awaiting-aluminium.json");

  assert.deepEqual(
    filtered.Response.BodyAsJson.items.map((prn) => prn.prnNumber).sort(),
    ["PRN123", "PRN130"],
  );
});

test("the compliance search mapping lists all eight awaiting PRNs", () => {
  const search = mappingNamed(prnMappings.prnMappings, "journey-compliance-scheme-prns.json");

  assert.equal(search.Response.BodyAsJson.totalItems, 8);
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

const journeyYear = Number(prns.producerPrns[0].obligationYear);

const paramsOf = (mapping) =>
  Object.fromEntries(mapping.Request.Params.map((param) => [param.Name, param.Matchers[0].Pattern]));

const priorityOf = (list, name) => mappingNamed(list, name).Priority;

// The year stubs follow the date, so the tests that describe them pin it: June of the journey year is outside the
// December waste window, and 15 December of the journey year is inside it.
const outsideWindow = new Date(Date.UTC(journeyYear, 5, 15, 12));
const insideWindow = new Date(Date.UTC(journeyYear, 11, 15, 12));
const yearMappingsAt = (now) => ({
  producer: prnMappings.yearMappings({
    prefix: "journey-producer-prns",
    searchMapping: prnMappings.prnSearchMapping,
    items: prnMappings.searchPrns().items,
    now,
  }),
  compliance: prnMappings.yearMappings({
    prefix: "journey-compliance-scheme-prns",
    searchMapping: prnMappings.compliancePrnSearchMapping,
    items: prns.complianceAwaitingSearchItems,
    now,
  }),
});

test("a year no note can be accepted into answers with an empty list whatever the filter, sort or page", () => {
  const next = mappingNamed(yearMappingsAt(outsideWindow).producer, `journey-producer-prns-year-${journeyYear + 1}.json`);

  assert.deepEqual(next.Response.BodyAsJson, { items: [], totalItems: 0 });
  assert.deepEqual(paramsOf(next), { acceptableIntoYear: String(journeyYear + 1) });
  assert.ok(next.Priority < priorityOf(prnMappings.prnMappings, "journey-producer-prns-filter-awaiting-all-sort-date-issued-desc.json"));
});

test("the journey year needs no year-wide mapping because every note is acceptable into it", () => {
  const names = prnMappings.prnMappings.map(([name]) => name);

  assert.equal(names.includes(`journey-producer-prns-year-${journeyYear}.json`), false);
});

test("single-item pages return the nth newest note and the full total", () => {
  const first = mappingNamed(prnMappings.prnMappings, `journey-producer-prns-year-${journeyYear}-size-1-page-1.json`);
  const second = mappingNamed(prnMappings.prnMappings, `journey-producer-prns-year-${journeyYear}-size-1-page-2.json`);

  assert.deepEqual(paramsOf(first), {
    acceptableIntoYear: String(journeyYear),
    filterBy: "awaiting-all",
    pageSize: "1",
    page: "1",
  });
  assert.equal(first.Response.BodyAsJson.totalItems, 11);
  assert.deepEqual(first.Response.BodyAsJson.items.map((prn) => prn.prnNumber), ["PRN123"]);
  assert.equal(second.Response.BodyAsJson.items.length, 1);
  assert.notEqual(second.Response.BodyAsJson.items[0].prnNumber, "PRN123");
});

test("a page past the end has no items but keeps the total, and ranks below the exact pages", () => {
  const past = mappingNamed(prnMappings.prnMappings, `journey-producer-prns-year-${journeyYear}-size-1-past-end.json`);
  const exact = mappingNamed(prnMappings.prnMappings, `journey-producer-prns-year-${journeyYear}-size-1-page-1.json`);

  assert.deepEqual(past.Response.BodyAsJson, { items: [], totalItems: 11 });
  assert.deepEqual(paramsOf(past), {
    acceptableIntoYear: String(journeyYear),
    filterBy: "awaiting-all",
    pageSize: "1",
  });
  assert.ok(exact.Priority < past.Priority);
});

test("inside the December waste window the next year lists the journey year's December waste notes", () => {
  const { producer, compliance } = yearMappingsAt(insideWindow);
  const numbers = (mapping) => mapping.Response.BodyAsJson.items.map((prn) => prn.prnNumber);
  const producerNext = mappingNamed(producer, `journey-producer-prns-year-${journeyYear + 1}.json`);
  const complianceNext = mappingNamed(compliance, `journey-compliance-scheme-prns-year-${journeyYear + 1}.json`);

  assert.deepEqual(numbers(producerNext), ["PRN870"]);
  assert.equal(producerNext.Response.BodyAsJson.totalItems, 1);
  assert.deepEqual(numbers(complianceNext), ["PRN872"]);
  assert.equal(complianceNext.Response.BodyAsJson.totalItems, 1);
});

test("the compliance scheme gets the same year and paging mappings for its own notes", () => {
  const { compliance } = yearMappingsAt(outsideWindow);
  const next = mappingNamed(compliance, `journey-compliance-scheme-prns-year-${journeyYear + 1}.json`);
  const last = mappingNamed(
    compliance,
    `journey-compliance-scheme-prns-year-${journeyYear}-size-1-page-8.json`,
  );

  assert.deepEqual(next.Response.BodyAsJson, { items: [], totalItems: 0 });
  assert.equal(last.Response.BodyAsJson.totalItems, 8);
  assert.equal(last.Response.BodyAsJson.items.length, 1);
  assert.equal(last.Request.Headers[0].Matchers[0].Pattern, "5165d0cb-fb61-4fd4-8b5e-75fa0e99a323");
});
