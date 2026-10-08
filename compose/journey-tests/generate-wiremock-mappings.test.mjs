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
    assert.equal(totalItems, 10);
    // Newest issue date first: the December 2026 waste PRN, then the standard
    // 2026 PRNs, then the December 2025 waste PRN.
    assert.deepEqual(
        items.map((item) => item.prnNumber),
        [
            "PRN131",
            "PRN123",
            "PRN124",
            "PRN125",
            "PRN126",
            "PRN127",
            "PRN128",
            "PRN129",
            "PRN130",
            "PRN132",
        ],
    );
    for (const item of items) {
        assert.equal(item.organisationId, directProducerId);
        assert.equal(item.prnStatus, "AWAITINGACCEPTANCE");
        assert.equal(item.issuedByOrg, "Journey Reprocessors Ltd");
    }
    const byNumber = Object.fromEntries(items.map((item) => [item.prnNumber, item]));
    assert.equal(byNumber.PRN123.materialName, "Aluminium");
    assert.equal(byNumber.PRN123.tonnageValue, 125);
    assert.equal(byNumber.PRN123.decemberWaste, false);

    // December waste (MO-479): PRN131 in the Dec 2026 window, PRN132 stale.
    assert.equal(byNumber.PRN131.decemberWaste, true);
    assert.equal(byNumber.PRN131.obligationYear, "2026");
    assert.equal(byNumber.PRN131.issueDate, "2026-12-05T09:00:00Z");
    assert.equal(byNumber.PRN132.decemberWaste, true);
    assert.equal(byNumber.PRN132.obligationYear, "2025");
    assert.equal(byNumber.PRN132.issueDate, "2025-12-10T09:00:00Z");

    const singlePrnMapping = await readMapping(outputDirectory, "journey-producer-prn.json");
    assert.deepEqual(singlePrnMapping.Request, {
        Path: {
            Matchers: [
                { Name: "ExactMatcher", Pattern: `/api/v1/prn/${byNumber.PRN123.externalId}` },
            ],
        },
        Methods: ["GET"],
        Headers: prnMapping.Request.Headers,
    });
    assert.equal(singlePrnMapping.Response.StatusCode, 200);
    assert.deepEqual(singlePrnMapping.Response.BodyAsJson, byNumber.PRN123);

    // Every other producer PRN has its own single-PRN read.
    for (const item of items.filter((prn) => prn.prnNumber !== "PRN123")) {
        const single = await readMapping(
            outputDirectory,
            `journey-producer-prn-${item.prnNumber.toLowerCase()}.json`,
        );
        assert.equal(
            single.Request.Path.Matchers[0].Pattern,
            `/api/v1/prn/${item.externalId}`,
        );
        assert.deepEqual(single.Response.BodyAsJson, item);
    }

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
        "PRN127", "PRN125", "PRN129", "PRN123", "PRN130", "PRN126", "PRN131", "PRN124", "PRN132",
        "PRN128",
    ]);

    const materialAscending = await readMapping(
        outputDirectory,
        "journey-producer-prns-sort-material-asc.json",
    );
    assert.deepEqual(
        materialAscending.Response.BodyAsJson.items.map((item) => item.materialName),
        [
            "Aluminium",
            "Aluminium",
            "Glass Other",
            "Glass Re-melt",
            "Paper/board",
            "Paper/board",
            "Plastic",
            "Plastic",
            "Plastic",
            "Steel",
        ],
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
    assert.deepEqual(prnNumbers(plasticByOldest), ["PRN125", "PRN124", "PRN131"]);
});

test("generates accepted PRN mappings for the accepted confirmation view", async (context) => {
    const outputDirectory = await mkdtemp(join(tmpdir(), mappingsDirectoryPrefix));
    context.after(() => rm(outputDirectory, { force: true, recursive: true }));

    await runGenerator(outputDirectory, {
        WASTE_OBLIGATION_ORG_ID: directProducerId,
        WASTE_OBLIGATION_CSO_ORG_ID: complianceSchemeId,
        WASTE_OBLIGATION_SUBMITTER_ID: submitterId,
        WASTE_OBLIGATION_SUBMITTER_EMAIL: submitterEmail,
    });

    const acceptedParams = [
        { Name: "filterBy", Matchers: [{ Name: "ExactMatcher", Pattern: "accepted-all" }] },
    ];

    for (const [account, organisationId, number, isExport] of [
        ["producer", directProducerId, "PRN131", false],
        ["compliance-scheme", complianceSchemeId, "PERN457", true],
    ]) {
        const search = await readMapping(
            outputDirectory,
            `journey-${account}-prns-filter-accepted-all.json`,
        );
        assert.equal(search.Priority, 2);
        assert.deepEqual(search.Request.Params, acceptedParams);
        assert.equal(search.Request.Headers[0].Matchers[0].Pattern, organisationId);
        const [prn] = search.Response.BodyAsJson.items;
        assert.equal(search.Response.BodyAsJson.totalItems, 1);
        assert.equal(prn.prnNumber, number);
        assert.equal(prn.prnStatus, "ACCEPTED");
        assert.equal(prn.organisationId, organisationId);
        assert.equal(prn.isExport, isExport);

        const single = await readMapping(outputDirectory, `journey-${account}-accepted-prn.json`);
        assert.equal(single.Request.Path.Matchers[0].Pattern, `/api/v1/prn/${prn.externalId}`);
        assert.deepEqual(single.Request.Headers, search.Request.Headers);
        assert.deepEqual(single.Response.BodyAsJson, prn);
    }

    // The unfiltered CSO search stays the awaiting list, below the accepted filter.
    const csoAwaiting = await readMapping(outputDirectory, "journey-compliance-scheme-prns.json");
    assert.equal(csoAwaiting.Priority, 10);
    assert.equal(csoAwaiting.Request.Params, undefined);

    // Accepted PRNs never appear in the awaiting list or its filters.
    const awaiting = await readMapping(outputDirectory, "journey-producer-prns.json");
    assert.ok(awaiting.Response.BodyAsJson.items.every((item) => item.prnStatus === "AWAITINGACCEPTANCE"));
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
