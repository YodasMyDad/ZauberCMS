# Vendored Blazored.Modal

Source of [Blazored.Modal](https://github.com/Blazored/Modal) v7.3.1 (commit `9095e1f`), MIT licence — see
`LICENSE-Blazored.Modal.txt`. The upstream repository is archived and the NuGet package deprecated, so the code lives
here and is maintained as part of ZauberCMS.

- Namespaces are unchanged (`Blazored.Modal`, `Blazored.Modal.Services`) so existing code and plugins compile as before.
- Do **not** also reference the `Blazored.Modal` NuGet package in a site that uses ZauberCMS: the types would clash.
- It lives in ZauberCMS.Components, with the rest of the admin UI (it was in ZauberCMS.Core until Roadmap Phase 11e,
  D51). Static assets are served from `_content/ZauberCMS.Components/` (the scoped CSS is in
  `ZauberCMS.Components.bundle.scp.css`; the script is `Modal/BlazoredModal.razor.js`). `AddBlazoredModal()` is called
  by `ZauberCMS.Components.Startup.AdminUiStartup`.
- The project's `_Imports.razor` applies to these components too (its `@inject`s and `@using`s); keep names here from
  clashing with it.
- Changes from upstream: `@namespace` in `_Imports.razor`, the JS import path in `BlazoredModal.razor`, and an
  `ErrorBoundary` round each modal's content in `BlazoredModalInstance.razor` (ZA-097: a panel that throws shows the
  error with "Try again" and "Close" instead of ending the circuit).
