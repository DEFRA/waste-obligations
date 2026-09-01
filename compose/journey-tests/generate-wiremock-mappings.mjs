import { mkdir, writeFile } from "node:fs/promises";
import { join } from "node:path";

const required = (name) => {
    const value = process.env[name];
    if (!value) {
        throw new Error(`${name} must be set`);
    }

    return value;
};

const outputDirectory =
    process.env.JOURNEY_TEST_WIREMOCK_MAPPINGS_DIR || "/output";
const directProducerId = required("WASTE_OBLIGATION_ORG_ID");
const complianceSchemeId = required("WASTE_OBLIGATION_CSO_ORG_ID");
const submitterId = required("WASTE_OBLIGATION_SUBMITTER_ID");
const submitterEmail = required("WASTE_OBLIGATION_SUBMITTER_EMAIL");
const delegatedPersonRole = "Delegated Person";

await mkdir(outputDirectory, { recursive: true });

const json = (value) => JSON.stringify(value, null, 2);
const mapping = (request, body, statusCode = 200) => ({
    Request: request,
    Response: {
        StatusCode: statusCode,
        BodyAsJson: body,
        Headers: { "Content-Type": "application/json; charset=utf-8" },
    },
});
const exactPath = (path) => ({
    Path: {
        Matchers: [{ Name: "ExactMatcher", Pattern: path }],
    },
    Methods: ["GET"],
});

// These people mirror the epr-local-environment Account seed. The scenario
// submitter remains an overlay because the journey suite supplies it in admin
// lifecycle requests.
const directProducerPeople = [
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

const complianceSchemePeople = [
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

const scenarioSubmitter = {
    userId: submitterId,
    firstName: "Journey-test",
    lastName: "Submitter",
    email: submitterEmail,
    serviceRole: delegatedPersonRole,
};

const addScenarioSubmitter = (people) =>
    people.some(
        (person) => person.userId.toLowerCase() === submitterId.toLowerCase(),
    )
        ? people
        : [...people, scenarioSubmitter];

await writeFile(
    join(
        outputDirectory,
        "backend-account-organisation-with-persons-direct-producer.json",
    ),
    json(
        mapping(
            exactPath(
                `/api/organisations/organisation-with-persons/${directProducerId}`,
            ),
            { persons: addScenarioSubmitter(directProducerPeople) },
        ),
    ),
);

await writeFile(
    join(
        outputDirectory,
        "backend-account-organisation-with-persons-compliance-scheme.json",
    ),
    json(
        mapping(
            exactPath(
                `/api/organisations/organisation-with-persons/${complianceSchemeId}`,
            ),
            { persons: addScenarioSubmitter(complianceSchemePeople) },
        ),
    ),
);

await writeFile(
    join(outputDirectory, "govuk-notify-send-email.json"),
    json(
        mapping(
            {
                Path: {
                    Matchers: [
                        {
                            Name: "ExactMatcher",
                            Pattern: "/v2/notifications/email",
                        },
                    ],
                },
                Methods: ["POST"],
            },
            { id: "journey-test-notification" },
        ),
    ),
);

console.log("Generated Waste Obligations WireMock mappings");
