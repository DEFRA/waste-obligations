// WireMock mappings for obligation calculations and accept status updates.

import {
    complianceSchemeId,
    directProducerId,
} from "./journey-config.mjs";
import {
    PRN_MATERIAL_GLASS_OTHER,
    PRN_MATERIAL_GLASS_REMELT,
    PRN_MATERIAL_PAPER_BOARD,
    PRN_OBLIGATION_YEAR,
    acceptedCompliancePrn,
    acceptedProducerPrn,
    complianceAwaitingSearchItems,
    producerPrns,
} from "./journey-prns.mjs";
import {
    PRN_MAPPING_1,
    PRN_MAPPING_2,
    exactPath,
    mapping,
    organisationHeader,
} from "./mapping-helpers.mjs";

export const obligationMappings = [];

// The accept-and-obligations journey accepts the producer's one paper/board
// PRN (PRN128). Its status update moves the scenario into the state in which
// the obligation calculation counts that PRN as accepted, so Paper's accepted
// tonnage rises by its tonnage. Before any accept, the calculation reports
// every listed PRN as awaiting acceptance. The status-update stub matches any
// status POST from the producer organisation and does not read the request
// body, so it does not check which PRN is named. Any status update for this
// organisation therefore moves the scenario, and PRN128 is counted as accepted
// whichever PRN the journey actually accepts.
export const BULK_ACCEPT_SCENARIO = "journey-producer-bulk-accept";
export const BULK_ACCEPTED_STATE = "PaperPrnAccepted";
export const JOURNEY_ACCEPTED_PRN_NUMBER = "PRN128";
// The compliance scheme's accept journey, on its own scenario: it accepts its
// paper/board PRN (PRN459), which is its only one in the fixture.
export const COMPLIANCE_ACCEPT_SCENARIO = "journey-compliance-scheme-prn-accept";
export const COMPLIANCE_ACCEPTED_STATE = "PaperPrnAccepted";
export const COMPLIANCE_JOURNEY_PRN_NUMBER = "PRN459";

export const obligationMaterial = (materialName) =>
    ({
        [PRN_MATERIAL_PAPER_BOARD]: "Paper",
        Fibre: "Paper",
        [PRN_MATERIAL_GLASS_OTHER]: "Glass",
        [PRN_MATERIAL_GLASS_REMELT]: "GlassRemelt",
    })[materialName] ?? materialName;

export const OBLIGATIONS_TO_MEET = {
    Paper: 400,
    Plastic: 600,
    Wood: 100,
    Steel: 700,
    Aluminium: 300,
    Glass: 200,
    GlassRemelt: 500,
};
export const MATERIAL_TARGET = 0.5;

export const sumTonnage = (prns, material) =>
    prns
        .filter((prn) => obligationMaterial(prn.materialName) === material)
        .reduce((total, prn) => total + prn.tonnageValue, 0);

// An organisation's obligation calculation: `listed` are its awaiting PRNs and
// `accepted` its already accepted ones. When `journeyAccepted`, the journey's
// PRN (`journeyPrnNumber`) counts as accepted rather than awaiting.
export const obligationsFor = ({
    organisationId,
    listed,
    accepted: alreadyAccepted,
    journeyPrnNumber,
    journeyAccepted,
}) => {
    const isAccepted = (prn) =>
        journeyAccepted && prn.prnNumber === journeyPrnNumber;
    const awaiting = listed.filter((prn) => !isAccepted(prn));
    const accepted = [...alreadyAccepted, ...listed.filter(isAccepted)];

    return {
        numberOfPrnsAwaitingAcceptance: awaiting.length,
        obligationData: Object.entries(OBLIGATIONS_TO_MEET).map(
            ([materialName, obligationToMeet]) => {
                const tonnageAccepted = sumTonnage(accepted, materialName);

                return {
                    organisationId,
                    materialName,
                    tonnage: obligationToMeet / MATERIAL_TARGET,
                    materialTarget: MATERIAL_TARGET,
                    obligationToMeet,
                    tonnageAwaitingAcceptance: sumTonnage(awaiting, materialName),
                    tonnageAccepted,
                    tonnageOutstanding: Math.max(
                        obligationToMeet - tonnageAccepted,
                        0,
                    ),
                    status:
                        tonnageAccepted >= obligationToMeet ? "Met" : "NotMet",
                };
            },
        ),
    };
};

// The obligation calculation for one organisation, in one scenario state (no
// state: the scenario's starting state).
// WireMock matches the lowest Priority first. The state-specific calculation must
// win over the starting-state one, or a read after the accept can match either.
export const obligationCalculationMapping = (scenario, organisationId, state, obligations) => ({
    Priority: state ? PRN_MAPPING_1 : PRN_MAPPING_2,
    Scenario: scenario,
    ...(state ? { WhenStateIs: state } : {}),
    ...mapping(
        {
            ...exactPath(
                `/api/v1/prn/obligationcalculation/${PRN_OBLIGATION_YEAR}`,
            ),
            Headers: organisationHeader(organisationId),
        },
        obligations,
    ),
});

// The status update for one organisation's accept, moving its scenario to
// `state`.
export const statusUpdateMapping = (scenario, organisationId, state) => ({
    Priority: PRN_MAPPING_1,
    Scenario: scenario,
    SetStateTo: state,
    Request: {
        Path: {
            Matchers: [{ Name: "ExactMatcher", Pattern: "/api/v1/prn/status" }],
        },
        Methods: ["POST"],
        Headers: organisationHeader(organisationId),
    },
    Response: { StatusCode: 200 },
});

export const producerJourneyObligations = (journeyAccepted) =>
    obligationsFor({
        organisationId: directProducerId,
        listed: producerPrns,
        accepted: [acceptedProducerPrn],
        journeyPrnNumber: JOURNEY_ACCEPTED_PRN_NUMBER,
        journeyAccepted,
    });

export const complianceJourneyObligations = (journeyAccepted) =>
    obligationsFor({
        organisationId: complianceSchemeId,
        listed: complianceAwaitingSearchItems,
        accepted: [acceptedCompliancePrn],
        journeyPrnNumber: COMPLIANCE_JOURNEY_PRN_NUMBER,
        journeyAccepted,
    });

obligationMappings.push(
    [
        "journey-producer-obligations.json",
        obligationCalculationMapping(
            BULK_ACCEPT_SCENARIO,
            directProducerId,
            undefined,
            producerJourneyObligations(false),
        ),
    ],
    [
        "journey-producer-obligations-bulk-accepted.json",
        obligationCalculationMapping(
            BULK_ACCEPT_SCENARIO,
            directProducerId,
            BULK_ACCEPTED_STATE,
            producerJourneyObligations(true),
        ),
    ],
    [
        "journey-producer-prn-status-update.json",
        statusUpdateMapping(
            BULK_ACCEPT_SCENARIO,
            directProducerId,
            BULK_ACCEPTED_STATE,
        ),
    ],
    [
        "journey-compliance-scheme-obligations.json",
        obligationCalculationMapping(
            COMPLIANCE_ACCEPT_SCENARIO,
            complianceSchemeId,
            undefined,
            complianceJourneyObligations(false),
        ),
    ],
    [
        "journey-compliance-scheme-obligations-accepted.json",
        obligationCalculationMapping(
            COMPLIANCE_ACCEPT_SCENARIO,
            complianceSchemeId,
            COMPLIANCE_ACCEPTED_STATE,
            complianceJourneyObligations(true),
        ),
    ],
    [
        "journey-compliance-scheme-prn-status-update.json",
        statusUpdateMapping(
            COMPLIANCE_ACCEPT_SCENARIO,
            complianceSchemeId,
            COMPLIANCE_ACCEPTED_STATE,
        ),
    ],
);
