# Vendored Blazored.Modal

Source of [Blazored.Modal](https://github.com/Blazored/Modal) v7.3.1 (commit `9095e1f`), MIT licence — see
`LICENSE-Blazored.Modal.txt`. The upstream repository is archived and the NuGet package deprecated, so the code lives
here and is maintained as part of ZauberCMS.

- Namespaces are unchanged (`Blazored.Modal`, `Blazored.Modal.Services`) so existing code and plugins compile as before.
- Do **not** also reference the `Blazored.Modal` NuGet package in a site that uses ZauberCMS: the types would clash.
- Static assets are served from `_content/ZauberCMS.Core/` (the scoped CSS bundle `ZauberCMS.Core.bundle.scp.css`
  and `Modal/BlazoredModal.razor.js`).
- Changes from upstream: `@namespace` in `_Imports.razor`, the JS import path in `BlazoredModal.razor`.
