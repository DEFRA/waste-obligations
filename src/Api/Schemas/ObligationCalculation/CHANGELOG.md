# Obligation calculation schema changelog

## v1.0

Initial persisted contract for a current submitter/year calculation, with stable ObjectId, entity version, schema version, UTC calculation time, submitter type and producer/material results. Quantities are whole 64-bit integers named `inputKilograms`, `inputTonnes` and `obligationTonnes`. `GL` and `GR` carry separate obligations and repeat the same glass input weights; those weights are not additive.

This is a new collection with no earlier persisted calculation contract or backfill. Index creation is additive and compatible with the previous application. History wraps an exact prior BSON payload without changing its schema version. No analytics events are introduced or rewritten.
