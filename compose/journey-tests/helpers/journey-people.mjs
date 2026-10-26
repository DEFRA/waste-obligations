// People the journey WireMock Account stubs answer with.

import {
    delegatedPersonRole,
    submitterEmail,
    submitterId,
} from "./journey-config.mjs";

// These people mirror the epr-local-environment Account seed. The scenario
// submitter remains an overlay because the journey suite supplies it in admin
// lifecycle requests.
export const directProducerPeople = [
    {
        userId: "79d0deab-c22d-4c30-8082-508ff8dc1bd7",
        firstName: "Direct",
        lastName: "Producer",
        email: "test+directproducer@ee.com",
        serviceRole: "Approved Person",
    },
    {
        userId: "513a78ee-d5bf-4fa4-9d8f-136550ea6072",
        firstName: "SB FirstName",
        lastName: "SB LastName",
        email: "bmmmdmgz@sharklasers.com",
        serviceRole: delegatedPersonRole,
    },
];

export const complianceSchemePeople = [
    {
        userId: "579c319d-d552-47a2-bf4c-5a125a3183bc",
        firstName: "First name",
        lastName: "Last Name",
        email: "test+17122025143216@ee.com",
        serviceRole: "Approved Person",
    },
    {
        userId: "ef2fd2a5-24bf-4b22-89a0-17a0367aee1c",
        firstName: "Francis",
        lastName: "Delegated",
        email: "francis.chelladurai+07042026@equalexperts.com",
        serviceRole: delegatedPersonRole,
    },
];

export const scenarioSubmitter = {
    userId: submitterId,
    firstName: "Journey-test",
    lastName: "Submitter",
    email: submitterEmail,
    serviceRole: delegatedPersonRole,
};

export const addScenarioSubmitter = (people) =>
    people.some(
        (person) => person.userId.toLowerCase() === submitterId.toLowerCase(),
    )
        ? people
        : [...people, scenarioSubmitter];
