// PRN fixtures for the producer and compliance scheme journeys.

import {
    complianceSchemeId,
    directProducerId,
} from "./journey-config.mjs";

// Keep the Azure PRN common backend contract beside its consuming service.

export const PRN_TONNAGE_125 = 125;
export const PRN_TONNAGE_40 = 40;
export const PRN_TONNAGE_310 = 310;
export const PRN_TONNAGE_75 = 75;
export const PRN_TONNAGE_510 = 510;
export const PRN_TONNAGE_15 = 15;
export const PRN_TONNAGE_220 = 220;
export const PRN_TONNAGE_90 = 90;
export const PRN_TONNAGE_1 = 1;
export const PRN_TONNAGE_60 = 60;
export const PRN_TONNAGE_35 = 35;
export const PRN_OBLIGATION_YEAR = "2026";

// Several distinguishable PRNs let the journey assert rendered values, material
// filtering and each sort order. Issue date, tonnage and material each give a
// different order, and Wood is deliberately absent so one filter is empty.
export const prnFixture = (
    prnNumber,
    externalId,
    materialName,
    tonnageValue,
    issueDate,
) => ({
    externalId,
    prnNumber,
    organisationId: directProducerId,
    organisationName: journeyPrnOrganisationName,
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
    obligationYear: PRN_OBLIGATION_YEAR,
    createdOn: issueDate,
    lastUpdatedDate: issueDate,
    isExport: false,
});

export const journeyPrnExternalId = "0d2f531d-0213-494b-8c8b-4133051bd44f";
export const journeyPrnOrganisationName = "Journey Producer Ltd";

export const PRN_MATERIAL_GLASS_OTHER = "Glass Other"
export const PRN_MATERIAL_GLASS_REMELT = "Glass Re-melt"
export const PRN_MATERIAL_PAPER_BOARD = "Paper/board";
export const PRN_MATERIAL_FIBRE = "Fibre"
export const PRN_MATERIAL_ALUMINIUM = "Aluminium"
export const PRN_MATERIAL_PLASTIC = "Plastic"
export const PRN_MATERIAL_STEEL = "Steel"
export const PRN_MATERIAL_WOOD = "Wood"

export const producerPrns = [
    prnFixture(
        "PRN123",
        journeyPrnExternalId,
        PRN_MATERIAL_ALUMINIUM,
        PRN_TONNAGE_125,
        "2026-06-15T10:30:00Z",
    ),
    prnFixture(
        "PRN124",
        "1f6b9a52-6a0e-4d53-9a4e-6c1f7c2d8e01",
        PRN_MATERIAL_PLASTIC,
        PRN_TONNAGE_40,
        "2026-06-10T09:00:00Z",
    ),
    prnFixture(
        "PRN125",
        "2a7c0b63-7b1f-4e64-8b5f-7d2a8d3e9f02",
        PRN_MATERIAL_PLASTIC,
        PRN_TONNAGE_310,
        "2026-05-20T11:15:00Z",
    ),
    prnFixture(
        "PRN126",
        "3b8d1c74-8c2a-4f75-9c6a-8e3b9e4fa003",
        PRN_MATERIAL_GLASS_OTHER,
        PRN_TONNAGE_75,
        "2026-05-02T14:45:00Z",
    ),
    prnFixture(
        "PRN127",
        "4c9e2d85-9d3b-4a86-8d7b-9f4cae5ab104",
        PRN_MATERIAL_STEEL,
        PRN_TONNAGE_510,
        "2026-04-18T08:20:00Z",
    ),
    prnFixture(
        "PRN128",
        "5daf3e96-ae4c-4b97-9e8c-a05dbf6bc205",
        PRN_MATERIAL_PAPER_BOARD,
        PRN_TONNAGE_15,
        "2026-03-30T16:05:00Z",
    ),
    prnFixture(
        "PRN129",
        "6eb04fa7-bf5d-4ca8-8f9d-b16ec07cd306",
        PRN_MATERIAL_GLASS_REMELT,
        PRN_TONNAGE_220,
        "2026-03-05T12:40:00Z",
    ),
    prnFixture(
        "PRN130",
        "7fc150b8-c06e-4db9-9a0e-c27fd18de407",
        PRN_MATERIAL_ALUMINIUM,
        PRN_TONNAGE_90,
        "2026-02-12T10:10:00Z",
    ),
    prnFixture(
        "PRN132",
        "8ad261c9-d1a0-4e1a-8b1f-d38fe29fe508",
        PRN_MATERIAL_FIBRE,
        PRN_TONNAGE_35,
        "2026-04-02T13:00:00Z",
    ),
];

export const compliancePrn = {
    ...prnFixture(
        "PRN456",
        "6a1e3c2b-7f4d-4e8a-9b5c-2d7f1a0e4c93",
        PRN_MATERIAL_ALUMINIUM,
        PRN_TONNAGE_125,
        "2026-06-15T10:30:00Z",
    ),
    organisationId: complianceSchemeId,
    organisationName: "Journey Compliance Scheme Ltd",
};

export const compliancePrnFixture = (...args) => ({
    ...prnFixture(...args),
    organisationId: complianceSchemeId,
    organisationName: compliancePrn.organisationName,
});

// The CSO check-selection journey selects one paper, fibre, glass other and
// glass re-melt PRN. They are older than PRN456, which stays first.
export const complianceAwaitingPrns = [
    compliancePrn,
    compliancePrnFixture(
        "PRN459",
        "9b3d6e1a-4c2f-4d8b-a7e5-1f0c2b3a4d59",
        PRN_MATERIAL_PAPER_BOARD,
        PRN_TONNAGE_15,
        "2026-05-11T09:00:00Z",
    ),
    compliancePrnFixture(
        "PRN460",
        "a4c7e2f9-5d3b-4e1a-b8f6-2a1d3c4b5e60",
        PRN_MATERIAL_FIBRE,
        PRN_TONNAGE_35,
        "2026-04-22T11:30:00Z",
    ),
    compliancePrnFixture(
        "PRN461",
        "b5d8f3a0-6e4c-4f2b-89a7-3b2e4d5c6f61",
        PRN_MATERIAL_GLASS_OTHER,
        PRN_TONNAGE_75,
        "2026-04-09T14:15:00Z",
    ),
    compliancePrnFixture(
        "PRN462",
        "c6e9a4b1-7f5d-4a3c-9ab8-4c3f5e6d7a62",
        PRN_MATERIAL_GLASS_REMELT,
        PRN_TONNAGE_220,
        "2026-03-18T08:45:00Z",
    ),
];

// The journey opens an accepted PRN to check the accepted confirmation view.
// Accepted PRNs only answer the accepted-all filter, so the awaiting lists and
// their sorts are unchanged. The CSO one is a PERN to cover that wording.
export const acceptedPrn = (prn) => ({ ...prn, prnStatus: "ACCEPTED" });

export const acceptedProducerPrn = acceptedPrn(
    prnFixture(
        "PRN131",
        "0f2e14dd-6969-4311-90c5-e1fa89f9c85d",
        PRN_MATERIAL_PAPER_BOARD,
        PRN_TONNAGE_1,
        "2026-02-05T09:30:00Z",
    ),
);

export const acceptedCompliancePrn = acceptedPrn({
    ...prnFixture(
        "PERN457",
        "45edaa0b-cb34-4aec-a66c-9e8230c5df22",
        PRN_MATERIAL_PLASTIC,
        PRN_TONNAGE_60,
        "2026-02-05T09:30:00Z",
    ),
    organisationId: complianceSchemeId,
    organisationName: compliancePrn.organisationName,
    isExport: true,
});

// The PDF journey opens a rejected note and expects the download inside the
// green banner. These stay off the awaiting lists.
export const rejectedPrn = (prn) => ({ ...prn, prnStatus: "REJECTED" });

export const rejectedProducerPrn = rejectedPrn(
    prnFixture(
        "PRN133",
        "8ad261c9-d17f-4ec0-ab1f-d380e29ef518",
        "Steel",
        PRN_TONNAGE_40,
        "2026-01-20T09:30:00Z",
    ),
);

// The CSO awaiting list starts as a PRN. The PDF journey also needs a PERN.
export const awaitingCompliancePern = {
    ...prnFixture(
        "PERN458",
        "9be372da-e280-4fd1-bc20-e491f30af629",
        "Aluminium",
        PRN_TONNAGE_15,
        "2026-04-01T09:30:00Z",
    ),
    organisationId: complianceSchemeId,
    organisationName: compliancePrn.organisationName,
    isExport: true,
};

// Everything the CSO awaiting search returns, in the order the journeys rely on.
// The obligation calculation counts the same PRNs, so both read this list.
export const complianceAwaitingSearchItems = [
    compliancePrn,
    awaitingCompliancePern,
    ...complianceAwaitingPrns.slice(1),
];

export const rejectedCompliancePrn = rejectedPrn({
    ...prnFixture(
        "PERN459",
        "acf483eb-f391-40e2-8d31-f5a2041b073a",
        "Plastic",
        PRN_TONNAGE_75,
        "2026-01-15T09:30:00Z",
    ),
    organisationId: complianceSchemeId,
    organisationName: compliancePrn.organisationName,
    isExport: true,
});
