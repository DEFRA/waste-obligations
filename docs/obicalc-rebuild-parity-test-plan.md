# Obligation calculator rebuild: parity test plan

Prepared 5 November 2026. Purpose: confirm that the CDP rebuild of the obligation calculation (waste-obligations,
MO-664 onwards) gives exactly the same per-producer, per-material obligations as the legacy Azure pipeline.

## 1. Sources

| Source | What it gives |
| --- | --- |
| ECV-196 (scenario 2a, direct producer, code 02, joins 1 May 2025, 245/365) | 2a H1/H2 POM CSVs, reg CSV, `MYC 2a&2b.xlsx`, TST checks in comments |
| ECV-207 (scenario 2b, group subsidiary, code 03, joins 3 May 2025, 243/365) | `2b.xlsx` (reg, H1/H2 POM, workings), reg and POM CSVs, compliance scheme workings in comments |
| ECV-340 (partial rounding fix) | Paper + Fibre Composite check (1616) and a compliance scheme member on TST (201/365) with `MaterialObligationValue` per material |
| epr-prn-common-backend `CalculationParityTests.cs` at 488028b (PR #466, MO-664) | Unit-level examples: general materials, glass split, partial days, leap year, 2025–2030 targets |
| Legacy code read on 5 November 2026 | `MaterialCalculationService` and strategies (epr-prn-common-backend main) |
| Rebuild code read on 5 November 2026 | waste-obligations PR #366 `MO-664-calculate-obligations` (full-year calculator only) |
| Policy paper "Proposed approach for calculating partial recycling obligations", 29 October 2026 | Schedule 5 and Schedule 9 basis; three glass methods (Method 2 is the current service logic); glass and paper/fibre worked examples |
| Business decision, received 5 November 2026 | Keep the existing service logic for partial obligations and rounding (Method 2 in the paper) |

Every expected value was recomputed by running a copy of the legacy `MaterialCalculationService` arithmetic
against the ticket inputs. Where a ticket spreadsheet disagrees with the code, the code value is used.

## 2. Test cases

**Material key**

| Code | Material |
| --- | --- |
| AL | Aluminium |
| FC | Fibre-based composite |
| GL | Glass. As an input, all glass tonnes; as a result, the glass other (remainder) obligation |
| GR | Glass remelt (a result only, never an input) |
| PC | Paper or board |
| PL | Plastic |
| ST | Steel |
| WD | Wood |

Displayed figures: Paper = PC + FC; Glass = GL; Glass Remelt = GR. "t" = tonnes.

Scope: the obligation calculation only. Which producers are obligated, their days obligated and the validation of
submitted registration and POM data belong to the submit team and are inputs here.

Priority: **P1** must pass before the rebuild replaces the function; **P2** should pass. "Level": U = rebuild
calculator unit/parity test, E = end to end (inputs in, stored and displayed obligations out).

The rebuild calculator takes kilograms. For unit cases below, tonnes × 1000 kg.

### 2.1 Full-year arithmetic (from PR #466)

| ID | Level | Input | Expected | P |
| --- | --- | --- | --- | --- |
| FY-01 | U | PL 101 t, 2025 | 56 | P1 |
| FY-02 | U | FC 101 t, 2025 | 76 | P1 |
| FY-03 | U | PC 100 t, 2025 | 75 | P1 |
| FY-04 | U | AL 100 t, 2025 | 61 | P1 |
| FY-05 | U | ST 100 t, 2025 | 80 | P1 |
| FY-06 | U | WD 100 t, 2025 | 45 | P1 |
| FY-07 | U | PL 20 t, 2025 (exact 11.00) | 11 (no round-up of an exact integer) | P1 |
| FY-08 | U | PL 21 t, 2025 (11.55) | 12 | P1 |
| FY-09 | U | ST 2,147,483,647 t, 2025 | 1,717,986,918 (legacy int limit; rebuild uses long) | P2 |
| FY-10 | U | 0 t of a material that is present | record with obligation 0 | P1 |
| FY-11 | U | Material absent from POM | no record for that material (not a 0 record) | P1 |

Targets by year (100 t of each material; GL shows remainder / remelt):

| ID | Year | PL | PC | FC | AL | ST | WD | GL | GR | P |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| TY-25 | 2025 | 55 | 75 | 75 | 61 | 80 | 45 | 18 | 56 | P1 |
| TY-26 | 2026 | 57 | 77 | 77 | 62 | 81 | 46 | 18 | 58 | P1 |
| TY-27 | 2027 | 59 | 79 | 79 | 63 | 82 | 47 | 17 | 61 | P1 |
| TY-28 | 2028 | 61 | 81 | 81 | 64 | 83 | 48 | 17 | 63 | P1 |
| TY-29 | 2029 | 63 | 83 | 83 | 65 | 84 | 49 | 17 | 65 | P1 |
| TY-30 | 2030 | 65 | 85 | 85 | 67 | 85 | 50 | 17 | 68 | P1 |
| TY-31 | Same PL 100 t for 2025 and 2026 in one run | 55 and 57 | | | | | | | | P1 |
| TY-32 | Year with no configured targets | fails, nothing stored (legacy throws `KeyNotFoundException`) | | | | | | | | P1 |

### 2.2 Glass split

| ID | Level | Input | Expected GL (remainder) / GR (remelt) | Why | P |
| --- | --- | --- | --- | --- | --- |
| GL-01 | U | 101 t, 2025 | 18 / 57 | ceil(74.74 × 0.75) = 57, ceil(74.74) − 57 = 18 | P1 |
| GL-02 | U | 3 t, 2025 | 1 / 2 | Remelt uses unrounded 2.22; rounding first would give 0 / 3 | P1 |
| GL-03 | U | 1 t, 2025 | 0 / 1 | Remainder can be 0 | P1 |
| GL-04 | U | 2 t, 2025 | 0 / 2 | | P2 |
| GL-05 | U | 100 t, 2026 | 18 / 58 | Uses 2026 remelt share 0.76, not 0.75 | P1 |
| GL-06 | U | 101 t, 2025 | Two records: GL (other) = 18 t and GR (remelt) = 57 t, each with tonnage 101 and the same `CalculatedOn` | Glass is stored as two materials; a single glass record would not match legacy | P2 |

### 2.3 Partial year (joiners)

Days obligated is an input (joiner date to 31 December inclusive).

| ID | Level | Input | Expected | P |
| --- | --- | --- | --- | --- |
| PY-01 | U | PL 101 t, 2025, 182 days | 28 | P1 |
| PY-02 | U | PL 101 t, 2028 (leap year, 366 days), 183 days | 31 | P1 |
| PY-03 | U | PL 101 t, 0 days | 0 | P2 |
| PY-04 | U | PL 2 t, 2025, 183 days | 2 (full 2 then scaled; scaling tonnage first gives 1). Same for direct registrant and compliance scheme | P1 |
| PY-05 | U | GL 101 t, 2025, 182 days | 9 / 29 | P1 |
| PY-06 | U | GL 101 t, 2028 (leap year, 366 days), 183 days | 9 / 32 | P1 |
| PY-07 | U | Joiner 1 January 2026, PL 101 t + GL 101 t | 365 days = full: PL 58, GL 18 / GR 59 | P1 |
| PY-08 | U | Joiner 31 December 2026 (1 day), PL 101 t, GL 101 t, WD 3 t | PL 1, GL 1 / GR 1, WD 1 | P1 |
| PY-09 | U | Joiner 31 December 2025 (1 day), GL 101 t | GL 1 / GR 1 (remelt and other each rounded up separately; rounding the total instead gives 1) | P1 |
| LY-01 | U | Leap year: PL 1000 t, 2028 (366 days), obligated 365 of 366 days | 609 (full year 610 × 365/366 = 608.33, rounded up; a 365-day year would wrongly give the full 610) | P1 |
| LY-02 | U | Leap year: joiner on leap day 29 February 2028 (307 of 366 days), PL 1000 t, GL 1000 t | PL 512; GL 148 / GR 524 (full 610; 176 / 624) | P1 |

#### Partial rounding: existing logic (current code, expected behaviour)

Each material's full-year obligation is scaled by days ÷ days in year and rounded up. For glass, the full-year remelt
and other (total − remelt) are each scaled and rounded up, then added.

Legacy quirk: the code works out days ÷ days in year as a decimal that rounds up in its last digit for some day counts
(for example 243, 201 and 5, not 245). When the scaled value is exactly a whole number, the result is 1 higher than an
exact round-up. Rows marked (quirk) show this.

| ID | Input | How the partial obligation is worked out | Expected partial obligation (tonnes) | P |
| --- | --- | --- | --- | --- |
| PY-11 | PL = 663 t<br>Year = 2025<br>Days obligated = 243/365<br>(joiner 3 May 2025) | Full-year obligation: 663 t × 0.55 plastic target = 364.65, rounded up to 365 t<br>365 × 243/365 = 243.00, rounded up to **244** (quirk: an exact round-up gives 243) | Plastic (PL) = 244 t | P1 |
| PY-12 | PL = 663 t<br>Year = 2025<br>Days obligated = 201/365<br>(joiner 14 June 2025) | Full-year obligation: 663 t × 0.55 plastic target = 364.65, rounded up to 365 t<br>365 × 201/365 = 201.00, rounded up to **202** (quirk: an exact round-up gives 201) | Plastic (PL) = 202 t | P1 |
| PY-13 | PL = 128 t<br>Year = 2026<br>Days obligated = 5/365<br>(joiner 27 December 2026) | Full-year obligation: 128 t × 0.57 plastic target = 72.96, rounded up to 73 t<br>73 × 5/365 = 1.00, rounded up to **2** (quirk: an exact round-up gives 1) | Plastic (PL) = 2 t | P1 |
| PY-15 | PL = 9 t<br>Year = 2028 (leap year, 366 days)<br>Days obligated = 61/366<br>(joiner 1 November 2028) | Full-year obligation: 9 t × 0.61 plastic target = 5.49, rounded up to 6 t<br>6 × 61/366 = 1.00, rounded up to **2** (quirk: an exact round-up gives 1) | Plastic (PL) = 2 t | P1 |
| PY-16 | PL = 663 t<br>Year = 2025<br>Days obligated = 245/365<br>(joiner 1 May 2025; control, not affected) | Full-year obligation: 663 t × 0.55 plastic target = 364.65, rounded up to 365 t<br>365 × 245/365 = 245.00, rounded up to **245** | Plastic (PL) = 245 t | P1 |
| PY-18 | PC = 110 t, FC = 210 t<br>Year = 2026<br>Days obligated = 183/365<br>(policy paper example) | Full-year paper/board: 110 t × 0.77 target = 84.7, rounded up to 85 t<br>Full-year fibre composite: 210 t × 0.77 target = 161.7, rounded up to 162 t<br>Paper/board: 85 × 183/365 = 42.62, rounded up to **43**<br>Fibre composite: 162 × 183/365 = 81.22, rounded up to **82** | Paper/board (PC) = 43 t<br>Fibre composite (FC) = 82 t<br>Paper displayed = 125 t | P1 |
| PY-17 | GL = 110 t<br>Year = 2026<br>Days obligated = 183/365<br>(policy paper example) | Full-year glass total: 110 t × 0.76 glass target = 83.6, rounded up to 84 t<br>Full-year remelt: 83.6 × 0.76 remelt share = 63.536, rounded up to 64 t<br>Full-year other: 84 − 64 = 20 t<br>Remelt: 64 × 183/365 = 32.09, rounded up to **33**<br>Other: 20 × 183/365 = 10.03, rounded up to **11**<br>Total: 33 + 11 = **44** | Glass remelt (GR) = 33 t<br>Glass other (GL) = 11 t<br>Glass total = 44 t | P1 |
| PY-14 | GL = 125 t<br>Year = 2026<br>Days obligated = 5/365<br>(joiner 27 December 2026) | Full-year glass total: 125 t × 0.76 glass target = 95, rounded up to 95 t<br>Full-year remelt: 95 × 0.76 remelt share = 72.2, rounded up to 73 t<br>Full-year other: 95 − 73 = 22 t<br>Remelt: 73 × 5/365 = 1.00, rounded up to **2** (quirk: an exact round-up gives 1)<br>Other: 22 × 5/365 = 0.30, rounded up to **1**<br>Total: 2 + 1 = **3** | Glass remelt (GR) = 2 t<br>Glass other (GL) = 1 t<br>Glass total = 3 t | P1 |
| PY-08g | GL = 101 t<br>Year = 2026<br>Days obligated = 1/365<br>(joiner 31 December 2026) | Full-year glass total: 101 t × 0.76 glass target = 76.76, rounded up to 77 t<br>Full-year remelt: 76.76 × 0.76 remelt share = 58.3376, rounded up to 59 t<br>Full-year other: 77 − 59 = 18 t<br>Remelt: 59 × 1/365 = 0.16, rounded up to **1**<br>Other: 18 × 1/365 = 0.05, rounded up to **1**<br>Total: 1 + 1 = **2** | Glass remelt (GR) = 1 t<br>Glass other (GL) = 1 t<br>Glass total = 2 t | P1 |
| PY-05 | GL = 101 t<br>Year = 2025<br>Days obligated = 182/365 | Full-year glass total: 101 t × 0.74 glass target = 74.74, rounded up to 75 t<br>Full-year remelt: 74.74 × 0.75 remelt share = 56.055, rounded up to 57 t<br>Full-year other: 75 − 57 = 18 t<br>Remelt: 57 × 182/365 = 28.42, rounded up to **29**<br>Other: 18 × 182/365 = 8.98, rounded up to **9**<br>Total: 29 + 9 = **38** | Glass remelt (GR) = 29 t<br>Glass other (GL) = 9 t<br>Glass total = 38 t | P1 |
| PY-06 | GL = 101 t<br>Year = 2028 (leap year, 366 days)<br>Days obligated = 183/366 | Full-year glass total: 101 t × 0.80 glass target = 80.8, rounded up to 81 t<br>Full-year remelt: 80.8 × 0.78 remelt share = 63.024, rounded up to 64 t<br>Full-year other: 81 − 64 = 17 t<br>Remelt: 64 × 183/366 = 32.00, rounded up to **32**<br>Other: 17 × 183/366 = 8.50, rounded up to **9**<br>Total: 32 + 9 = **41** | Glass remelt (GR) = 32 t<br>Glass other (GL) = 9 t<br>Glass total = 41 t | P1 |
| LY-02g | GL = 1000 t<br>Year = 2028 (leap year, 366 days)<br>Days obligated = 307/366<br>(joiner 29 February 2028) | Full-year glass total: 1000 t × 0.80 glass target = 800, rounded up to 800 t<br>Full-year remelt: 800 × 0.78 remelt share = 624, rounded up to 624 t<br>Full-year other: 800 − 624 = 176 t<br>Remelt: 624 × 307/366 = 523.41, rounded up to **524**<br>Other: 176 × 307/366 = 147.63, rounded up to **148**<br>Total: 524 + 148 = **672** | Glass remelt (GR) = 524 t<br>Glass other (GL) = 148 t<br>Glass total = 672 t | P1 |
| E2E-02 | GL = 70 t<br>Year = 2025<br>Days obligated = 243/365<br>(ECV-207 subsidiary) | Full-year glass total: 70 t × 0.74 glass target = 51.8, rounded up to 52 t<br>Full-year remelt: 51.8 × 0.75 remelt share = 38.85, rounded up to 39 t<br>Full-year other: 52 − 39 = 13 t<br>Remelt: 39 × 243/365 = 25.96, rounded up to **26**<br>Other: 13 × 243/365 = 8.65, rounded up to **9**<br>Total: 26 + 9 = **35** | Glass remelt (GR) = 26 t<br>Glass other (GL) = 9 t<br>Glass total = 35 t | P1 |
| E2E-01 | GL = 31557 t<br>Year = 2025<br>Days obligated = 245/365<br>(ECV-196) | Full-year glass total: 31557 t × 0.74 glass target = 23352.18, rounded up to 23353 t<br>Full-year remelt: 23352.18 × 0.75 remelt share = 17514.135, rounded up to 17515 t<br>Full-year other: 23353 − 17515 = 5838 t<br>Remelt: 17515 × 245/365 = 11756.64, rounded up to **11757**<br>Other: 5838 × 245/365 = 3918.66, rounded up to **3919**<br>Total: 11757 + 3919 = **15676** | Glass remelt (GR) = 11757 t<br>Glass other (GL) = 3919 t<br>Glass total = 15676 t | P1 |
| E2E-04 | GL = 23557 t<br>Year = 2025<br>Days obligated = 201/365<br>(ECV-340) | Full-year glass total: 23557 t × 0.74 glass target = 17432.18, rounded up to 17433 t<br>Full-year remelt: 17432.18 × 0.75 remelt share = 13074.135, rounded up to 13075 t<br>Full-year other: 17433 − 13075 = 4358 t<br>Remelt: 13075 × 201/365 = 7200.21, rounded up to **7201**<br>Other: 4358 × 201/365 = 2399.88, rounded up to **2400**<br>Total: 7201 + 2400 = **9601** | Glass remelt (GR) = 7201 t<br>Glass other (GL) = 2400 t<br>Glass total = 9601 t | P1 |
| E2E-03 | GL = 33784 t<br>Year = 2025<br>Days obligated = 245/365<br>(`MYC 2a&2b.xlsx`) | Full-year glass total: 33784 t × 0.74 glass target = 25000.16, rounded up to 25001 t<br>Full-year remelt: 25000.16 × 0.75 remelt share = 18750.12, rounded up to 18751 t<br>Full-year other: 25001 − 18751 = 6250 t<br>Remelt: 18751 × 245/365 = 12586.29, rounded up to **12587**<br>Other: 6250 × 245/365 = 4195.21, rounded up to **4196**<br>Total: 12587 + 4196 = **16783** | Glass remelt (GR) = 12587 t<br>Glass other (GL) = 4196 t<br>Glass total = 16783 t | P1 |

#### Partial rounding: proposed change

> **Not signed off.** As of 6 November 2026 the business has not signed off this proposed change. The existing logic
> above is the expected behaviour; this table is for comparison only.

Each obligation is scaled by days ÷ days in year and rounded up exactly (no decimal quirk). For glass, the full-year
total and remelt are each scaled and rounded up, and other = partial total − partial remelt. Input tonnes are the
packaging supplied; days obligated is the number of days the producer is obligated for out of the days in that year.

| ID | Input | How the partial obligation is worked out | Partial obligation (tonnes) | Differs from existing? |
| --- | --- | --- | --- | --- |
| PY-11 | PL = 663 t<br>Year = 2025<br>Days obligated = 243/365<br>(joiner 3 May 2025) | Full-year obligation: 663 t × 0.55 plastic target = 364.65, rounded up to 365 t<br>365 × 243/365 = 243.00, rounded up to **243** | Plastic (PL) = 243 t | Yes: 243 t vs 244 t |
| PY-12 | PL = 663 t<br>Year = 2025<br>Days obligated = 201/365<br>(joiner 14 June 2025) | Full-year obligation: 663 t × 0.55 plastic target = 364.65, rounded up to 365 t<br>365 × 201/365 = 201.00, rounded up to **201** | Plastic (PL) = 201 t | Yes: 201 t vs 202 t |
| PY-13 | PL = 128 t<br>Year = 2026<br>Days obligated = 5/365<br>(joiner 27 December 2026) | Full-year obligation: 128 t × 0.57 plastic target = 72.96, rounded up to 73 t<br>73 × 5/365 = 1.00, rounded up to **1** | Plastic (PL) = 1 t | Yes: 1 t vs 2 t |
| PY-15 | PL = 9 t<br>Year = 2028 (leap year, 366 days)<br>Days obligated = 61/366<br>(joiner 1 November 2028) | Full-year obligation: 9 t × 0.61 plastic target = 5.49, rounded up to 6 t<br>6 × 61/366 = 1.00, rounded up to **1** | Plastic (PL) = 1 t | Yes: 1 t vs 2 t |
| PY-16 | PL = 663 t<br>Year = 2025<br>Days obligated = 245/365<br>(joiner 1 May 2025; control, not affected) | Full-year obligation: 663 t × 0.55 plastic target = 364.65, rounded up to 365 t<br>365 × 245/365 = 245.00, rounded up to **245** | Plastic (PL) = 245 t | No |
| PY-18 | PC = 110 t, FC = 210 t<br>Year = 2026<br>Days obligated = 183/365<br>(policy paper example) | Full-year paper/board: 110 t × 0.77 target = 84.7, rounded up to 85 t<br>Full-year fibre composite: 210 t × 0.77 target = 161.7, rounded up to 162 t<br>Paper/board: 85 × 183/365 = 42.62, rounded up to **43**<br>Fibre composite: 162 × 183/365 = 81.22, rounded up to **82** | Paper/board (PC) = 43 t<br>Fibre composite (FC) = 82 t<br>Paper displayed = 125 t | No |
| PY-17 | GL = 110 t<br>Year = 2026<br>Days obligated = 183/365<br>(policy paper example) | Full-year glass total: 110 t × 0.76 glass target = 83.6, rounded up to 84 t<br>Full-year remelt: 83.6 × 0.76 remelt share = 63.536, rounded up to 64 t<br>Total: 84 × 183/365 = 42.12, rounded up to **43**<br>Remelt: 64 × 183/365 = 32.09, rounded up to **33**<br>Other: 43 − 33 = **10** | Glass remelt (GR) = 33 t<br>Glass other (GL) = 10 t<br>Glass total = 43 t | Yes: other 10 t vs 11 t, total 43 t vs 44 t |
| PY-14 | GL = 125 t<br>Year = 2026<br>Days obligated = 5/365<br>(joiner 27 December 2026) | Full-year glass total: 125 t × 0.76 glass target = 95, rounded up to 95 t<br>Full-year remelt: 95 × 0.76 remelt share = 72.2, rounded up to 73 t<br>Total: 95 × 5/365 = 1.30, rounded up to **2**<br>Remelt: 73 × 5/365 = 1.00, rounded up to **1**<br>Other: 2 − 1 = **1** | Glass remelt (GR) = 1 t<br>Glass other (GL) = 1 t<br>Glass total = 2 t | Yes: remelt 1 t vs 2 t, total 2 t vs 3 t |
| PY-08g | GL = 101 t<br>Year = 2026<br>Days obligated = 1/365<br>(joiner 31 December 2026) | Full-year glass total: 101 t × 0.76 glass target = 76.76, rounded up to 77 t<br>Full-year remelt: 76.76 × 0.76 remelt share = 58.3376, rounded up to 59 t<br>Total: 77 × 1/365 = 0.21, rounded up to **1**<br>Remelt: 59 × 1/365 = 0.16, rounded up to **1**<br>Other: 1 − 1 = **0** | Glass remelt (GR) = 1 t<br>Glass other (GL) = 0 t<br>Glass total = 1 t | Yes: other 0 t vs 1 t, total 1 t vs 2 t |
| PY-05 | GL = 101 t<br>Year = 2025<br>Days obligated = 182/365 | Full-year glass total: 101 t × 0.74 glass target = 74.74, rounded up to 75 t<br>Full-year remelt: 74.74 × 0.75 remelt share = 56.055, rounded up to 57 t<br>Total: 75 × 182/365 = 37.40, rounded up to **38**<br>Remelt: 57 × 182/365 = 28.42, rounded up to **29**<br>Other: 38 − 29 = **9** | Glass remelt (GR) = 29 t<br>Glass other (GL) = 9 t<br>Glass total = 38 t | No |
| PY-06 | GL = 101 t<br>Year = 2028 (leap year, 366 days)<br>Days obligated = 183/366 | Full-year glass total: 101 t × 0.80 glass target = 80.8, rounded up to 81 t<br>Full-year remelt: 80.8 × 0.78 remelt share = 63.024, rounded up to 64 t<br>Total: 81 × 183/366 = 40.50, rounded up to **41**<br>Remelt: 64 × 183/366 = 32.00, rounded up to **32**<br>Other: 41 − 32 = **9** | Glass remelt (GR) = 32 t<br>Glass other (GL) = 9 t<br>Glass total = 41 t | No |
| LY-02g | GL = 1000 t<br>Year = 2028 (leap year, 366 days)<br>Days obligated = 307/366<br>(joiner 29 February 2028) | Full-year glass total: 1000 t × 0.80 glass target = 800, rounded up to 800 t<br>Full-year remelt: 800 × 0.78 remelt share = 624, rounded up to 624 t<br>Total: 800 × 307/366 = 671.04, rounded up to **672**<br>Remelt: 624 × 307/366 = 523.41, rounded up to **524**<br>Other: 672 − 524 = **148** | Glass remelt (GR) = 524 t<br>Glass other (GL) = 148 t<br>Glass total = 672 t | No |
| E2E-02 | GL = 70 t<br>Year = 2025<br>Days obligated = 243/365<br>(ECV-207 subsidiary) | Full-year glass total: 70 t × 0.74 glass target = 51.8, rounded up to 52 t<br>Full-year remelt: 51.8 × 0.75 remelt share = 38.85, rounded up to 39 t<br>Total: 52 × 243/365 = 34.62, rounded up to **35**<br>Remelt: 39 × 243/365 = 25.96, rounded up to **26**<br>Other: 35 − 26 = **9** | Glass remelt (GR) = 26 t<br>Glass other (GL) = 9 t<br>Glass total = 35 t | No |
| E2E-01 | GL = 31557 t<br>Year = 2025<br>Days obligated = 245/365<br>(ECV-196) | Full-year glass total: 31557 t × 0.74 glass target = 23352.18, rounded up to 23353 t<br>Full-year remelt: 23352.18 × 0.75 remelt share = 17514.135, rounded up to 17515 t<br>Total: 23353 × 245/365 = 15675.30, rounded up to **15676**<br>Remelt: 17515 × 245/365 = 11756.64, rounded up to **11757**<br>Other: 15676 − 11757 = **3919** | Glass remelt (GR) = 11757 t<br>Glass other (GL) = 3919 t<br>Glass total = 15676 t | No |
| E2E-04 | GL = 23557 t<br>Year = 2025<br>Days obligated = 201/365<br>(ECV-340) | Full-year glass total: 23557 t × 0.74 glass target = 17432.18, rounded up to 17433 t<br>Full-year remelt: 17432.18 × 0.75 remelt share = 13074.135, rounded up to 13075 t<br>Total: 17433 × 201/365 = 9600.09, rounded up to **9601**<br>Remelt: 13075 × 201/365 = 7200.21, rounded up to **7201**<br>Other: 9601 − 7201 = **2400** | Glass remelt (GR) = 7201 t<br>Glass other (GL) = 2400 t<br>Glass total = 9601 t | No |
| E2E-03 | GL = 33784 t<br>Year = 2025<br>Days obligated = 245/365<br>(`MYC 2a&2b.xlsx`) | Full-year glass total: 33784 t × 0.74 glass target = 25000.16, rounded up to 25001 t<br>Full-year remelt: 25000.16 × 0.75 remelt share = 18750.12, rounded up to 18751 t<br>Total: 25001 × 245/365 = 16781.49, rounded up to **16782**<br>Remelt: 18751 × 245/365 = 12586.29, rounded up to **12587**<br>Other: 16782 − 12587 = **4195** | Glass remelt (GR) = 12587 t<br>Glass other (GL) = 4195 t<br>Glass total = 16782 t | Yes: other 4195 t vs 4196 t, total 16782 t vs 16783 t |

### 2.4 Kilograms to tonnes

| ID | Level | Input | Expected | P |
| --- | --- | --- | --- | --- |
| KG-01 | U | 499 kg / 500 kg / 1499 kg / 1500 kg / 2500 kg | 0 / 1 / 1 / 2 / 3 t (half away from zero, not banker's rounding) | P1 |
| KG-02 | U | ECV-196 2a totals (Producer A) | AL 377,286 kg → 377 t; FC 1,155,626 → 1156; GL 31,557,430 → 31557; PC 2,051,702 → 2052; PL 900,592 → 901; WD 883,484 → 883 | P1 |

## 3. End-to-end scenarios from the tickets

Use these as data sets: load the reg and POM files, run both pipelines, compare stored values per producer and
material, then the displayed totals.

### E2E-01 Scenario 2a: direct producer, code 02, joins 1 May 2025 (ECV-196, ECV-340)

Producer A (direct registrant), 245/365, data from `2a_H1.csv`, `2a_H2.csv`, `MYC-2a org (2).csv`.

| Material | Tonnes | Full year | Partial (expected) |
| --- | --- | --- | --- |
| AL | 377 | 230 | 155 |
| FC | 1156 | 867 | 582 |
| GL remainder | 31557 | 5838 | 3919 |
| GR remelt | 31557 | 17515 | 11757 |
| PC | 2052 | 1539 | 1034 |
| PL | 901 | 496 | 333 |
| WD | 883 | 398 | 268 |

Displayed: Paper 1616 (matches the ECV-340 TST check), Glass Remelt 11757, Glass 3919.

### E2E-02 Scenario 2b: group, subsidiary code 03, joins 3 May 2025 (ECV-207)

Holding company B (full year) and Subsidiary C (243/365). Data from
`reg-15012026_15_13_27.csv` and the two POM CSVs (submitted as 2024-P1 / 2024-P4).

| Producer | Material | Tonnes | Full year | Expected |
| --- | --- | --- | --- | --- |
| Holding company B | PL | 40 | 22 | 22 |
| Holding company B | GL remainder | 40 | 7 | 7 |
| Holding company B | GR remelt | 40 | 23 | 23 |
| Subsidiary C | PL | 70 | 39 | 26 |
| Subsidiary C | GL remainder | 70 | 13 | 9 |
| Subsidiary C | GR remelt | 70 | 39 | 26 |
| Total | PL / GL / GR | | | 48 / 16 / 49 (matches the ECV-207 UI evidence) |

### E2E-03 `MYC 2a&2b.xlsx` (tonnes given directly, 245/365, 2025)

| Material | Tonnes | Full year | 2a partial | 2b group (Sub A full + Sub B partial) |
| --- | --- | --- | --- | --- |
| AL | 8197 | 5001 | 3357 | 8358 |
| PC (paper, board and fibre, one line) | 26667 | 20001 | 13426 | 33427 |
| PL | 45455 | 25001 | 16782 | 41783 |
| ST | 37500 | 30000 | 20137 | 50137 |
| WD | 26667 | 12001 | 8056 | 20057 |
| GR remelt | 33784 | 18751 | 12587 | 31338 |
| GL remainder | 33784 | 6250 | **4196** | **10446** |

### E2E-04 Compliance scheme member joining 14 June 2025 (ECV-340, TST)

Scheme member D, a member of a compliance scheme, 201/365.

| Material | Tonnes | Full year | Expected (matches DB on TST) |
| --- | --- | --- | --- |
| AL | 0 | 0 | 0 |
| FC | 16 | 12 | 7 |
| GL remainder | 23557 | 4358 | 2400 |
| GR remelt | 23557 | 13075 | 7201 |
| PC | 1870 | 1403 | 773 |
| PL | 901 | 496 | 274 |
| WD | 3 | 2 | 2 |

Other scheme members: unchanged.

### E2E-05 Whole-population comparison (tst)

For the current obligation year, run the rebuild over the same approved submissions the function last processed and
compare every `(submitter, producer, material, year)` value with the legacy `ObligationCalculations` table (not
deleted rows). Expected: zero differences, same set of keys (no missing or extra producers or materials). Report
counts compared, matched and each difference. P1.
