# Security Review Checklist

## Input Validation

### User Input

- [ ] All inputs are validated using Data Annotations or FluentValidation
- [ ] Model binding uses strongly-typed DTOs, not raw strings
- [ ] Input length limits enforced via `[MaxLength]` / `[StringLength]`
- [ ] File uploads validate MIME type, extension, size, and content (magic bytes)

### URL Parameters

- [ ] Route and query parameters are validated with model binding constraints
- [ ] `[FromRoute]`, `[FromQuery]`, `[FromBody]` used explicitly
- [ ] Redirect URLs validated against an allowlist (avoid open redirects)

## Cross-Site Scripting (XSS)

### Output Encoding

- [ ] Razor views auto-encode output; raw `@Html.Raw()` usage is justified
- [ ] User-generated content is HTML-encoded before rendering
- [ ] JSON responses use `System.Text.Json` with safe defaults
- [ ] URLs validated before use in `href`/`src` attributes

### DOM Manipulation

- [ ] Blazor components avoid `MarkupString` with untrusted input
- [ ] JavaScript interop does not pass raw user input to `eval` or `innerHTML`

## Authentication & Authorization

### ASP.NET Core Identity / JWT

- [ ] Passwords hashed with `IPasswordHasher<T>` (PBKDF2 by default)
- [ ] JWT tokens validated: issuer, audience, expiry, and signature
- [ ] Refresh tokens stored securely and rotated on use
- [ ] `[Authorize]` attribute applied to all protected controllers/actions
- [ ] Role and policy checks use `IAuthorizationService` server-side

### Session Management

- [ ] Sessions use `SameSite=Strict` and `HttpOnly` cookies
- [ ] Anti-forgery tokens (`[ValidateAntiForgeryToken]`) on state-changing forms
- [ ] Session expires after a reasonable idle timeout
- [ ] Session invalidated on logout (`HttpContext.SignOutAsync()`)

### Access Control

- [ ] Authorization policies defined and applied (not just roles)
- [ ] Resource-based authorization used where ownership matters
- [ ] Admin endpoints protected with dedicated policies
- [ ] No authorization logic on the client side only

## Data Protection

### Sensitive Data

- [ ] Passwords never logged; `[DataProtection]` API used for tokens
- [ ] API keys and secrets stored in `IConfiguration` / `Azure Key Vault`, not hardcoded
- [ ] `[JsonIgnore]` or response DTOs prevent leaking sensitive fields
- [ ] Error middleware returns generic messages in production (`UseExceptionHandler`)

### Storage & Cookies

- [ ] ASP.NET Core Data Protection keys stored securely (not in-memory for distributed apps)
- [ ] Cookies use `Secure`, `HttpOnly`, and appropriate `SameSite` policy
- [ ] EF Core uses parameterized queries by default; raw SQL uses `FromSqlRaw` with parameters
- [ ] Sensitive data encrypted at rest (e.g., Azure Storage encryption, SQL TDE)

## Dependencies

### NuGet Packages

- [ ] Packages sourced from trusted feeds (nuget.org or private feed)
- [ ] No known vulnerabilities (`dotnet list package --vulnerable`)
- [ ] Versions pinned in `Directory.Packages.props` (CPM enabled)
- [ ] Minimal package footprint; unused packages removed

### Transitive Dependencies

- [ ] `dotnet restore` with lock file (`packages.lock.json`) enabled
- [ ] Dependabot or Renovate configured for automated updates

## Common Vulnerabilities

### SQL Injection

```csharp
// BAD: string interpolation in raw SQL
var users = context.Users
    .FromSqlRaw($"SELECT * FROM Users WHERE Id = {userId}")
    .ToList();

// GOOD: parameterized raw SQL
var users = context.Users
    .FromSqlRaw("SELECT * FROM Users WHERE Id = {0}", userId)
    .ToList();

// BEST: use EF Core LINQ — parameterized automatically
var user = await context.Users.FindAsync(userId);
```

### Path Traversal

```csharp
// BAD: direct user-supplied filename
var file = File.ReadAllBytes($"uploads/{filename}");

// GOOD: validate and sanitize
var safeName = Path.GetFileName(filename); // strips directory components
var fullPath = Path.GetFullPath(Path.Combine("uploads", safeName));
if (!fullPath.StartsWith(Path.GetFullPath("uploads")))
    return BadRequest();
var file = File.ReadAllBytes(fullPath);
```

### Insecure Direct Object Reference (IDOR)

```csharp
// BAD: trust the user-supplied ID
[HttpGet("{id}")]
public async Task<IActionResult> GetDocument(Guid id)
    => Ok(await _repo.GetByIdAsync(id));

// GOOD: verify ownership with resource-based authorization
[HttpGet("{id}")]
public async Task<IActionResult> GetDocument(Guid id)
{
    var doc = await _repo.GetByIdAsync(id);
    var authResult = await _authService.AuthorizeAsync(User, doc, "OwnerPolicy");
    if (!authResult.Succeeded) return Forbid();
    return Ok(doc);
}
```

### Mass Assignment

```csharp
// BAD: binding directly to domain entity
[HttpPost]
public async Task<IActionResult> Create([FromBody] User user) { ... }

// GOOD: use a dedicated DTO / command
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateUserRequest request) { ... }
```

## Reporting Security Issues

When you find a security issue:

1. **Severity**: Critical / High / Medium / Low
2. **Location**: File path and line number
3. **Description**: What the vulnerability allows
4. **Recommendation**: How to fix it
5. **References**: CWE ID or OWASP category if applicable
