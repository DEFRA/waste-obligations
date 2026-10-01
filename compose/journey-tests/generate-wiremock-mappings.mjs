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

// Keep the Azure PRN common backend contract beside its consuming service.
// Several distinguishable PRNs let the journey assert rendered values, material
// filtering and each sort order. Issue date, tonnage and material each give a
// different order, and Wood is deliberately absent so one filter is empty.
const prnFixture = (
    prnNumber,
    externalId,
    materialName,
    tonnageValue,
    issueDate,
) => ({
    externalId,
    prnNumber,
    organisationId: directProducerId,
    organisationName: "Journey Producer Ltd",
    reprocessorExporterAgency: "Environment Agency",
    prnStatus: "AWAITINGACCEPTANCE",
    tonnageValue,
    materialName,
    issuerNotes: "Journey PRN list fixture",
    issueDate,
    processToBeUsed: "R3",
    decemberWaste: false,
    issuedByOrg: "Journey Reprocessors Ltd",
    accreditationNumber: "ACC123",
    accreditationYear: "2026",
    obligationYear: "2026",
    createdOn: issueDate,
    lastUpdatedDate: issueDate,
    isExport: false,
});

const PRN_TONNAGE_125 = 125;
const PRN_TONNAGE_40 = 40;
const PRN_TONNAGE_310 = 310;
const PRN_TONNAGE_75 = 75;
const PRN_TONNAGE_510 = 510;
const PRN_TONNAGE_15 = 15;
const PRN_TONNAGE_220 = 220;
const PRN_TONNAGE_90 = 90;

const producerPrns = [
    prnFixture(
        "PRN123",
        "0d2f531d-0213-494b-8c8b-4133051bd44f",
        "Aluminium",
        PRN_TONNAGE_125,
        "2026-06-15T10:30:00Z",
    ),
    prnFixture(
        "PRN124",
        "1f6b9a52-6a0e-4d53-9a4e-6c1f7c2d8e01",
        "Plastic",
        PRN_TONNAGE_40,
        "2026-06-10T09:00:00Z",
    ),
    prnFixture(
        "PRN125",
        "2a7c0b63-7b1f-4e64-8b5f-7d2a8d3e9f02",
        "Plastic",
        PRN_TONNAGE_310,
        "2026-05-20T11:15:00Z",
    ),
    prnFixture(
        "PRN126",
        "3b8d1c74-8c2a-4f75-9c6a-8e3b9e4fa003",
        "Glass Other",
        PRN_TONNAGE_75,
        "2026-05-02T14:45:00Z",
    ),
    prnFixture(
        "PRN127",
        "4c9e2d85-9d3b-4a86-8d7b-9f4cae5ab104",
        "Steel",
        PRN_TONNAGE_510,
        "2026-04-18T08:20:00Z",
    ),
    prnFixture(
        "PRN128",
        "5daf3e96-ae4c-4b97-9e8c-a05dbf6bc205",
        "Paper/board",
        PRN_TONNAGE_15,
        "2026-03-30T16:05:00Z",
    ),
    prnFixture(
        "PRN129",
        "6eb04fa7-bf5d-4ca8-8f9d-b16ec07cd306",
        "Glass Re-melt",
        PRN_TONNAGE_220,
        "2026-03-05T12:40:00Z",
    ),
    prnFixture(
        "PRN130",
        "7fc150b8-c06e-4db9-9a0e-c27fd18de407",
        "Aluminium",
        PRN_TONNAGE_90,
        "2026-02-12T10:10:00Z",
    ),
];

// Mirrors the common backend's filterBy and sortBy handling for awaiting PRNs.
// Without sortBy the backend orders by issue date, newest first.
const prnFilters = {
    "awaiting-all": () => true,
    "awaiting-aluminium": (prn) => prn.materialName === "Aluminium",
    "awaiting-glassother": (prn) => prn.materialName === "Glass Other",
    "awaiting-glassremelt": (prn) => prn.materialName === "Glass Re-melt",
    "awaiting-paperfiber": (prn) =>
        ["Paper/board", "Fibre"].includes(prn.materialName),
    "awaiting-plastic": (prn) => prn.materialName === "Plastic",
    "awaiting-steel": (prn) => prn.materialName === "Steel",
    "awaiting-wood": (prn) => prn.materialName === "Wood",
};
const byIssueDate = (a, b) => Date.parse(a.issueDate) - Date.parse(b.issueDate);
const byValue = (key) => (a, b) => {
    if (a[key] < b[key]) {
        return -1;
    }
    if (a[key] > b[key]) {
        return 1;
    }

    return 0;
};
const descending = (compare) => (a, b) => compare(b, a);
const prnSorts = {
    "date-issued-desc": descending(byIssueDate),
    "date-issued-asc": byIssueDate,
    "tonnage-desc": descending(byValue("tonnageValue")),
    "tonnage-asc": byValue("tonnageValue"),
    "issued-by-desc": descending(byValue("issuedByOrg")),
    "issued-by-asc": byValue("issuedByOrg"),
    "december-waste-desc": descending(byValue("decemberWaste")),
    "material-desc": descending(byValue("materialName")),
    "material-asc": byValue("materialName"),
};
const defaultPrnSort = prnSorts["date-issued-desc"];

const searchPrns = (filter = () => true, sort = defaultPrnSort) => {
    // Break ties on issue date so repeated generation is deterministic.
    const items = producerPrns
        .filter(filter)
        .sort((a, b) => sort(a, b) || defaultPrnSort(a, b));

    return { items, totalItems: items.length };
};
const queryParam = (name, value) => ({
    Name: name,
    Matchers: [{ Name: "ExactMatcher", Pattern: value }],
});
// WireMock.Net matches the lowest Priority first, so the most specific
// parameter combination wins and an unrecognised query falls back to every
// PRN in default order.
const prnSearchMapping = (params, body, priority) => ({
    Priority: priority,
    ...mapping(
        {
            ...exactPath("/api/v1/prn/search"),
            Headers: [
                {
                    Name: "X-EPR-ORGANISATION",
                    Matchers: [
                        { Name: "ExactMatcher", Pattern: directProducerId },
                    ],
                },
            ],
            ...(params.length ? { Params: params } : {}),
        },
        body,
    ),
});

const PRN_MAPPING_1 = 1;
const PRN_MAPPING_2 = 2;
const PRN_MAPPING_3 = 3;
const PRN_MAPPING_10 = 10;

const prnMappings = [
    [
        "journey-producer-prns.json",
        prnSearchMapping([], searchPrns(), PRN_MAPPING_10),
    ],
];
for (const [sortBy, sort] of Object.entries(prnSorts)) {
    prnMappings.push([
        `journey-producer-prns-sort-${sortBy}.json`,
        prnSearchMapping(
            [queryParam("sortBy", sortBy)],
            searchPrns(undefined, sort),
            PRN_MAPPING_3,
        ),
    ]);
}
for (const [filterBy, filter] of Object.entries(prnFilters)) {
    prnMappings.push([
        `journey-producer-prns-filter-${filterBy}.json`,
        prnSearchMapping(
            [queryParam("filterBy", filterBy)],
            searchPrns(filter),
            PRN_MAPPING_2,
        ),
    ]);
    for (const [sortBy, sort] of Object.entries(prnSorts)) {
        prnMappings.push([
            `journey-producer-prns-filter-${filterBy}-sort-${sortBy}.json`,
            prnSearchMapping(
                [
                    queryParam("filterBy", filterBy),
                    queryParam("sortBy", sortBy),
                ],
                searchPrns(filter, sort),
                PRN_MAPPING_1,
            ),
        ]);
    }
}
await Promise.all(
    prnMappings.map(([name, value]) =>
        writeFile(join(outputDirectory, name), json(value)),
    ),
);

console.log("Generated Waste Obligations WireMock mappings");
