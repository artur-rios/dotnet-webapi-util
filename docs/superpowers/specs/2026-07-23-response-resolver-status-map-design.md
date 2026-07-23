# ResponseResolver status-code mapping — design

**Date:** 2026-07-23
**Status:** Approved

## Problem

`ResponseResolver.Resolve` currently decides an HTTP status code from just two inputs:
an optional explicit `statusCode`, and the envelope's `Success` flag (200 on success,
400 on failure). A caller who wants a failed operation to surface a more specific status
— 404, 409, 422, etc. — must either hard-code `statusCode` at the call site (losing the
"let the resolver decide" convenience) or branch on the error text themselves.

We want to let the caller optionally supply a map from an envelope's **first error or
message** to an HTTP status code, so that the resolver can pick a meaningful status
without the action branching on strings, while leaving today's behavior untouched for
callers who pass nothing.

## Scope

In scope:

- Add one optional, trailing parameter to each of the three `Resolve` overloads.
- A single shared status-resolution helper (all three envelope types derive from
  `ProcessOutput`).
- XML doc updates on the class and the three overloads.
- Docs update: `docs/content/responses.md`.
- Unit tests for `ResponseResolver` (currently none exist).

Out of scope (YAGNI):

- No dedicated map/builder type — a plain dictionary is enough.
- No multi-key or ordered matching — only the first error/message is considered.
- No separate error-map/message-map split — one unified map, keyed by `Success`.

## Design

### Signature

Each overload gains one optional, trailing `IReadOnlyDictionary<string, int>? statusMap`
parameter. Being optional and last, this is source-compatible with every existing call.

```csharp
public static ActionResult<ProcessOutput> Resolve(
    ProcessOutput processOutput,
    int? statusCode = null,
    IReadOnlyDictionary<string, int>? statusMap = null);

public static ActionResult<DataOutput<T?>> Resolve<T>(
    DataOutput<T?> dataOutput,
    int? statusCode = null,
    IReadOnlyDictionary<string, int>? statusMap = null);

public static ActionResult<PaginatedOutput<T>> Resolve<T>(
    PaginatedOutput<T> paginatedOutput,
    int? statusCode = null,
    IReadOnlyDictionary<string, int>? statusMap = null);
```

### Resolution order

For every overload, the status code is resolved by the same rules — identical to today's
behavior when both `statusCode` and `statusMap` are omitted:

1. **`statusCode` supplied** → use it as-is, regardless of `Success` or the map.
2. **Else `statusMap` supplied** → choose the lookup key:
   - `Success == false` → the first `Errors` entry.
   - `Success == true` → the first `Messages` entry.

   If that key is non-null and present in the map, use its value.
3. **Otherwise** (no map, an empty `Errors`/`Messages` list, or a key not found in the
   map) → default: **200** on success, **400** on failure.

The caller owns the dictionary and therefore its key comparer — passing a dictionary
built with `StringComparer.OrdinalIgnoreCase` yields case-insensitive matching; the
resolver does no normalization of its own.

### Implementation

`DataOutput<T>` derives from `ProcessOutput` and `PaginatedOutput<T>` derives from
`DataOutput<T>`, so a single private helper typed on `ProcessOutput` serves all three
overloads. Each overload delegates to it and keeps returning the same `ObjectResult`.

```csharp
private static int ResolveStatusCode(ProcessOutput output, int? statusCode,
    IReadOnlyDictionary<string, int>? statusMap)
{
    if (statusCode.HasValue)
    {
        return statusCode.Value;
    }

    if (statusMap is not null)
    {
        var key = output.Success
            ? output.Messages.FirstOrDefault()
            : output.Errors.FirstOrDefault();

        if (key is not null && statusMap.TryGetValue(key, out var mapped))
        {
            return mapped;
        }
    }

    return GetDefaultStatusCode(output.Success);
}
```

`GetDefaultStatusCode` is unchanged.

## Testing

Add a `ResponseResolverTests` covering, for representative envelope types:

- No map, no `statusCode` → 200 on success, 400 on failure (behavior preserved).
- Explicit `statusCode` wins over both a matching map entry and the default.
- Map hit on the first **error** (failure) → mapped status.
- Map hit on the first **message** (success) → mapped status.
- Map miss (key not present) → falls back to 200/400 default.
- Empty `Errors`/`Messages` list with a map present → falls back to default (no crash).
- The returned `ObjectResult` body is the same envelope instance that was passed in.

Include at least one test per envelope type (`ProcessOutput`, `DataOutput<T>`,
`PaginatedOutput<T>`) to confirm the shared helper is wired through each overload.

## Docs

Update `docs/content/responses.md`:

- Extend the "Default status mapping" section to a three-step resolution order.
- Update the mermaid flowchart to show the `statusMap` branch between `statusCode` and
  the `Success` default.
- Add a short caller example building a `Dictionary<string, int>` and passing it as
  `statusMap`.
