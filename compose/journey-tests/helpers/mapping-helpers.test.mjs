import assert from "node:assert/strict";
import { test } from "node:test";
import "./test-support.mjs";
import * as mappingHelpers from "./mapping-helpers.mjs";

test("json pretty-prints with two-space indentation", () => {
  assert.equal(mappingHelpers.json({ a: 1 }), '{\n  "a": 1\n}');
});

test("mapping wraps a request and body with a 200 JSON response by default", () => {
  const request = { Path: {} };
  const body = { items: [] };

  const result = mappingHelpers.mapping(request, body);

  assert.deepEqual(result, {
    Request: request,
    Response: {
      StatusCode: 200,
      BodyAsJson: body,
      Headers: { "Content-Type": "application/json; charset=utf-8" },
    },
  });
});

test("mapping uses the status code it is given", () => {
  assert.equal(mappingHelpers.mapping({}, {}, 404).Response.StatusCode, 404);
});

test("exactPath matches the path exactly and only for GET", () => {
  assert.deepEqual(mappingHelpers.exactPath("/api/v1/prn/search"), {
    Path: { Matchers: [{ Name: "ExactMatcher", Pattern: "/api/v1/prn/search" }] },
    Methods: ["GET"],
  });
});

test("organisationHeader matches the X-EPR-ORGANISATION header exactly", () => {
  assert.deepEqual(mappingHelpers.organisationHeader("org-1"), [
    { Name: "X-EPR-ORGANISATION", Matchers: [{ Name: "ExactMatcher", Pattern: "org-1" }] },
  ]);
});

test("queryParam matches a named query parameter exactly", () => {
  assert.deepEqual(mappingHelpers.queryParam("filterBy", "awaiting-all"), {
    Name: "filterBy",
    Matchers: [{ Name: "ExactMatcher", Pattern: "awaiting-all" }],
  });
});

test("search priorities put the most specific mappings first", () => {
  const { PRN_MAPPING_1, PRN_MAPPING_2, PRN_MAPPING_3, PRN_MAPPING_10 } = mappingHelpers;

  assert.ok(PRN_MAPPING_1 < PRN_MAPPING_2);
  assert.ok(PRN_MAPPING_2 < PRN_MAPPING_3);
  assert.ok(PRN_MAPPING_3 < PRN_MAPPING_10);
});
