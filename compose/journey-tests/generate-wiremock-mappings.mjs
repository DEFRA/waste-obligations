import { mkdir, writeFile } from "node:fs/promises";
import { join } from "node:path";
import {
    complianceSchemeId,
    directProducerId,
} from "../helpers/journey-config.mjs";
import {
    addScenarioSubmitter,
    complianceSchemePeople,
    directProducerPeople,
} from "../helpers/journey-people.mjs";
import {
    exactPath,
    json,
    mapping,
} from "../helpers/mapping-helpers.mjs";
import { prnMappings } from "../helpers/prn-mappings.mjs";
import { obligationMappings } from "../helpers/obligation-mappings.mjs";

const outputDirectory =
    process.env.JOURNEY_TEST_WIREMOCK_MAPPINGS_DIR || "/output";
await mkdir(outputDirectory, { recursive: true });

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

await Promise.all(
    [...prnMappings, ...obligationMappings].map(([name, value]) =>
        writeFile(join(outputDirectory, name), json(value)),
    ),
);

console.log("Generated Waste Obligations WireMock mappings");
