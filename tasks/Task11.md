# Task 11 — Implementar Middleware, Filters e Error Handling Global

## Contexto

A API precisa de middleware e filters cross-cutting: tratamento global de exceções (`GlobalExceptionFilter`), correlation ID para rastreabilidade, headers de segurança e logging de requests. O `GlobalExceptionFilter` está vazio, o `IdempotencyFilter` está vazio, e o `RateLimitAttribute` está comentado. Além disso, falta middleware de correlation ID e security headers.

## Arquivos a Alterar/Criar

- `src/services/AuctionKings.API/Filters/GlobalExceptionFilter.cs` — implementar `IExceptionFilter`: mapeia `ValidationException` → 400, `NotFoundException` → 404, `ConcurrencyException` → 409, exceções não tratadas → 500 com ProblemDetails
- `src/services/AuctionKings.API/Filters/IdempotencyFilter.cs` — implementar `IAsyncActionFilter`: extrai header `Idempotency-Key`, injeta no command
- `src/services/AuctionKings.API/Middleware/CorrelationIdMiddleware.cs` — gera/propaga `X-Correlation-Id` header em requests e responses
- `src/services/AuctionKings.API/Middleware/SecurityHeadersMiddleware.cs` — adiciona `X-Content-Type-Options`, `X-Frame-Options`, `Strict-Transport-Security`, `X-XSS-Protection`
- `src/services/AuctionKings.API/Middleware/RequestLoggingMiddleware.cs` — loga method, path, status code, tempo de execução com structured logging
- `src/services/AuctionKings.API/Program.cs` — registrar middleware na pipeline (ordem: CorrelationId → SecurityHeaders → RequestLogging → Routing → Auth → Endpoints)

## Critérios de Aceite

- Todas as exceções não tratadas retornam `ProblemDetails` (RFC 7807) com correlation ID
- `ValidationException` retorna 400 com lista de erros de validação no body
- Exceções de concorrência (EF Core `DbUpdateConcurrencyException`) retornam 409 Conflict
- Correlation ID é gerado se não presente no request, propagado no response e logado em todo request
- Security headers presentes em toda response
- Request logging inclui: method, path, status code, duration em ms, correlation ID
- Nenhum stack trace vazado em ambiente de produção (apenas em Development)

## Testes Obrigatórios

- `GlobalExceptionFilter_ValidationException_ShouldReturn400`
- `GlobalExceptionFilter_NotFoundException_ShouldReturn404`
- `GlobalExceptionFilter_ConcurrencyException_ShouldReturn409`
- `GlobalExceptionFilter_UnhandledException_ShouldReturn500WithProblemDetails`
- `CorrelationIdMiddleware_NoHeader_ShouldGenerate`
- `CorrelationIdMiddleware_WithHeader_ShouldPropagate`
- `SecurityHeadersMiddleware_ShouldAddAllHeaders`

## Riscos/Observações

- `ProblemDetails` deve usar `application/problem+json` content type
- Logging de request body deve ser opt-in e nunca logar dados sensíveis (senhas, tokens)
- Idempotency filter deve funcionar apenas com POST/PUT — não com GET/DELETE
- Middleware order no ASP.NET Core é crítico — documentar a ordem esperada

## Estimativa

**M** (Medium)

## Dependências

- **Task 07** — `IdempotencyBehavior` no pipeline (para o `IdempotencyFilter` fazer sentido end-to-end)
- **Task 10** — controllers implementados (para os filters terem endpoints onde agir)

## Status

✅ **Done** (partial — #33 and #55 implemented; remaining items in Task 11 scope pending)

## GitHub Issues

| #                                                                | Title                                       | Status     |
| ---------------------------------------------------------------- | ------------------------------------------- | ---------- |
| [#33](https://github.com/PellizzoniCode/auction-kings/issues/33) | Adicionar Idempotency (header X-Request-Id) | ✅ closed  |
| [#55](https://github.com/PellizzoniCode/auction-kings/issues/55) | Adicionar Correlation ID middleware         | ✅ closed  |
