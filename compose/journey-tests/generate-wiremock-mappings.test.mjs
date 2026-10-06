import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";
import { promisify } from "node:util";

const execFileAsync = promisify(execFile);
const generatorPath = fileURLToPath(
    new URL("./generate-wiremock-mappings.mjs", import.meta.url),
);
const directProducerId = "b4a566a0-3a79-45a3-b43f-2ce900124750";
const complianceSchemeId = "5165d0cb-fb61-4fd4-8b5e-75fa0e99a323";
const submitterId = "2c0bb58b-91df-423d-9353-a87ef0d3c89e";
const submitterEmail = "journey-test@example.com";
const mappingsDirectoryPrefix = "waste-obligations-wiremock-mappings-";

const runGenerator = (outputDirectory, environment) =>
    execFileAsync("node", [generatorPath], {
        env: { ...process.env, JOURNEY_TEST_WIREMOCK_MAPPINGS_DIR: outputDirectory, ...environment },
    });

const readMapping = async (outputDirectory, name) =>
    JSON.parse(await readFile(join(outputDirectory, name), "utf8"));

test("generates Account, Notify and PRN mappings from the supplied scenario", async (context) => {
    const outputDirectory = await mkdtemp(join(tmpdir(), mappingsDirectoryPrefix));
    context.after(() => rm(outputDirectory, { force: true, recursive: true }));

    await runGenerator(outputDirectory, {
        WASTE_OBLIGATION_ORG_ID: directProducerId,
        WASTE_OBLIGATION_CSO_ORG_ID: complianceSchemeId,
        WASTE_OBLIGATION_SUBMITTER_ID: submitterId,
        WASTE_OBLIGATION_SUBMITTER_EMAIL: submitterEmail,
    });

    const directProducerMapping = await readMapping(
        outputDirectory,
        "backend-account-organisation-with-persons-direct-producer.json",
    );
    const complianceSchemeMapping = await readMapping(
        outputDirectory,
        "backend-account-organisation-with-persons-compliance-scheme.json",
    );
    const notifyMapping = await readMapping(outputDirectory, "govuk-notify-send-email.json");
    const prnMapping = await readMapping(outputDirectory, "journey-producer-prns.json");

    const organisationsByExternalIdsMapping = await readMapping(
        outputDirectory,
        "backend-account-organisations-by-externalIds.json",
    );
    assert.deepEqual(organisationsByExternalIdsMapping, {
        Request: {
            Path: {
                Matchers: [
                    {
                        Name: "ExactMatcher",
                        Pattern: "/api/organisations/organisations-by-externalIds",
                    },
                ],
            },
            Methods: ["POST"],
            Body: {
                Matcher: {
                    Name: "JsonPathMatcher",
                    Pattern: `$.externalIds[?(@ == '${directProducerId}')]`,
                },
            },
        },
        Response: {
            StatusCode: 200,
            BodyAsJson: {
                organisations: [
                    {
                        externalId: directProducerId,
                        referenceNumber: "100001",
                        isComplianceScheme: false,
                    },
                ],
                notFoundExternalIds: [],
            },
            Headers: { "Content-Type": "application/json; charset=utf-8" },
        },
    });

    const obligationCalculationMapping = await readMapping(
        outputDirectory,
        "journey-producer-obligation-calculation.json",
    );
    assert.deepEqual(obligationCalculationMapping, {
        Request: {
            Path: {
                Matchers: [
                    {
                        Name: "RegexMatcher",
                        Pattern: "^/api/v1/prn/obligationcalculation/[0-9]{4}$",
                    },
                ],
            },
            Methods: ["GET"],
            Headers: [
                {
                    Name: "X-EPR-ORGANISATION",
                    Matchers: [{ Name: "ExactMatcher", Pattern: directProducerId }],
                },
            ],
        },
        Response: {
            StatusCode: 200,
            BodyAsJson: {
                numberOfPrnsAwaitingAcceptance: 8,
                obligationData: [
                    {
                        organisationId: directProducerId,
                        materialName: "Aluminium",
                        tonnage: 100,
                        materialTarget: 0.75,
                        obligationToMeet: 75,
                        tonnageAwaitingAcceptance: 215,
                        tonnageAccepted: 80,
                        tonnageOutstanding: 0,
                        status: "Met",
                    },
                    {
                        organisationId: directProducerId,
                        materialName: "Wood",
                        tonnage: 200,
                        materialTarget: 0.35,
                        obligationToMeet: null,
                        tonnageAwaitingAcceptance: 0,
                        tonnageAccepted: 0,
                        tonnageOutstanding: null,
                        status: "NoDataYet",
                    },
                ],
            },
            Headers: { "Content-Type": "application/json; charset=utf-8" },
        },
    });

    const obligationCalculationPath = new RegExp(
        obligationCalculationMapping.Request.Path.Matchers[0].Pattern,
    );
    assert.equal(obligationCalculationPath.test("/api/v1/prn/obligationcalculation/2026"), true);
    assert.equal(obligationCalculationPath.test("/api/v1/prn/obligationcalculation/2027"), true);
    assert.equal(obligationCalculationPath.test("/api/v1/prn/obligationcalculation/not-a-year"), false);
    assert.equal(obligationCalculationPath.test("/api/v1/prn/obligationcalculation/20270"), false);
    assert.equal(obligationCalculationPath.test("/api/v1/prn/search/2027"), false);
    assert.equal(obligationCalculationPath.test("/api/v1/prn/obligationcalculation/2027/extra"), false);

    assert.deepEqual(prnMapping.Request, {
        Path: {
            Matchers: [{ Name: "ExactMatcher", Pattern: "/api/v1/prn/search" }],
        },
        Methods: ["GET"],
        Headers: [
            {
                Name: "X-EPR-ORGANISATION",
                Matchers: [{ Name: "ExactMatcher", Pattern: directProducerId }],
            },
        ],
    });
    assert.equal(prnMapping.Priority, 10);
    assert.equal(prnMapping.Response.StatusCode, 200);
    const { items, totalItems } = prnMapping.Response.BodyAsJson;
    assert.equal(totalItems, 8);
    assert.deepEqual(
        items.map((item) => item.prnNumber),
        ["PRN123", "PRN124", "PRN125", "PRN126", "PRN127", "PRN128", "PRN129", "PRN130"],
    );
    for (const item of items) {
        assert.equal(item.organisationId, directProducerId);
        assert.equal(item.prnStatus, "AWAITINGACCEPTANCE");
        assert.equal(item.issuedByOrg, "Journey Reprocessors Ltd");
    }
    assert.equal(items[0].materialName, "Aluminium");
    assert.equal(items[0].tonnageValue, 125);

    const singlePrnMapping = await readMapping(outputDirectory, "journey-producer-prn.json");
    assert.deepEqual(singlePrnMapping.Request, {
        Path: {
            Matchers: [{ Name: "ExactMatcher", Pattern: `/api/v1/prn/${items[0].externalId}` }],
        },
        Methods: ["GET"],
        Headers: prnMapping.Request.Headers,
    });
    assert.equal(singlePrnMapping.Response.StatusCode, 200);
    assert.deepEqual(singlePrnMapping.Response.BodyAsJson, items[0]);

    const csoPrnsMapping = await readMapping(outputDirectory, "journey-compliance-scheme-prns.json");
    assert.deepEqual(csoPrnsMapping.Request.Headers, [
        {
            Name: "X-EPR-ORGANISATION",
            Matchers: [{ Name: "ExactMatcher", Pattern: complianceSchemeId }],
        },
    ]);
    const [csoPrn] = csoPrnsMapping.Response.BodyAsJson.items;
    assert.equal(csoPrnsMapping.Response.BodyAsJson.totalItems, 1);
    assert.equal(csoPrn.organisationId, complianceSchemeId);
    assert.equal(csoPrn.prnNumber, "PRN456");
    assert.equal(csoPrn.prnStatus, "AWAITINGACCEPTANCE");
    assert.notEqual(csoPrn.externalId, items[0].externalId);

    const csoSinglePrnMapping = await readMapping(outputDirectory, "journey-compliance-scheme-prn.json");
    assert.equal(
        csoSinglePrnMapping.Request.Path.Matchers[0].Pattern,
        `/api/v1/prn/${csoPrn.externalId}`,
    );
    assert.deepEqual(csoSinglePrnMapping.Request.Headers, csoPrnsMapping.Request.Headers);
    assert.deepEqual(csoSinglePrnMapping.Response.BodyAsJson, csoPrn);

    assert.equal(
        directProducerMapping.Request.Path.Matchers[0].Pattern,
        `/api/organisations/organisation-with-persons/${directProducerId}`,
    );
    assert.deepEqual(directProducerMapping.Response.BodyAsJson.persons.at(-1), {
        userId: submitterId,
        firstName: "Journey-test",
        lastName: "Submitter",
        email: submitterEmail,
        serviceRole: "Delegated Person",
    });
    assert.equal(
        complianceSchemeMapping.Request.Path.Matchers[0].Pattern,
        `/api/organisations/organisation-with-persons/${complianceSchemeId}`,
    );
    assert.deepEqual(complianceSchemeMapping.Response.BodyAsJson.persons.at(-1), {
        userId: submitterId,
        firstName: "Journey-test",
        lastName: "Submitter",
        email: submitterEmail,
        serviceRole: "Delegated Person",
    });
    assert.deepEqual(notifyMapping, {
        Request: {
            Path: {
                Matchers: [
                    { Name: "ExactMatcher", Pattern: "/v2/notifications/email" },
                ],
            },
            Methods: ["POST"],
        },
        Response: {
            StatusCode: 200,
            BodyAsJson: { id: "journey-test-notification" },
            Headers: { "Content-Type": "application/json; charset=utf-8" },
        },
    });
});

test("generates PRN search mappings that filter and sort like the common backend", async (context) => {
    const outputDirectory = await mkdtemp(join(tmpdir(), mappingsDirectoryPrefix));
    context.after(() => rm(outputDirectory, { force: true, recursive: true }));

    await runGenerator(outputDirectory, {
        WASTE_OBLIGATION_ORG_ID: directProducerId,
        WASTE_OBLIGATION_CSO_ORG_ID: complianceSchemeId,
        WASTE_OBLIGATION_SUBMITTER_ID: submitterId,
        WASTE_OBLIGATION_SUBMITTER_EMAIL: submitterEmail,
    });

    const prnNumbers = (mapping) => mapping.Response.BodyAsJson.items.map((item) => item.prnNumber);
    const params = (mapping) =>
        Object.fromEntries(
            mapping.Request.Params.map((param) => [param.Name, param.Matchers[0].Pattern]),
        );

    const aluminium = await readMapping(
        outputDirectory,
        "journey-producer-prns-filter-awaiting-aluminium.json",
    );
    assert.equal(aluminium.Priority, 2);
    assert.deepEqual(params(aluminium), { filterBy: "awaiting-aluminium" });
    assert.deepEqual(prnNumbers(aluminium), ["PRN123", "PRN130"]);
    assert.equal(aluminium.Response.BodyAsJson.totalItems, 2);

    const wood = await readMapping(outputDirectory, "journey-producer-prns-filter-awaiting-wood.json");
    assert.deepEqual(wood.Response.BodyAsJson, { items: [], totalItems: 0 });

    const tonnageDescending = await readMapping(
        outputDirectory,
        "journey-producer-prns-sort-tonnage-desc.json",
    );
    assert.equal(tonnageDescending.Priority, 3);
    assert.deepEqual(prnNumbers(tonnageDescending), [
        "PRN127", "PRN125", "PRN129", "PRN123", "PRN130", "PRN126", "PRN124", "PRN128",
    ]);

    const materialAscending = await readMapping(
        outputDirectory,
        "journey-producer-prns-sort-material-asc.json",
    );
    assert.deepEqual(
        materialAscending.Response.BodyAsJson.items.map((item) => item.materialName),
        ["Aluminium", "Aluminium", "Glass Other", "Glass Re-melt", "Paper/board", "Plastic", "Plastic", "Steel"],
    );

    const plasticByOldest = await readMapping(
        outputDirectory,
        "journey-producer-prns-filter-awaiting-plastic-sort-date-issued-asc.json",
    );
    assert.equal(plasticByOldest.Priority, 1);
    assert.deepEqual(params(plasticByOldest), {
        filterBy: "awaiting-plastic",
        sortBy: "date-issued-asc",
    });
    assert.deepEqual(prnNumbers(plasticByOldest), ["PRN125", "PRN124"]);
});

test("fails when a required scenario value is missing", async (context) => {
    const outputDirectory = await mkdtemp(join(tmpdir(), mappingsDirectoryPrefix));
    context.after(() => rm(outputDirectory, { force: true, recursive: true }));

    await assert.rejects(
        runGenerator(outputDirectory, {
            WASTE_OBLIGATION_ORG_ID: directProducerId,
            WASTE_OBLIGATION_CSO_ORG_ID: complianceSchemeId,
            WASTE_OBLIGATION_SUBMITTER_ID: submitterId,
        }),
        /WASTE_OBLIGATION_SUBMITTER_EMAIL must be set/,
    );
});
