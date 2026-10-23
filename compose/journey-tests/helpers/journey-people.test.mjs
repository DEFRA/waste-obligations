import assert from "node:assert/strict";
import { test } from "node:test";
import "./test-support.mjs";
import * as config from "./journey-config.mjs";
import * as people from "./journey-people.mjs";

test("each account's people include a delegated person", () => {
  for (const list of [people.directProducerPeople, people.complianceSchemePeople]) {
    assert.equal(list.length, 2);
    assert.ok(list.some((person) => person.serviceRole === config.delegatedPersonRole));
  }
});

test("addScenarioSubmitter appends the submitter when it is not already listed", () => {
  const list = people.addScenarioSubmitter(people.directProducerPeople);

  assert.equal(list.length, people.directProducerPeople.length + 1);
  assert.equal(list.at(-1).userId, config.submitterId);
  assert.equal(list.at(-1).email, config.submitterEmail);
});

test("addScenarioSubmitter returns the list unchanged when the submitter is already listed", () => {
  const listed = [
    ...people.directProducerPeople,
    { userId: config.submitterId.toUpperCase(), firstName: "A", lastName: "B", email: "x", serviceRole: "Approved Person" },
  ];

  assert.equal(people.addScenarioSubmitter(listed), listed);
});
