# SmartyStreetsLookup

Sample code for validating/normalizing US and international mailing
addresses via [SmartyStreets](https://www.smarty.com/)' US Street and
International Street REST APIs.

This folder has three independent, self-contained pieces. Pick whichever
fits your scenario - they don't depend on each other or on any shared
project.

| Folder | What it is | When to use it |
|---|---|---|
| [`AzureFunction/`](AzureFunction) | An isolated-worker Azure Function (.NET 10) exposing two HTTP endpoints that look up addresses via SmartyStreets | You want a shared, centrally-deployed lookup service that other apps call over HTTPS without each needing its own SmartyStreets credentials |
| [`ClientSamples/`](ClientSamples) | Sample HTTP client code that calls the Azure Function above | You're integrating a caller with the Azure Function and want a starting point for either .NET Framework 4.7/4.8 or .NET 6/8/10 |
| [`ClassLibraries/`](ClassLibraries) | Two standalone class libraries (`SmartyStreetsLookup.NetFramework` and `SmartyStreetsLookup.NetCore`) that call SmartyStreets directly | You want to look up addresses from *inside* your own app, with no extra HTTP hop |

## Why two ways to look up (Azure Function vs. class library)?

- The **Azure Function** centralizes the SmartyStreets credentials in one
  place. Good when several applications - possibly including ones you don't
  want to hand SmartyStreets credentials to directly - need address lookups.
- The **class libraries** are for when you're building (or already own) the
  calling app and would rather skip the network hop and call SmartyStreets
  in-process.

## US vs. International

Every entry point (both Function endpoints, and both class libraries)
exposes the same choice: a **US** lookup (SmartyStreets' US Street API) and
an **International** lookup (SmartyStreets' International Street API) for
any other country. Pick based on where the address is - the request/response
shape is identical either way, except `Country` is required for the
International path and ignored for the US path.

## Prerequisites common to all three

A [SmartyStreets account](https://www.smarty.com/account/keys) (a free tier
is available) with an auth-id/auth-token pair for the US Street API and/or
the International Street API - the two are licensed and authenticated
separately, so you only need the pair(s) for the API(s) you're actually
calling.

## Security note

This is sample/portfolio code. All credentials in this repo are placeholders
(`your-us-auth-id`, `REPLACE_WITH_YOUR_AUTH_TOKEN`, etc.) - replace them with
your own before running anything for real, and never commit real secrets.
`local.settings.json` is git-ignored for exactly that reason; only
`local.settings.json.sample` is checked in.
