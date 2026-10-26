// WireMock mappings for PRN search, sort, filter and single-PRN reads.

import {
    complianceSchemeId,
    directProducerId,
} from "./journey-config.mjs";
import {
    PRN_MATERIAL_ALUMINIUM,
    PRN_MATERIAL_FIBRE,
    PRN_MATERIAL_GLASS_OTHER,
    PRN_MATERIAL_GLASS_REMELT,
    PRN_MATERIAL_PAPER_BOARD,
    PRN_MATERIAL_PLASTIC,
    PRN_MATERIAL_STEEL,
    PRN_MATERIAL_WOOD,
    acceptedCompliancePrn,
    acceptedProducerPrn,
    awaitingCompliancePern,
    complianceAwaitingPrns,
    complianceAwaitingSearchItems,
    compliancePrn,
    producerPrns,
    rejectedCompliancePrn,
    rejectedProducerPrn,
} from "./journey-prns.mjs";
import {
    PRN_MAPPING_1,
    PRN_MAPPING_10,
    PRN_MAPPING_2,
    PRN_MAPPING_3,
    exactPath,
    mapping,
    organisationHeader,
    queryParam,
} from "./mapping-helpers.mjs";

// WireMock.Net matches the lowest Priority first, so the most specific
// parameter combination wins and an unrecognised query falls back to every
// PRN in default order.
export const prnSearchMapping = (params, body, priority) => ({
    Priority: priority,
    ...mapping(
        {
            ...exactPath("/api/v1/prn/search"),
            Headers: organisationHeader(directProducerId),
            ...(params.length ? { Params: params } : {}),
        },
        body,
    ),
});

export const byIssueDate = (a, b) => Date.parse(a.issueDate) - Date.parse(b.issueDate);
export const byValue = (key) => (a, b) => {
    if (a[key] < b[key]) {
        return -1;
    }
    if (a[key] > b[key]) {
        return 1;
    }

    return 0;
};
export const descending = (compare) => (a, b) => compare(b, a);
export const prnSorts = {
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

export const defaultPrnSort = prnSorts["date-issued-desc"];

export const searchPrns = (filter = () => true, sort = defaultPrnSort) => {
    // Break ties on issue date so repeated generation is deterministic.
    const items = producerPrns
        .filter(filter)
        .sort((a, b) => sort(a, b) || defaultPrnSort(a, b));

    return { items, totalItems: items.length };
};

export const prnMappings = [
    [
        "journey-producer-prns.json",
        prnSearchMapping([], searchPrns(), PRN_MAPPING_10),
    ],
];

// Mirrors the common backend's filterBy and sortBy handling for awaiting PRNs.
// Without sortBy the backend orders by issue date, newest first.
export const prnFilters = {
    "awaiting-all": () => true,
    "awaiting-aluminium": (prn) => prn.materialName === PRN_MATERIAL_ALUMINIUM,
    "awaiting-glassother": (prn) => prn.materialName === PRN_MATERIAL_GLASS_OTHER,
    "awaiting-glassremelt": (prn) => prn.materialName === PRN_MATERIAL_GLASS_REMELT,
    "awaiting-paperfiber": (prn) =>
        [PRN_MATERIAL_PAPER_BOARD, PRN_MATERIAL_FIBRE].includes(prn.materialName),
    "awaiting-plastic": (prn) => prn.materialName === PRN_MATERIAL_PLASTIC,
    "awaiting-steel": (prn) => prn.materialName === PRN_MATERIAL_STEEL,
    "awaiting-wood": (prn) => prn.materialName === PRN_MATERIAL_WOOD,
};


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
// The journey opens a listed PRN, so its single-PRN read returns the same
// record as the search.
export const singlePrnMapping = (prn) =>
    mapping(
        {
            ...exactPath(`/api/v1/prn/${prn.externalId}`),
            Headers: organisationHeader(prn.organisationId),
        },
        prn,
    );

export const acceptedFilter = [queryParam("filterBy", "accepted-all")];
export const rejectedFilter = [queryParam("filterBy", "rejected-all")];

export const compliancePrnSearchMapping = (params, body, priority) => ({
    Priority: priority,
    ...mapping(
        {
            ...exactPath("/api/v1/prn/search"),
            Headers: organisationHeader(complianceSchemeId),
            ...(params.length ? { Params: params } : {}),
        },
        body,
    ),
});

// Every listed PRN answers a single-PRN read: the bulk accept checks each one
// is still awaiting acceptance before updating it.
prnMappings.push(
    ["journey-producer-prn.json", singlePrnMapping(producerPrns[0])],
    ...producerPrns
        .slice(1)
        .map((prn) => [
            `journey-producer-prn-${prn.prnNumber}.json`,
            singlePrnMapping(prn),
        ]),
    [
        "journey-producer-prns-filter-accepted-all.json",
        prnSearchMapping(
            acceptedFilter,
            { items: [acceptedProducerPrn], totalItems: 1 },
            PRN_MAPPING_2,
        ),
    ],
    [
        "journey-producer-accepted-prn.json",
        singlePrnMapping(acceptedProducerPrn),
    ],
    [
        "journey-producer-prns-filter-rejected-all.json",
        prnSearchMapping(
            rejectedFilter,
            { items: [rejectedProducerPrn], totalItems: 1 },
            PRN_MAPPING_2,
        ),
    ],
    [
        "journey-producer-rejected-prn.json",
        singlePrnMapping(rejectedProducerPrn),
    ],
    [
        "journey-compliance-scheme-prns.json",
        compliancePrnSearchMapping(
            [],
            {
                items: complianceAwaitingSearchItems,
                totalItems: complianceAwaitingSearchItems.length,
            },
            PRN_MAPPING_10,
        ),
    ],
    [
        "journey-compliance-scheme-prns-filter-accepted-all.json",
        compliancePrnSearchMapping(
            acceptedFilter,
            { items: [acceptedCompliancePrn], totalItems: 1 },
            PRN_MAPPING_2,
        ),
    ],
    ["journey-compliance-scheme-prn.json", singlePrnMapping(compliancePrn)],
    [
        "journey-compliance-scheme-awaiting-pern.json",
        singlePrnMapping(awaitingCompliancePern),
    ],
    [
        "journey-compliance-scheme-accepted-prn.json",
        singlePrnMapping(acceptedCompliancePrn),
    ],
    [
        "journey-compliance-scheme-prns-filter-rejected-all.json",
        compliancePrnSearchMapping(
            rejectedFilter,
            { items: [rejectedCompliancePrn], totalItems: 1 },
            PRN_MAPPING_2,
        ),
    ],
    [
        "journey-compliance-scheme-rejected-prn.json",
        singlePrnMapping(rejectedCompliancePrn),
    ],
);

// Single-PRN reads for the compliance scheme's awaiting PRNs: a selected PRN is
// read again before it is accepted.
for (const prn of complianceAwaitingPrns.slice(1)) {
    prnMappings.push([
        `journey-compliance-scheme-prn-${prn.prnNumber}.json`,
        singlePrnMapping(prn),
    ]);
}
