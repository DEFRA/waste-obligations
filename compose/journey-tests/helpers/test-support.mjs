// Sets the environment the helpers read on import, and shares small test utilities.
Object.assign(process.env, {
  WASTE_OBLIGATION_ORG_ID: "b4a566a0-3a79-45a3-b43f-2ce900124750",
  WASTE_OBLIGATION_CSO_ORG_ID: "5165d0cb-fb61-4fd4-8b5e-75fa0e99a323",
  WASTE_OBLIGATION_SUBMITTER_ID: "2c0bb58b-91df-423d-9353-a87ef0d3c89e",
  WASTE_OBLIGATION_SUBMITTER_EMAIL: "journey-test@example.com",
});

export const mappingNamed = (list, name) => {
  const entry = list.find(([fileName]) => fileName === name);
  if (!entry) {
    throw new Error(`missing mapping ${name}`);
  }
  return entry[1];
};
