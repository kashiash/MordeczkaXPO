window.MAISTER_DATA = {
  generated: "2026-08-31T17:34:13Z",
  task: {
    title: "Reguły wyglądu z dyktowania (AI) w mordeczce",
    type: "development",
    status: "in_progress",
    description: "Reguły wyglądu trzymane w bazie + narzędzie AI, które z dyktowanego zdania tworzy regułę (typ, kryterium, rola koloru, kontekst). Polska terminologia, deploy na Proxmox i test.",
    path: ".maister/tasks/development/2026-08-31-regula-wygladu-z-dyktowania",
    current_activity: "Piszę specyfikację"
  },
  characteristics: {
    has_reproducible_defect: false,
    modifies_existing_code: true,
    creates_new_entities: true,
    involves_data_operations: true,
    ui_heavy: true
  },
  phases: [
    { id: "phase-1", name: "Analiza bazy kodu", icon_hint: "analysis", status: "completed", started: "2026-08-31T16:40:49Z", completed: "2026-08-31T17:00:21Z", skip_reason: null,
      summary: "Zero reguł sterowanych danymi w repo; silnik ConditionalAppearance już podpięty w obu hostach. Workflow.cs to gotowy wzorzec 'dane zamiast atrybutów' poza odciskiem metadanych.",
      decisions: [{ decision: "Encja reguł poza QueryMetadata/GetMetadataFingerprint", rationale: "Wzorem WorkflowDefinition — dane, nie schemat; inaczej każda reguła restartuje trzy repliki" }],
      risks: [
        "resolved: Wariant A ma zerowy ślad w repo — rozstrzygnięte na bramce, wybrany A",
        "resolved: Wariant B — ryzyko cache'owanego modelu aplikacji; wariant odrzucony",
        "Reguła może przeżyć pole, które wskazuje; wyjątek w CollectAppearanceRules wywraca cały widok",
        "Zapisy narzędzi AI idą przez INonSecuredObjectSpaceFactory — omijają bezpieczeństwo XAF",
        "Brak projektu testowego w repo — weryfikacja ręczna",
        "AddStateMachine tylko w Blazor.Server — wzorcowa funkcja Workflow jest w praktyce tylko-Blazorowa"
      ],
      artifacts: [{ path: "analysis/codebase-analysis.md", label: "Analiza bazy kodu", html: null }],
      gate: null },
    { id: "phase-2", name: "Analiza luk i zakresu", icon_hint: "analysis", status: "completed", started: "2026-08-31T17:00:21Z", completed: "2026-08-31T17:27:52Z", skip_reason: null,
      summary: "Cztery decyzje krytyczne rozstrzygnięte. Potwierdzone: graduacja cicho unieszkodliwia reguły, bo GraduationService nie emituje namespace. Kompletność CRUD 40% — brak ekranu reguł.",
      decisions: [
        { decision: "Wariant A — kontroler pod CollectAppearanceRules", rationale: "Propagacja na trzy repliki za darmo, parytet Blazor/WinForms, brak globalnego bufora" },
        { decision: "Wdrożenie (b) — zgasić trójkę i wdrożyć od nowa", rationale: "trzy-repliki.sh jest sprawdzony, nic nowego do napisania; przerwa akceptowalna na maszynie demo" },
        { decision: "Reguły dostają własny ekran w nawigacji", rationale: "Kompletność CRUD z 40% na 100%; bez tego jedyną drogą cofnięcia błędu AI jest SQL w bazie produkcyjnej" },
        { decision: "Graduacja — ostrzeżenie ORAZ migracja TargetTypeName", rationale: "FullName zmienia się z RuntimeEntities.X na Module.BusinessObjects.X; sam fallback na nazwę krótką odrzucony przez kolizje nazw" },
        { decision: "Kolor przez rolę semantyczną, nie ARGB", rationale: "Reguła z ARGB jest związana z jedną skórką; modelowi łatwiej trafić w rolę niż w hex" },
        { decision: "Jeden walidator w trzech miejscach, niesprawna reguła WIDOCZNA", rationale: "We wszystkich czterech aplikacjach referencyjnych zepsuta reguła milknie po cichu" }
      ],
      risks: [
        "Transfer paczki 223 MB jest wg README-wdrozenie.md §1 blokowany dla agenta — ten krok wykona użytkownik",
        "Updater.cs woła CreateDefaultRole() tylko w #if !RELEASE — jeśli obraz powstaje w Release, nadania uprawnień nigdy się nie wykonują. NIEZWERYFIKOWANE",
        "Dokumentacja sprzeczna: bezprzerwy/README.md opiera uzasadnienie na zmiennej XAF_UPDATE_DB, której nie ma",
        "delete_entity jest dostępne jako narzędzie AI — osierocenie encji osiągalne z tego samego czatu, który tworzy reguły"
      ],
      artifacts: [{ path: "analysis/gap-analysis.md", label: "Analiza luk", html: null }],
      gate: { question: "Cztery decyzje krytyczne", answer: "Wariant A · wdrożenie (b) · ekran reguł tak · graduacja: ostrzeżenie + migracja" } },
    { id: "phase-4", name: "Makiety interfejsu", icon_hint: "spec", status: "completed", started: "2026-08-31T17:27:52Z", completed: "2026-08-31T17:34:13Z", skip_reason: null,
      summary: "Sześć ekranów po polsku w układzie XAF: lista reguł ze stanem, dwie zakładki szczegółów z panelem diagnozy, dwa przepływy w czacie (udany i odrzucony przez walidator), efekt na liście faktur.",
      decisions: [{ decision: "Makiety wiążą się z układem XAF, nie z własnym motywem", rationale: "Projekt nie ma systemu projektowego — site.css to 30 linii szablonu, zero zmiennych CSS" }],
      risks: ["Nie ustalono, czy motyw DevExpress jest przełączalny per użytkownik — od tego zależy, czy paleta ról musi być jedna dla obu motywów"],
      artifacts: [
        { path: "analysis/design-context/INDEX.md", label: "Spis ekranów", html: null },
        { path: "analysis/design-context/design-resources.md", label: "Zasoby projektowe", html: null },
        { path: "analysis/design-context/mockups/lista-regu-wygl-du.html", label: "Lista reguł", html: "analysis/design-context/mockups/lista-regu-wygl-du.html" },
        { path: "analysis/design-context/mockups/regu-a-zak-adka-definicja.html", label: "Reguła — Definicja", html: "analysis/design-context/mockups/regu-a-zak-adka-definicja.html" },
        { path: "analysis/design-context/mockups/regu-a-zak-adka-wygl-d.html", label: "Reguła — Wygląd", html: "analysis/design-context/mockups/regu-a-zak-adka-wygl-d.html" },
        { path: "analysis/design-context/mockups/czat-utworzenie-regu-y-z-dyktowania.html", label: "Czat — utworzenie", html: "analysis/design-context/mockups/czat-utworzenie-regu-y-z-dyktowania.html" },
        { path: "analysis/design-context/mockups/czat-walidacja-odrzuca-bubla.html", label: "Czat — odmowa", html: "analysis/design-context/mockups/czat-walidacja-odrzuca-bubla.html" },
        { path: "analysis/design-context/mockups/efekt-lista-faktur.html", label: "Efekt na fakturach", html: "analysis/design-context/mockups/efekt-lista-faktur.html" }
      ],
      gate: null },
    { id: "phase-5", name: "Specyfikacja", icon_hint: "spec", status: "in_progress", started: "2026-08-31T17:34:13Z", completed: null, skip_reason: null, summary: null, decisions: [], risks: [], artifacts: [], gate: null },
    { id: "phase-6", name: "Audyt specyfikacji", icon_hint: "spec", status: "pending", started: null, completed: null, skip_reason: null, summary: null, decisions: [], risks: [], artifacts: [], gate: null },
    { id: "phase-7", name: "Plan wdrożenia", icon_hint: "plan", status: "pending", started: null, completed: null, skip_reason: null, summary: null, decisions: [], risks: [], artifacts: [], gate: null },
    { id: "phase-8", name: "Implementacja", icon_hint: "code", status: "pending", started: null, completed: null, skip_reason: null, summary: null, decisions: [], risks: [], artifacts: [], gate: null },
    { id: "phase-10", name: "Wybór weryfikacji", icon_hint: "verify", status: "pending", started: null, completed: null, skip_reason: null, summary: null, decisions: [], risks: [], artifacts: [], gate: null },
    { id: "phase-11", name: "Weryfikacja i naprawa", icon_hint: "verify", status: "pending", started: null, completed: null, skip_reason: null, summary: null, decisions: [], risks: [], artifacts: [], gate: null },
    { id: "phase-12", name: "Testy E2E", icon_hint: "verify", status: "pending", started: null, completed: null, skip_reason: null, summary: null, decisions: [], risks: [], artifacts: [], gate: null },
    { id: "phase-13", name: "Dokumentacja użytkownika", icon_hint: "docs", status: "pending", started: null, completed: null, skip_reason: null, summary: null, decisions: [], risks: [], artifacts: [], gate: null },
    { id: "phase-14", name: "Domknięcie i wdrożenie", icon_hint: "done", status: "pending", started: null, completed: null, skip_reason: null, summary: null, decisions: [], risks: [], artifacts: [], gate: null }
  ],
  verification: { status: null, issues: [], fixes: [], reverify_count: 0 }
};
