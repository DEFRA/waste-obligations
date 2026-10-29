// Builders for the WireMock request and response mappings.

export const json = (value) => JSON.stringify(value, null, 2);
export const mapping = (request, body, statusCode = 200) => ({
    Request: request,
    Response: {
        StatusCode: statusCode,
        BodyAsJson: body,
        Headers: { "Content-Type": "application/json; charset=utf-8" },
    },
});
export const exactPath = (path) => ({
    Path: {
        Matchers: [{ Name: "ExactMatcher", Pattern: path }],
    },
    Methods: ["GET"],
});

export const organisationHeader = (organisationId) => [
    {
        Name: "X-EPR-ORGANISATION",
        Matchers: [{ Name: "ExactMatcher", Pattern: organisationId }],
    },
];

// A scenario state's search answer must beat every query-specific search
// mapping, the most specific of which has priority 1.
export const PRN_MAPPING_0 = 0;
export const PRN_MAPPING_1 = 1;
export const PRN_MAPPING_2 = 2;
export const PRN_MAPPING_3 = 3;
export const PRN_MAPPING_10 = 10;


export const queryParam = (name, value) => ({
    Name: name,
    Matchers: [{ Name: "ExactMatcher", Pattern: value }],
});
