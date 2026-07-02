# Email translation export tool

This tool exports GOV.UK Notify email templates configured in the API project's
`src/Api/appsettings.json` file.

The exporter reads:

```text
GovukNotify:Templates:*:TemplateId:En
```

Each configured English template is pulled from GOV.UK Notify and written to a
Welsh translation workbook. If a matching Welsh template ID is configured, its
current subject/body is used to pre-fill the Welsh column where it differs from
the English source.

## API key

The CLI loads `.env` from the repository root before reading environment
variables. The local `.env` file is ignored by Git.

Supported key variables are:

```text
GovukNotify_ApiKey
GovukNotify__ApiKey
GOVUKNOTIFY_APIKEY
GOVUK_NOTIFY_API_KEY
```

## Commands

Build and test the tool:

```bash
dotnet build tools/translations/translations.slnx
dotnet test tools/translations/translations.slnx
```

Export the configured emails:

```bash
dotnet run --project tools/translations/cli/cli.csproj -- export
```

By default, workbooks are written to:

```text
translations/welsh-email-translations
```

To write somewhere else:

```bash
dotnet run --project tools/translations/cli/cli.csproj -- export --output /tmp/waste-obligations-email-translations
```

To read a different API settings file:

```bash
dotnet run --project tools/translations/cli/cli.csproj -- export --appsettings src/Api/appsettings.Development.json
```

## Applying returned translations

The public GOV.UK Notify API and .NET client can read templates and generate
previews, but they do not update template content. When a completed workbook is
returned, update the Welsh template manually in the GOV.UK Notify web UI.

For each workbook:

1. Open the workbook and check the hidden template metadata columns:
   - `Template name`
   - `Template id`
   - `Field`
2. In `src/Api/appsettings.json`, find the matching entry under
   `GovukNotify:Templates`.
3. Use the configured `TemplateId:Cy` value to identify the Welsh template that
   should be updated. If no Welsh template ID exists, create or request the
   Welsh template in GOV.UK Notify, then add the new ID to appsettings.
4. Sign in to GOV.UK Notify and open the service that owns the template.
5. Use the Welsh template ID from appsettings or the workbook to find/open the
   matching template.
6. Edit the template content:
   - copy the translated `Subject` row into the Notify email subject
   - copy the translated `Body` row into the Notify email body
7. Preserve GOV.UK Notify personalisation placeholders exactly, for example
   `((regulator))` and `((obligationYear))`.
8. Preserve Markdown formatting, links, headings and blank lines.
9. Save the template in GOV.UK Notify and use the template preview to check that
   the updated Welsh email renders as expected.
10. Re-run the export. The Welsh column should now be pre-filled from GOV.UK
    Notify, confirming that the saved template content matches the returned
    translation.
