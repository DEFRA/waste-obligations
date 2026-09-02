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

test("generates the Account and GOV.UK Notify mappings from the supplied scenario", async (context) => {
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
