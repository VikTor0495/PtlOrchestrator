# Convenzioni

- Italiano per commenti XML, messaggi e commit; identificatori in inglese.
- Nullable e ImplicitUsings attivi (`Directory.Build.props`). Primary constructor + campi `_x` readonly per la DI.
- Interfacce in `Service/`, implementazioni in `Service/Impl/`.
- Ogni feature con test xUnit nel progetto `tests/*.Tests` corrispondente; `dotnet test PtlOrchestrator.sln` verde prima del commit.
- Commit conventional: `feat(scope):`, `fix:`, `test:`, `docs:`, `refactor:`, `chore:`.
- **Flusso git**: lavoro su `feature/evolutiva-gui-db`, commit piccoli, `git push` dopo ogni passo (il PC può cadere). Merge su `master` solo a evolutiva completata.
- Mai segreti nel repo (API key, password DB): user-secrets / variabili d'ambiente.
