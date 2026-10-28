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
    acceptedPrn,
    acceptedProducerPrn,
    complianceAwaitingSearchItems,
    producerPrns,
} from "./journey-prns.mjs";
import {
    PRN_MAPPING_0,
    PRN_MAPPING_1,
    PRN_MAPPING_2,
    exactPath,
    mapping,
    organisationHeader,
    queryParam,
} from "./mapping-helpers.mjs";
import {
    acceptedFilter,
    compliancePrnSearchMapping,
    prnSearchMapping,
    searchPrns,
} from "./prn-mappings.mjs";

export const obligationMappings = [];

// The multi-select accept journey (MO-471) accepts several of an
// organisation's PRNs in one status update: paper/board and fibre (one
// confirmation tile) and both glass materials for the producer; paper/board,
// fibre, glass other and an aluminium PERN for the compliance scheme. Only an
// update naming exactly these notes, each accepted towards the obligation year,
// moves the scenario into JOURNEY_ACCEPTED_STATE. In that state the obligation
// calculation counts them as accepted, and the awaiting and accepted searches
// move them from one list to the other.
export const BULK_ACCEPT_SCENARIO = "journey-producer-bulk-accept";
export const COMPLIANCE_ACCEPT_SCENARIO = "journey-compliance-scheme-prn-accept";
export const JOURNEY_ACCEPTED_STATE = "JourneyPrnsAccepted";
export const PRODUCER_JOURNEY_PRN_NUMBERS = ["PRN128", "PRN132", "PRN126", "PRN129"];
export const COMPLIANCE_JOURNEY_PRN_NUMBERS = ["PRN459", "PRN460", "PRN461", "PERN458"];

// Any other status update for the organisation (the single paper/board accept
// of earlier journey versions) moves the scenario into the state in which only
// PRN128 (PRN459 for the compliance scheme) counts as accepted. It does not
// read the request body. Remove it once no journey branch in use accepts a
// single PRN.
export const BULK_ACCEPTED_STATE = "PaperPrnAccepted";
export const JOURNEY_ACCEPTED_PRN_NUMBER = "PRN128";
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
// `accepted` its already accepted ones. The listed PRNs named in
// `journeyPrnNumbers` count as accepted rather than awaiting.
export const obligationsFor = ({
    organisationId,
    listed,
    accepted: alreadyAccepted,
    journeyPrnNumbers = [],
}) => {
    const isAccepted = (prn) => journeyPrnNumbers.includes(prn.prnNumber);
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
// WireMock.Net ends a scenario's state when a mapping in it matches without
// setting the next one, so a state-specific read sets its own state again.
export const obligationCalculationMapping = (scenario, organisationId, state, obligations) => ({
    Priority: state ? PRN_MAPPING_1 : PRN_MAPPING_2,
    Scenario: scenario,
    ...(state ? { WhenStateIs: state, SetStateTo: state } : {}),
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

// A request body matcher for a status update that accepts exactly `prns`
// towards the obligation year, in any order: one update per PRN, each naming
// "ACCEPTED" and the year, and every PRN's id among them.
export const acceptsExactly = (prns) => {
    const update = `\\{(?=[^{}]*"status":"ACCEPTED")(?=[^{}]*"obligationYear":"${PRN_OBLIGATION_YEAR}")[^{}]*\\}`;
    const ids = prns
        .map((prn) => `(?=.*"prnId":"${prn.externalId}")`)
        .join("");

    return {
        Matcher: {
            Name: "RegexMatcher",
            Pattern: `^${ids}\\[${update}(?:,${update}){${prns.length - 1}}\\]$`,
        },
    };
};

// The status update for one organisation's accept, moving its scenario to
// `state`. With `body`, only an update matching it does so, and it wins over
// the organisation's catch-all update.
export const statusUpdateMapping = (scenario, organisationId, state, body) => ({
    Priority: body ? PRN_MAPPING_1 : PRN_MAPPING_2,
    Scenario: scenario,
    SetStateTo: state,
    Request: {
        Path: {
            Matchers: [{ Name: "ExactMatcher", Pattern: "/api/v1/prn/status" }],
        },
        Methods: ["POST"],
        Headers: organisationHeader(organisationId),
        ...(body ? { Body: body } : {}),
    },
    Response: { StatusCode: 200 },
});

// A read that applies only once `scenario` is in `state`, and keeps it there
// (see obligationCalculationMapping).
export const inState = (scenario, state, value) => ({
    Scenario: scenario,
    WhenStateIs: state,
    SetStateTo: state,
    ...value,
});

const prnsNumbered = (prns, numbers) =>
    numbers.map((number) => prns.find((prn) => prn.prnNumber === number));
const notIn = (numbers) => (prn) => !numbers.includes(prn.prnNumber);

export const producerJourneyPrns = prnsNumbered(
    producerPrns,
    PRODUCER_JOURNEY_PRN_NUMBERS,
);
export const complianceJourneyPrns = prnsNumbered(
    complianceAwaitingSearchItems,
    COMPLIANCE_JOURNEY_PRN_NUMBERS,
);

export const producerJourneyObligations = (journeyPrnNumbers) =>
    obligationsFor({
        organisationId: directProducerId,
        listed: producerPrns,
        accepted: [acceptedProducerPrn],
        journeyPrnNumbers,
    });

export const complianceJourneyObligations = (journeyPrnNumbers) =>
    obligationsFor({
        organisationId: complianceSchemeId,
        listed: complianceAwaitingSearchItems,
        accepted: [acceptedCompliancePrn],
        journeyPrnNumbers,
    });

// The searches once the journey's PRNs are accepted: the awaiting search
// (every sort, and the journey's API read without one) leaves them out, and
// the accepted search adds them.
const producerAwaitingAfterAccept = searchPrns(
    notIn(PRODUCER_JOURNEY_PRN_NUMBERS),
);
const complianceAwaitingAfterAccept = complianceAwaitingSearchItems.filter(
    notIn(COMPLIANCE_JOURNEY_PRN_NUMBERS),
);
const awaitingFilter = [queryParam("filterBy", "awaiting-all")];
const acceptedSearch = (alreadyAccepted, journeyPrns) => {
    const items = [alreadyAccepted, ...journeyPrns.map(acceptedPrn)];

    return { items, totalItems: items.length };
};

obligationMappings.push(
    [
        "journey-producer-obligations.json",
        obligationCalculationMapping(
            BULK_ACCEPT_SCENARIO,
            directProducerId,
            undefined,
            producerJourneyObligations([]),
        ),
    ],
    [
        "journey-producer-obligations-bulk-accepted.json",
        obligationCalculationMapping(
            BULK_ACCEPT_SCENARIO,
            directProducerId,
            BULK_ACCEPTED_STATE,
            producerJourneyObligations([JOURNEY_ACCEPTED_PRN_NUMBER]),
        ),
    ],
    [
        "journey-producer-obligations-journey-accepted.json",
        obligationCalculationMapping(
            BULK_ACCEPT_SCENARIO,
            directProducerId,
            JOURNEY_ACCEPTED_STATE,
            producerJourneyObligations(PRODUCER_JOURNEY_PRN_NUMBERS),
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
        "journey-producer-prn-status-update-journey.json",
        statusUpdateMapping(
            BULK_ACCEPT_SCENARIO,
            directProducerId,
            JOURNEY_ACCEPTED_STATE,
            acceptsExactly(producerJourneyPrns),
        ),
    ],
    [
        "journey-producer-prns-journey-accepted-awaiting.json",
        inState(
            BULK_ACCEPT_SCENARIO,
            JOURNEY_ACCEPTED_STATE,
            prnSearchMapping(
                awaitingFilter,
                producerAwaitingAfterAccept,
                PRN_MAPPING_0,
            ),
        ),
    ],
    [
        "journey-producer-prns-journey-accepted-accepted.json",
        inState(
            BULK_ACCEPT_SCENARIO,
            JOURNEY_ACCEPTED_STATE,
            prnSearchMapping(
                acceptedFilter,
                acceptedSearch(acceptedProducerPrn, producerJourneyPrns),
                PRN_MAPPING_0,
            ),
        ),
    ],
    [
        "journey-compliance-scheme-obligations.json",
        obligationCalculationMapping(
            COMPLIANCE_ACCEPT_SCENARIO,
            complianceSchemeId,
            undefined,
            complianceJourneyObligations([]),
        ),
    ],
    [
        "journey-compliance-scheme-obligations-accepted.json",
        obligationCalculationMapping(
            COMPLIANCE_ACCEPT_SCENARIO,
            complianceSchemeId,
            COMPLIANCE_ACCEPTED_STATE,
            complianceJourneyObligations([COMPLIANCE_JOURNEY_PRN_NUMBER]),
        ),
    ],
    [
        "journey-compliance-scheme-obligations-journey-accepted.json",
        obligationCalculationMapping(
            COMPLIANCE_ACCEPT_SCENARIO,
            complianceSchemeId,
            JOURNEY_ACCEPTED_STATE,
            complianceJourneyObligations(COMPLIANCE_JOURNEY_PRN_NUMBERS),
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
    [
        "journey-compliance-scheme-prn-status-update-journey.json",
        statusUpdateMapping(
            COMPLIANCE_ACCEPT_SCENARIO,
            complianceSchemeId,
            JOURNEY_ACCEPTED_STATE,
            acceptsExactly(complianceJourneyPrns),
        ),
    ],
    [
        "journey-compliance-scheme-prns-journey-accepted-awaiting.json",
        inState(
            COMPLIANCE_ACCEPT_SCENARIO,
            JOURNEY_ACCEPTED_STATE,
            compliancePrnSearchMapping(
                awaitingFilter,
                {
                    items: complianceAwaitingAfterAccept,
                    totalItems: complianceAwaitingAfterAccept.length,
                },
                PRN_MAPPING_0,
            ),
        ),
    ],
    [
        "journey-compliance-scheme-prns-journey-accepted-accepted.json",
        inState(
            COMPLIANCE_ACCEPT_SCENARIO,
            JOURNEY_ACCEPTED_STATE,
            compliancePrnSearchMapping(
                acceptedFilter,
                acceptedSearch(acceptedCompliancePrn, complianceJourneyPrns),
                PRN_MAPPING_0,
            ),
        ),
    ],
);
