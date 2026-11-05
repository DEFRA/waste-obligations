import assert from "node:assert/strict";
import { test } from "node:test";
import "./test-support.mjs";
import * as config from "./journey-config.mjs";

test("required returns a set value and names the variable when it is missing", () => {
  process.env.TEST_REQUIRED_VALUE = "value";
  assert.equal(config.required("TEST_REQUIRED_VALUE"), "value");

  delete process.env.TEST_REQUIRED_VALUE;
  assert.throws(() => config.required("TEST_REQUIRED_VALUE"), {
    message: "TEST_REQUIRED_VALUE must be set",
  });
});

test("journey-config reads the organisation and submitter IDs from the environment", () => {
  assert.equal(config.directProducerId, process.env.WASTE_OBLIGATION_ORG_ID);
  assert.equal(config.complianceSchemeId, process.env.WASTE_OBLIGATION_CSO_ORG_ID);
  assert.equal(config.submitterId, process.env.WASTE_OBLIGATION_SUBMITTER_ID);
  assert.equal(config.submitterEmail, process.env.WASTE_OBLIGATION_SUBMITTER_EMAIL);
});
