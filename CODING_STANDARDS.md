# Coding standards

## Coding conventions
- Name main service implementations with the `Service` suffix and their interfaces with the matching `I` prefix.
- Do not use the `Async` suffix for asynchronous methods
- Add blank line before return statement
- Prefer typed `Results.[method]()` helpers over `Results.Problem()` for endpoint responses; reserve `Results.Problem()` for cases without an appropriate typed helper
- Use constants for values that are used more than once; inline values that are only used once
- Place each production class, interface, record, enum, or struct in its own file, named for that type. Keep nested test-only types with their test when that makes the test self-contained.
- Declare variables as close to their point of use as possible
- Use camelCase for constants declared within methods
- Lint files changed/created using "dotnet csharpier format ."
- Name expressions with x => x. syntax where possible
- Specify variables as const in tests where possible
- Use collection expressions where possible
- Use object initializers where possible
- When enums are used as variables, inline them instead of creating a property
- For a public query parameter that accepts one enum value, bind the raw nullable string, validate it against the exact JSON enum values, then parse it after validation; do not bind it directly to an enum because numeric values may be accepted. Use `EnumCommaSeparatedList` when the contract accepts a list of enum values.
- Merge related conditionals where doing so keeps the condition clear.
- Prefer `??` directly in a return statement when it clearly expresses a null fallback or exception.
- Do not use Arrange Act Assert comments in tests
- Use _camelCase for private instance fields
- Prefer AwesomeAssertions for assertions; where `Should().NotBeNull()` provides nullable flow information, do not add redundant null suppression operators or extra null guards
- Keep assertion style consistent within a test or helper; avoid introducing local variables solely for one assertion when surrounding assertions access the same object inline
- When fixing Sonar or analyzer findings in tests, prefer keeping values local to the assertion or test where possible; do not hoist values to test class level solely to satisfy a warning unless they are genuinely shared
- Place new appsettings.json (and related environment variant files) config sections at the bottom of existing settings
