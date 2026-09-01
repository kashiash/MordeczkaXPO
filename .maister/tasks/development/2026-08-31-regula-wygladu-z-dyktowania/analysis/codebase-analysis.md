# Raport analizy kodu — reguły wyglądu z dyktowania

**Data**: 2026-08-31
**Zadanie**: Reguły wyglądu (conditional appearance) trzymane w bazie + narzędzie AI zamieniające podyktowane polskie zdanie na rekord reguły
**Opis**: Dodać do XafXPODynAssem ("mordeczka" — XAF Blazor Server + WinForms, XPO, PostgreSQL, DevExpress 26.1.4, polska lokalizacja, encje runtime kompilowane Roslynem) reguły wyglądu sterowane danymi, działające bez rekompilacji, plus narzędzie function-calling, które ze zdania typu "zrób faktury niezapłacone na czerwono na listach" tworzy rekord reguły. Cel wdrożenia: Proxmox LXC 200, trzy repliki za nginx.
**Analiza**: skill codebase-analyzer (3 agenty Explore: File Discovery, Code Analysis, Pattern Mining) + weryfikacja bezpośrednia w kodzie

> **Uwaga o ścieżkach**: agenty Explore raportowały ścieżki w formie `XafXPODynAssem.Module/...`. To jest błędne — realny układ repo ma zdublowany segment. Wszystkie ścieżki w tym raporcie są bezwzględne i zweryfikowane: `/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/...`.

---

## TL;DR

W repo **nie ma dziś żadnej reguły wyglądu sterowanej danymi** — cały conditional appearance to cztery statyczne atrybuty `[Appearance]` w dwóch plikach. Sam silnik DevExpress jest już jednak podpięty (`ConditionalAppearanceModule` + `.AddConditionalAppearance()` w obu hostach), więc dokładamy źródło reguł, nie silnik.
Istnieje **gotowy wzorzec "dane zamiast atrybutów"** do skopiowania niemal linia w linię: `WorkflowDefinition` — encja XPO wpinana w mechanizm DevExpress normalnie sterowany konfiguracją, z leniwym `XafTypesInfo.FindTypeInfo`, i celowo **poza** odciskiem metadanych, więc nie wymaga restartu replik.
Decyzja architektoniczna nie jest rozstrzygnięta: **Wariant A** (`AppearanceController.CollectAppearanceRules`) ma zerowy ślad w tym repo, ale czyta z bazy przy każdej aktywacji widoku; **Wariant B** (wstrzykiwanie `IModelAppearanceRule` do modelu aplikacji) ma tu udowodniony mechanizm wstrzykiwania, ale nieudowodniony ładunek i ryzyko cache'owania modelu.
Warstwa AI jest gotowa — dokładamy dwie metody do `CreateTools()`, żadnej nowej rejestracji DI.

## Kluczowe decyzje

- **Reguły wyglądu trzymamy jako czyste dane, na wzór `WorkflowDefinition`, a NIE jako atrybuty wypalane w kod generowany Roslynem** — tylko ścieżka "dane czytane na żywo" spełnia wymóg "bez rekompilacji"; wypalanie w atrybuty wciąga regułę w `QueryMetadata`/fingerprint i restartuje wszystkie trzy repliki przy każdym podyktowanym zdaniu.
- **Encja reguły musi mieć strażnika typu wzorowanego na `IStateMachine.Active` z `Workflow.cs`** — zweryfikowany precedens w tym repo: reguła jest "aktywna" tylko wtedy, gdy `ResolveTargetType() != null`; komentarz w kodzie wprost ostrzega, że bez tego "kazdy widok w aplikacji dostalby NullReferenceException".
- **Typ docelowy przechowujemy jako `FullName` w stringu i rozwiązujemy przez `XafTypesInfo.FindTypeInfo(name)?.Type`, nigdy przez `Type.GetType`** — typy runtime są kompilowane Roslynem w pamięci, `Type.GetType` ich nie widzi. Wzorzec: `Workflow.cs:129`.
- **Narzędzie AI idzie w dwuetapowej konwencji repo `validate_* → build_*`** (jak `ValidateReportSpec`/`BuildReport`) — walidacja tylko do odczytu zwraca problemy tekstem i każe LLM-owi dopytać, zamiast zgadywać.
- **Nie kopiujemy z repo siostrzanych: dopasowania typu przez `StartsWith` ani statycznego eventu `RulesCommitted`** — oba są nie do utrzymania w tym wdrożeniu (uzasadnienie w sekcji ryzyk).

## Otwarte pytania / ryzyka

- **[ROZSTRZYGA WARIANT]** Czy nowa reguła dociera do repliki, która nigdy nie obsłużyła zapisu i nie była restartowana? Kryterium akceptacji jest jedno: **nowa reguła musi dotrzeć do trzech replik, które nigdy nie widziały zapisu, bez restartu i bez rekompilacji.** Test wynikowy (nie da się go oszukać): wstawić wiersz `AppearanceRule` bezpośrednio do PostgreSQL, po czym otworzyć świeżą sesję przeglądarki na replice, która nie obsłużyła zapisu i nie była restartowana — czy kolorowanie się pojawia? Ten test działa identycznie na prototypie każdego z wariantów i nie wymaga wnioskowania o wewnętrznych mechanizmach DevExpress. Licznik w `AIChatDetailViewUpdater.UpdateNode` zostawić najwyżej jako tanią diagnostykę — sam w sobie nie rozstrzyga, bo mierzy, czy przebiega **generator** węzłów; każdy obwód może budować własny `ModelApplication` z już wygenerowanych i zacache'owanych węzłów, przez co licznik wygląda „per obwód", choć żaden nowy wiersz z bazy do modelu nie trafia.
- Czy „graduacja" encji (Runtime → Graduating → Compiled, `GraduationService.cs`) zachowuje `FullName` typu? Jeśli przy przejściu na klasę statyczną zmienia się przestrzeń nazw, każda zapisana reguła cicho przestaje działać — bez błędu, po prostu przestaje kolorować.
- Reguła może przeżyć właściwość, którą celuje (użytkownik usuwa pole przez AI). Dotyczy **obu** wariantów.
- Zapisy narzędzia AI idą przez `INonSecuredObjectSpaceFactory` — całkowicie omijają system bezpieczeństwa XAF. Dowolny użytkownik czatu może w ten sposób stworzyć regułę na dowolnej encji.
- **Brak jakiegokolwiek projektu testowego w repo.** Jedyny plik z „test" w nazwie to `/Users/jacek/Projects/Brekhof/XafXPODynAssem/test_runtime.py`. Weryfikacja tej funkcji będzie ręczna/na żywo.
- Specyfikacja pól encji i kształt kontrolera z sekcji Pattern Mining pochodzą z **repozytoriów siostrzanych (Fleetman/DataDrive/Brekhof), nieodczytanych w tej sesji** — traktować jako propozycję do weryfikacji, nie jako fakt o tym kodzie.

---

## Podsumowanie

Aplikacja to trójprojektowe rozwiązanie XAF (Module / Blazor.Server / Win) budujące encje runtime z metadanych `CustomClass`/`CustomField`, kompilowane Roslynem, z asystentem AI (LLMTornado) manipulującym tymi metadanymi przez function calling. Dziś conditional appearance jest w 100% atrybutowy i kompilowany — nie da się nim obsłużyć encji generowanych w runtime. Nie ma też żadnego kodu do dyktowania/mowy: „podyktowane zdanie" to zwykły tekst wchodzący istniejącym potokiem `AIChatService.AskAsync`.

Najbliższym i najlepiej dopasowanym precedensem jest funkcja Workflow (maszyna stanów): pokazuje, jak wpiąć encję XPO w mechanizm DevExpress normalnie sterowany atrybutami, jak leniwie rozwiązywać typy runtime i — co najważniejsze dla wdrożenia na trzy repliki — jak zrobić to tak, żeby zmiana **nie** wymagała restartu.

---

## Zidentyfikowane pliki

### Pliki pierwszorzędne

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/BusinessObjects/Workflow.cs** (322 linie) — WZORZEC NR 1
- Definiuje `WorkflowDefinition`/`WorkflowState`/`WorkflowTransition` jako zwykłe klasy `BaseObject` (XPO), które wpinają się w przekrojowy mechanizm DevExpress (`IStateMachine`) normalnie sterowany konfiguracją.
- Zawiera dokładnie te wzorce, których wymaga encja reguły wyglądu: `ResolveTargetType()` z `typesInfo.FindTypeInfo(targetTypeName)?.Type` (linia 129, **zweryfikowane**), `TargetTypeName` jako string, strażnik `Active` sprawdzający, że typ **i** właściwość dalej się rozwiązują, `[Association(...), Aggregated]` na kolekcjach dzieci, polskie `[XafDisplayName]` na każdej właściwości.
- Kluczowy komentarz w kodzie (ok. linii 140): `IStateMachine.Active` musi zwracać false przy nierozwiązanym typie, bo inaczej „kazdy widok w aplikacji dostalby NullReferenceException". To dokładnie ten sam problem, który czeka regułę wyglądu wskazującą nieistniejące pole.

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/Services/SchemaAIToolsProvider.cs** (2570 linii) — WZORZEC NR 2 (narzędzie AI)
- `CreateTools()` (linie 40–69, **zweryfikowane**) rejestruje narzędzia przez `AIFunctionFactory.Create(Metoda, "nazwa_narzedzia")`. Aktualnie 19 narzędzi w grupach: Read, Write, Role, Report, Workflow. Nowe narzędzia dokładamy tutaj — nic więcej.
- `GetTornadoTools()` konwertuje na `Tool`/`ToolFunction` LLMTornado, korzystając z `fn.JsonSchema`.
- `ScopedObjectSpace` + `CreateObjectSpace()` (linie 87–123, **zweryfikowane**) — każde narzędzie zapisujące tworzy nowy zakres DI i **niezabezpieczoną** przestrzeń obiektów przez `INonSecuredObjectSpaceFactory`, kończy `scope.Os.CommitChanges()`.
- Narzędzia workflow (`CreateWorkflow`, `AddWorkflowState`, `AddWorkflowTransition`, `DescribeWorkflow`, `ListWorkflows`) to najbliższy analog dla `create_appearance_rule`: `[Description(...)]` na metodzie i na każdym parametrze (to dosłownie prompt, który widzi LLM), walidacja przed jakimkolwiek zapisem, zwrot markdownowego stringa z instrukcją „odśwież F5, deploy niepotrzebny".
- Parametry są ZAWSZE prymitywne (string/bool/int); złożone wejście wchodzi jako surowy JSON i jest ręcznie deserializowane do prywatnych DTO na końcu pliku (`FieldDefinition`, `TransitionDefinition`, `ModificationsPayload`).
- Narzędzia nigdy nie rzucają przez granicę — każde ciało w `try/catch` zwracającym `$"Error ...: {ex.Message}"`.

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/AIChatDetailViewUpdater.cs** (50 linii) — DOWÓD MECHANIZMU DLA WARIANTU B
- `ModelNodesGeneratorUpdater<ModelViewsNodesGenerator>`; `UpdateNode(ModelNode node)` rzutuje na `IModelViews`, znajduje `views["AIChat_DetailView"] as IModelDetailView`, woła `dv.Items.AddNode<IModelAIChatViewItem>(chatItemId)`, usuwa węzeł `Oid` i przebudowuje `dv.Layout`. **Odczytane w całości.**
- Zarejestrowany w `Module.cs` w `AddGeneratorUpdaters` (**zweryfikowane**): `updaters.Add(new AIChatDetailViewUpdater());`.
- To dowodzi, że **mechanizm wstrzykiwania węzłów do modelu aplikacji jest tu sprawdzony**. Nie dowodzi, że działa z węzłami wyglądu — `IModelAppearanceRule` nie występuje w repo ani raz.

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/Module.cs** (426 linii) — PUNKT INTEGRACJI
- Linie 95–104 (**zweryfikowane**): rejestracja typów trwałych przez `AdditionalExportedTypes.Add(...)`, w tym trzy typy Workflow. Tu dochodzi nowa encja reguły.
- Linia 107 (**zweryfikowane**): `RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.ConditionalAppearance.ConditionalAppearanceModule));` — moduł jest już wczytany, tylko reaguje wyłącznie na atrybuty `[Appearance]`.
- `AddGeneratorUpdaters` (**zweryfikowane**) — miejsce rejestracji ewentualnego updatera z Wariantu B.
- `GetMetadataFingerprint` / `QueryMetadata` / `ComputeFingerprint` (od ok. linii 264 do końca pliku, 425 linii; odczytane fragmenty 264–300 i 320–345): `QueryMetadata` czyta surowym SQL-em (Npgsql) **wyłącznie** tabele `CustomClass` i `CustomField` — potwierdzone komentarzem dokumentacyjnym i treścią `SELECT`-ów. `WorkflowDefinition` i spółka są poza odciskiem — to precedens, na którym stoi całe „bez restartu".

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/Services/SchemaDiscoveryService.cs** — PUNKT INTEGRACJI PROMPTU
- `GenerateSystemPrompt` buduje prompt systemowy wstrzykiwany przed każdą turą AI. Ma już sekcje „## Workflows (State Machines)" i „## Reports" napisane jako jawne reguły w języku naturalnym. Nowa sekcja „## Appearance Rules" musi trafić tutaj, w tej samej konwencji.
- To tu żyje jedyna „bramka" na człowieka: instrukcje „Always confirm with the user before executing any schema change" oraz „NEVER guess the parts the user did not say... ask ONE specific question about it and wait for the answer". Bramka jest wyłącznie promptowa — nie ma popupu, podglądu ani dwufazowego commitu.

### Pliki powiązane

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Blazor.Server/Startup.cs**
- Linia 53 (**zweryfikowane**): `.AddConditionalAppearance()`.
- `.AddStateMachine(options => options.StateMachineStorageType = typeof(WorkflowDefinition))` (**zweryfikowane** jako wywołanie, numer linii pomijam) — wzorzec rejestracji własnego typu magazynującego dla mechanizmu DevExpress.
- `EarlyBootstrap()` wołany przed `services.AddXaf(...)` — Roslyn kompiluje typy runtime zanim XAF się zainicjalizuje.

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Win/Startup.cs**
- Linia 30 (**zweryfikowane**): `.AddConditionalAppearance()` — silnik jest podpięty także w hoście WinForms.
- **`.AddStateMachine` NIE jest tu odbite** (**zweryfikowane**: grep za `AddStateMachine`/`StateMachineStorageType` w tym pliku — 0 trafień). Czyli funkcja Workflow, na której wzorujemy encję, jest w praktyce **tylko-Blazorowa**. Konsekwencja dla planu: albo świadomie decydujemy, że reguły wyglądu też są tylko-Blazorowe, albo rejestrację trzeba odbić w obu hostach — czego istniejący precedens nie robi. Do rozstrzygnięcia w specyfikacji.

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/BusinessObjects/CustomClass.cs** (123 linie)
- Linie 23–32 (**zweryfikowane**): jedyne dwa statyczne `[Appearance]` na encji w całej aplikacji — `Criteria = "Status = 2"` / `"Status = 1"`, `Context = "ListView"`, `FontColor = "Gray"/"Orange"`, `FontStyle = DXFontStyle.Italic`. Negatywny przykład, który nowa funkcja zastępuje: działa tylko dla typu znanego w czasie kompilacji.

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/BusinessObjects/ApplicationUserLoginInfo.cs**
- Linie 18 i 25 (**zweryfikowane**): `[Appearance(..., Enabled = false, Criteria = "...", Context = "DetailView")]` — jedyne w repo użycie `Enabled` (blokada edycji), czego wszystkie trzy repo siostrzane nie potrafią zrobić z bazy.

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Blazor.Server/Services/ReplicaSyncService.cs** (269 linii) + `ReplicaDrainMiddleware.cs` (86 linii)
- Mechanizm propagacji na trzy repliki: opt-in przez `REPLIKA_INDEKS`, sondowanie odcisku metadanych co `REPLIKA_SONDA` sekund, walidacja `ValidateRuntimeMetadata`, kolejka po indeksie, czekanie na bezczynność, `RestartService.RequestRestart()` → kod wyjścia 42 → supervisor restartuje.
- **Ta ścieżka jest zarezerwowana dla zmian schematu.** Reguły wyglądu nie mają w niej być.

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/Controllers/CustomFieldDeleteGuardController.cs** (68 linii) + `Module/Validation/FieldTypeChangeGuard.cs`
- Blokuje usunięcie `CustomField`, jeśli pole jest w użyciu (`FieldTypeChangeGuard.IsFieldRemovalSafe`). To jest miejsce, gdzie należy dołożyć sprawdzenie „czy to pole jest wskazywane przez jakąś regułę wyglądu".

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/Services/AIServiceCollectionExtensions.cs** (41 linii)
- `SchemaAIToolsProvider` jest singletonem; nowe metody-narzędzia nie wymagają żadnej nowej rejestracji DI.

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/DatabaseUpdate/Updater.cs** (138 linii)
- `CreateDefaultRole()`/`CreateAdminRole()` + `GrantNavigationHubPermissions` — idempotentny wzorzec nadawania uprawnień nowemu typowi trwałemu; tu też ewentualny seed reguł domyślnych.

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/Services/AIChatService.cs** (313 linii)
- `AskAsync()` woła `RefreshSystemPrompt()` przed każdą turą i prowadzi pętlę wywołań narzędzi (`ExecuteToolAsync(fc.Name, fc.Arguments)` → `function.InvokeAsync(new AIFunctionArguments(dict))`), ograniczoną przez `MaxToolIterations` (domyślnie 10).

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/Services/GraduationService.cs**
- Cykl Runtime → Graduating → Compiled. Reguły muszą przeżyć graduację (patrz otwarte pytanie o `FullName`).

**/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/Controllers/WorkflowCommitController.cs**, `GraduateController.cs`, `GraduationWarningController.cs`
- Precedensy `ViewController` w tym repo: kontrolery leżą w `Module/Controllers/*.cs` i są wykrywane automatycznie przez skanowanie XAF — bez ręcznej rejestracji. `WorkflowCommitController` podpina się w `OnActivated` pod zdarzenie kontrolera DevExpress (`Frame.GetController<StateMachineController>().TransitionExecuted += ...`) — to dokładnie kształt, jakiego wymaga Wariant A.

---

## Aktualna funkcjonalność

### Conditional appearance — dziś

Silnik jest podpięty i żywy, ale karmiony wyłącznie atrybutami:

| Element | Stan | Dowód |
|---|---|---|
| Pakiet `DevExpress.ExpressApp.ConditionalAppearance` 26.1.4 | jest | `XafXPODynAssem.Module.csproj` |
| `RequiredModuleTypes.Add(typeof(ConditionalAppearanceModule))` | jest | `Module.cs:107` |
| `.AddConditionalAppearance()` (Blazor + Win) | jest | `Blazor.Server/Startup.cs:53`, `Win/Startup.cs` |
| Reguły z atrybutów | 4 sztuki | `CustomClass.cs:23,28`; `ApplicationUserLoginInfo.cs:18,25` |
| Reguły z bazy | **ZERO** | grep za `AppearanceController`, `CollectAppearanceRules`, `IModelAppearanceRule`, `IAppearanceRuleProperties` — **0 trafień w całym repo** |
| Kod mowy/dyktowania | **ZERO** | brak w repo; „dyktowanie" = tekst w istniejącym czacie |

### Przepływ danych — docelowy (obie warianty wspólnie)

```
użytkownik dyktuje/pisze zdanie po polsku
   → AIChatService.AskAsync (prompt systemowy z SchemaDiscoveryService)
   → LLM wybiera validate_appearance_rule (read-only) → problemy albo OK
   → LLM wywołuje build_appearance_rule
   → SchemaAIToolsProvider: nowy scope DI + INonSecuredObjectSpaceFactory
   → CreateObject<AppearanceRule>() + CommitChanges()
   → [WARIANT A] przy następnej aktywacji widoku kontroler czyta reguły z bazy
     [WARIANT B] przy następnej budowie modelu aplikacji węzły trafiają do modelu
   → DevExpress AppearanceController maluje wiersze
```

---

## FORK ARCHITEKTONICZNY — do decyzji użytkownika

Dwa agenty zaproponowały **różne mechanizmy**. Nie rozstrzygam — poniżej dowody po obu stronach.

### Wariant A — `ViewController` + `AppearanceController.CollectAppearanceRules`

Encja XPO implementuje `IAppearanceRuleProperties`. Kontroler w `OnActivated` sięga po `Frame.GetController<AppearanceController>()`, wykonuje sekwencję `ResetRulesCache()` → subskrypcja `CollectAppearanceRules` → `Refresh()`, a w handlerze dorzuca do `e.AppearanceRules` reguły wczytane wcześniej z bazy.

**Dowody ZA:**
- Reguły są czytane z bazy **przy każdej aktywacji widoku**. Wszystkie trzy repliki czytają tę samą bazę, więc propagacja między replikami jest darmowa — żadnej sygnalizacji, żadnego restartu, żadnego dopisywania do fingerprintu.
- Kształt kontrolera pasuje do istniejących precedensów w tym repo: `WorkflowCommitController` już podpina się w `OnActivated` pod zdarzenie kontrolera DevExpress.
- Mechanizm jest sprawdzony w praktyce w repo siostrzanych (Fleetman `CustomApperance.cs` + `CustomApperanceViewControler.cs`, DataDrive `AdditionalAppearanceRule.cs`, Brekhof).
- XPO pozwala, żeby obiekt trwały implementował `IAppearanceRuleProperties` bezpośrednio — bez klasy-adaptera, której wymaga EF Core.

**Dowody PRZECIW / koszty:**
- **Mechanizm ma zerowy ślad w tym repo.** Grep za `AppearanceController` i `CollectAppearanceRules` nie zwraca nic. „Sprawdzony" znaczy tu: sprawdzony w innych repozytoriach, których nie odczytano w tej sesji.
- `CollectAppearanceRules` odpala się **raz na element UI** — zapytanie do bazy w handlerze jest katastrofą wydajnościową; reguły trzeba wczytać raz do pola w `OnActivated`.
- Nieobsłużony wyjątek w handlerze wywraca cały widok (patrz ryzyka).
- Sekwencja `ResetRulesCache() → subskrypcja → Refresh()` jest nienegocjowalna. Pominięcie `ResetRulesCache()` sprawia, że `CollectAppearanceRules` w ogóle się nie odpala, bo DevExpress zacache'ował „brak reguł dla tego typu" z pierwszej, bezregułowej aktywacji widoku.
- Wymaga `Enabled`/`AppearanceItemType` jako właściwości-zaślepek (`[NonPersistent]`), żeby zadowolić interfejs.

### Wariant B — `ModelNodesGeneratorUpdater<ModelViewsNodesGenerator>` + `IModelAppearanceRule`

Updater modelu wstrzykuje węzły `IModelAppearanceRule` do modelu aplikacji (`dv.AppearanceRules.AddNode<IModelAppearanceRule>(id)` z `Criteria`, `TargetItems`, `FontColor`, `Visibility`), a DevExpress ewaluuje je natywnie.

**Dowody ZA:**
- **Mechanizm wstrzykiwania węzłów jest w tym repo udowodniony i działa w produkcji**: `AIChatDetailViewUpdater.cs` robi dokładnie to (`dv.Items.AddNode<IModelAIChatViewItem>`), zarejestrowany w `Module.cs` `AddGeneratorUpdaters`.
- Zero własnego interpretera `CriteriaOperator` — całą ewaluację robi natywny `ConditionalAppearanceModule`.
- Celuje w widok po stringowym ID, więc działa jednolicie na typach statycznych i tych z Roslyna.
- Oficjalnie wspierana ścieżka DevExpress.

**Dowody PRZECIW / koszty:**
- **Udowodniony jest mechanizm, nie ładunek.** `IModelAppearanceRule` nie występuje w repo ani razu. To, że updater potrafi dodać `IModelAIChatViewItem` do `Items`, nie dowodzi, że ta sama ścieżka obsłuży węzły wyglądu.
- **Model aplikacji jest budowany raz i cache'owany.** Jeśli w tym setupie Blazor Server cache jest procesowy, nowo podyktowana reguła nie pojawi się aż do restartu — co przekreśla wymóg „działa bez rekompilacji/restartu". Sam agent Code Analysis to zastrzegł: „Confirm empirically whether your Blazor Server setup rebuilds the Application Model per-circuit or process-wide-cached".
- Na trzech replikach problem się mnoży: replika, która nie obsłużyła zapisu, przebuduje model dopiero wtedy, gdy coś ją do tego zmusi. Jedyne dostępne dziś „zmuszenie" to ścieżka restartu z `ReplicaSyncService` — a ta jest napędzana fingerprintem metadanych, do którego reguł wyglądu dokładać nie wolno.
- **Host WinForms jest osobnym, niezależnym sposobem, w jaki Wariant B nie spełnia wymogu.** Klient WinForms to długo żyjący proces desktopowy — reguła w postaci węzła modelu najprawdopodobniej wymaga restartu klienta, żeby się pojawić. Wariant A zachowuje się w obu hostach identycznie (kontroler wykrywany automatycznie, czyta przy każdej aktywacji widoku).

### Kryterium rozstrzygające

Kryterium akceptacji dla obu wariantów jest to samo: **podyktowana reguła musi dotrzeć do trzech replik, które nigdy nie widziały zapisu, bez restartu i bez rekompilacji.**

**Test wynikowy** (rozstrzygający, niezależny od wewnętrznych mechanizmów DevExpress): wstawić wiersz `AppearanceRule` bezpośrednio do PostgreSQL — z pominięciem aplikacji — a następnie otworzyć świeżą sesję przeglądarki na replice, która nie obsłużyła zapisu i nie była restartowana. Jeśli kolorowanie się pojawia, wariant spełnia wymóg; jeśli nie — nie spełnia. Test wykonać na prototypie każdego z wariantów; dla Wariantu B powtórzyć go też na kliencie WinForms.

**Diagnostyka pomocnicza** (nie rozstrzyga sama z siebie): licznik/log w `AIChatDetailViewUpdater.UpdateNode` pokazuje, czy generator węzłów przebiega raz na proces, czy raz na obwód. Uwaga na pułapkę: każdy obwód może budować własny `ModelApplication` z już wygenerowanych i zacache'owanych węzłów — wtedy licznik wygląda „per obwód", a mimo to nowy wiersz z bazy nigdy nie trafia do modelu.

---

## Zależności

### Co ta funkcja wykorzystuje

- `DevExpress.ExpressApp.ConditionalAppearance` 26.1.4 — silnik, już podpięty.
- `DevExpress.Persistent.BaseImpl.BaseObject` (XPO) — klasa bazowa encji.
- `XafTypesInfo` / `ITypesInfo.FindTypeInfo` — rozwiązywanie typów runtime.
- `Microsoft.Extensions.AI` `AIFunctionFactory` + LLMTornado — warstwa narzędzi AI.
- `INonSecuredObjectSpaceFactory` — zapisy z narzędzi AI.
- `CriteriaOperator.Parse` — walidacja kryteriów.
- Npgsql / PostgreSQL — magazyn.

### Co będzie zależeć od tej funkcji

- Każdy widok listy i szczegółu w aplikacji (Wariant A: przez aktywację kontrolera; Wariant B: przez model).
- `CustomFieldDeleteGuardController` / `FieldTypeChangeGuard` — powinny zacząć sprawdzać referencje reguł.
- `GraduationService` — reguły muszą przeżyć przejście Runtime → Compiled.
- Prompt systemowy AI (`SchemaDiscoveryService.GenerateSystemPrompt`).

**Liczba konsumentów**: efektywnie wszystkie widoki obiektowe. **Zasięg wpływu**: Wysoki — to przekrojowa funkcja UI dotykająca każdego ekranu.

---

## Pokrycie testami

### Pliki testowe

**Brak.** W repo nie ma projektu testowego. Jedyny plik z „test" w nazwie to `/Users/jacek/Projects/Brekhof/XafXPODynAssem/test_runtime.py` (skrypt pomocniczy, nie zestaw testów).

### Ocena pokrycia

- **Liczba testów**: 0
- **Luki**: wszystko. Nie ma testów dla `Workflow.cs`, `SchemaAIToolsProvider`, `ReplicaSyncService`, kompilacji Roslynem ani niczego innego.
- **Konsekwencja**: weryfikacja tej funkcji będzie ręczna, na żywej aplikacji. Warto to uwzględnić w planie — zwłaszcza test kryterium rozstrzygającego fork oraz test „reguła wskazuje usunięte pole".

---

## Wzorce kodowania

### Konwencje nazewnicze

- **Identyfikatory C#**: angielskie (`WorkflowDefinition`, `TargetTypeName`, `ResolveTargetType`).
- **Napisy dla użytkownika**: polskie, przez `[XafDisplayName("...")]` — np. `[XafDisplayName("Przepływ (maszyna stanów)")]`, `[XafDisplayName("Nazwa przepływu")]`, `[XafDisplayName("Klasa użytkownika")]`. Brak warstwy `.resx`; polski jest wpisany w atrybuty.
- **Komentarze**: polskie, bez polskich znaków w części plików (`kazdy widok`, `Leniwe rozwiazanie typu`).
- **Grupa nawigacji**: `[NavigationItem("Zarządzanie schematem")]` — wspólna dla `CustomClass` i `WorkflowDefinition`; nowa encja tu pasuje i pojawi się w nawigacji bez ręcznej edycji `.xafml`.
- **Teksty `[Description]` narzędzi AI są ANGIELSKIE** (kontrakt dla LLM), ale stringi zwracane przez narzędzia przechodzą na polski dla ścieżek odmowy/strażników (np. „Zadna zmiana nie zostala zapisana.").
- Sugerowana nazwa encji: `AppearanceRule` z `[XafDisplayName("Reguła wyglądu")]`.

### Wzorce architektoniczne

- **ORM**: XPO (nie EF Core). Konstruktor `Session`, `SetPropertyValue(nameof(X), ref x, value)`.
- **Rejestracja typów**: statyczne przez `AdditionalExportedTypes.Add` w konstruktorze modułu; runtime przez `RefreshRuntimeTypes(Type[])`.
- **Schemat**: `SchemaChangeOrchestrator.UpdateDatabaseSchema` w `EarlyBootstrap()`, zaraz po kompilacji Roslynem, przed pełną inicjalizacją XAF.
- **Kontrolery**: w `Module/Controllers/*.cs`, wykrywane automatycznie.
- **Narzędzia AI**: prywatne metody z `[Description]`, parametry tylko prymitywne, złożone wejście jako JSON-string parsowany ręcznie, błędy zwracane jako tekst, nie wyjątki.
- **Bramka na człowieka**: wyłącznie promptowa, bez technicznego potwierdzenia. Pójście tą samą drogą jest tu konwencją, nie odstępstwem.

### Korekty do CLAUDE.md

Agent Code Analysis zweryfikował i skorygował:
- Baza runtime to **PostgreSQL** (`XpoProvider=Postgres` w `appsettings.json`), nie SQL Server localdb.
- Target to **net8.0**, DevExpress 26.1.4.
- **Nie ma** zmiennej `XAF_UPDATE_DB` — aktualizacja bazy idzie flagą CLI (`--updateDatabase`) lub przez debugger, plus zawsze aktywna ścieżka `EarlyBootstrap`/`BootstrapRuntimeEntities`.

---

## Ocena złożoności

| Czynnik | Wartość | Poziom |
|---|---|---|
| Liczba plików do dotknięcia | 8–11 (encja, kontroler/updater, 2× Startup, Module.cs, narzędzia AI, prompt, Updater.cs, guard) | Wysoki |
| Zależności | ok. 6 głównych (ConditionalAppearance, XPO, XafTypesInfo, Microsoft.Extensions.AI, CriteriaOperator, Npgsql) | Średni |
| Konsumenci | wszystkie widoki obiektowe | Wysoki |
| Pokrycie testami | 0 testów, brak projektu testowego | Wysoki (ryzyko) |
| Nierozstrzygnięty fork architektoniczny | 1, blokujący | Wysoki |

### Ogólnie: Złożone

Zadanie łączy trzy przekrojowe obszary: nową encję trwałą, przekrojowy mechanizm UI dotykający każdego widoku, oraz narzędzie AI z zapisem do bazy. Do tego dochodzi wdrożenie na trzy repliki i zerowe pokrycie testami. Sama encja jest prosta — trudność leży w mechanizmie dostarczania reguł i w propagacji.

---

## Kluczowe wnioski

### Mocne strony

- Silnik conditional appearance jest już podpięty w obu hostach — nie trzeba go instalować ani konfigurować.
- Istnieje kompletny, działający wzorzec „dane zamiast atrybutów" (`Workflow.cs`) z rozwiązanym problemem typów runtime i strażnikiem przed NRE.
- Warstwa AI jest gotowa i rozszerzalna jedną linią w `CreateTools()` — singleton, zero nowej rejestracji DI.
- Istnieje udowodniony precedens „to są dane, nie schemat — restart niepotrzebny": Workflow jest poza fingerprintem i narzędzia wprost mówią użytkownikowi „odśwież F5".
- Konwencja walidacji przed zapisem (`ValidateReportSpec`/`BuildReport`) już istnieje i pasuje idealnie.

### Obawy

- Fork architektoniczny nierozstrzygnięty, a wybór zmienia praktycznie cały kształt implementacji.
- Zero testów — nie da się zweryfikować regresji na widokach.
- Bramka na człowieka jest wyłącznie promptowa, a narzędzie omija bezpieczeństwo XAF.
- Reguły odwołują się do typów i pól przez stringi — klasyczne wiszące referencje.
- Specyfikacja pól i kontrolera z Pattern Mining pochodzi z niezweryfikowanych repo siostrzanych.

### Możliwości

- `Enabled` (blokada edycji z bazy) — wszystkie trzy repo siostrzane hardkodują tu `null`. `ApplicationUserLoginInfo.cs` dowodzi, że atrybutowo to działa; z bazy byłoby nowością.
- `AppearanceItemType` — repo siostrzane zawsze hardkodują `"ViewItem"`, więc nie da się z bazy celować w przyciski akcji ani elementy layoutu. Do rozważenia jako zakres.
- `AIChatDefaults.PromptSuggestions` — gotowe miejsce na podpowiedź „kolorowanie na listach".
- Nowa encja z `[NavigationItem("Zarządzanie schematem")]` pojawi się w nawigacji bez edycji `.xafml`.

---

## Ocena wpływu

- **Zmiany główne**: nowa encja `AppearanceRule` w `Module/BusinessObjects/`; kontroler LUB updater modelu (zależnie od wariantu); `Module.cs` (`AdditionalExportedTypes`, ewentualnie `AddGeneratorUpdaters`); `SchemaAIToolsProvider.cs` (2 nowe narzędzia + `CreateTools()`); `SchemaDiscoveryService.GenerateSystemPrompt` (nowa sekcja).
- **Zmiany powiązane**: `DatabaseUpdate/Updater.cs` (uprawnienia dla nowego typu), `CustomFieldDeleteGuardController`/`FieldTypeChangeGuard` (sprawdzanie referencji), `AIChatDefaults.PromptSuggestions`, ewentualnie `Win/Startup.cs` dla lustrzanej rejestracji.
- **Wpływ na testy**: brak projektu testowego — plan musi przewidzieć ręczny scenariusz weryfikacyjny, w tym test kryterium rozstrzygającego fork i test reguły wskazującej usunięte pole.

### Poziom ryzyka: Wysoki

#### Ryzyka wymagające jawnego pilnowania

1. **Reguły NIE MOGĄ trafić do `Module.GetMetadataFingerprint`/`QueryMetadata`.** Do fingerprintu należą wyłącznie `CustomClass` i `CustomField`. Gdyby dopisać tam reguły wyglądu, każde podyktowane zdanie zmieniałoby odcisk, a `ReplicaSyncService` zrestartowałby wszystkie trzy repliki (kod wyjścia 42 → supervisor). Precedens jest jednoznaczny: `WorkflowDefinition` jest poza fingerprintem i restartu nie wymaga.

2. **Typy runtime są kompilowane Roslynem — `Type.GetType` na nich zawodzi.** Rozwiązywać wyłącznie przez `XafTypesInfo.FindTypeInfo(name)?.Type`. W bazie przechowywać `FullName`, **nigdy** `AssemblyQualifiedName` (assembly jest generowane w pamięci i jego tożsamość zmienia się przy każdej rekompilacji). Wzorzec: `Workflow.cs:129`.

3. **Nie kopiować dopasowania typu przez `StartsWith`** (wzorzec z Brekhof). Prefiksowe dopasowanie stringów łapie typy rodzeństwa — a polskie encje runtime dzielą prefiksy: reguła dla `Faktura` złapałaby też `FakturaPozycja`. Używać dokładnej równości `Type` albo `ITypeInfo.IsAssignableFrom` dla świadomego dopasowania po dziedziczeniu.

4. **Nie kopiować `static event RulesCommitted`** (wzorzec z Brekhof). Trzy repliki to trzy procesy — event w procesie nigdy nie dotrze do pozostałych dwóch. Dodatkowo w Blazor Server każdy obwód subskrybuje się w `OnActivated`, więc zapis jednego użytkownika odpalałby zapytanie do bazy i odświeżenie w każdym otwartym obwodzie, na wątku zapisującego, poza dyspozytorami tych obwodów. Odświeżanie po zapisie ograniczyć do ramki samego edytora reguł (`ObjectSpace.Committed` → `ResetRulesCache()` + `Refresh()`), nie rozgłaszać.

5. **Reguła może przeżyć właściwość, którą celuje** — użytkownik usuwa pole przez AI, reguła zostaje. Dotyczy **obu wariantów**. Osobno: w Wariancie A `CollectAppearanceRules` odpala się **raz na każdy element UI**, więc nieosłonięty wyjątek w handlerze wywraca cały widok, nie jedną komórkę. Obowiązkowo: strażnik typu i właściwości w stylu `IStateMachine.Active` z `Workflow.cs` (reguła nieaktywna, jeśli typ lub pole się nie rozwiązuje) plus `try/catch` wewnątrz handlera.

6. **Zapisy narzędzia AI idą przez `INonSecuredObjectSpaceFactory`** — całkowicie omijają system bezpieczeństwa XAF (uprawnienia do typów i składowych). To istniejąca konwencja repo dla wszystkich narzędzi zapisujących, ale dla narzędzia, które może wywołać dowolny użytkownik czatu, oznacza: każdy rozmówca może stworzyć regułę wyglądu na dowolnej encji, także takiej, do której nie ma dostępu w UI. Do świadomej akceptacji albo do obudowania.

7. **Nie hookować `OnSaving()` do wpychania reguły do cache'u** (wzorzec z DataDrive) — wycofana transakcja zostawia widmową regułę w pamięci aż do restartu. Wyłącznie `ObjectSpace.Committed`.

8. **`FontStyle` to `DevExpress.Drawing.DXFontStyle`**, nie `System.Drawing.FontStyle`. Puste kolory zwracać jako `null`, nie `Color.Empty` (`Color.Empty.Name == "0"` cicho przechodzi przez null-checki).

---

## Rekomendacje

Zadanie tworzy nową funkcjonalność (brak istniejącej implementacji), więc rekomendacje dotyczą architektury i integracji.

### 1. Encja (wspólna dla obu wariantów)

Modelować na `WorkflowDefinition`. Minimalny zestaw pól — **propozycja z repo siostrzanych, do weryfikacji**:

`Name` (string), `IsEnabled` (bool, domyślnie true), `Priority` (int, realnie trwały — nie zaślepka), `TargetTypeName` (string, `FullName`), `Criterion` (string, `[Size(-1)]`, `[CriteriaOptions]`, `[EditorAlias(EditorAliases.CriteriaPropertyEditor)]`), `TargetItems` (string, `;`-rozdzielone lub `*`), `AppearanceContext` (enum Any/DetailView/ListView), `BackColor`/`ForeColor`, `FontStyle` (`DXFontStyle?`), `ItemVisibility` (`ViewItemVisibility?`).

Obowiązkowo: `ResolveTargetType()` bez cache'owania wyniku (jak w `Workflow.cs`) oraz strażnik aktywności sprawdzający, że typ **i** wskazane pola się rozwiązują.

### 2. Mechanizm dostarczania — po rozstrzygnięciu forku

Najpierw wykonać test rozstrzygający (licznik w `AIChatDetailViewUpdater.UpdateNode`). Dopiero potem wybrać wariant. Jeśli test pokaże cache procesowy — Wariant B nie spełnia wymogu i decyzja jest wymuszona.

### 3. Narzędzie AI

Dwie metody dołożone do `CreateTools()`, w konwencji `ValidateReportSpec`/`BuildReport`:

- `validate_appearance_rule` — tylko odczyt, zwraca listę problemów tekstem, każe zadać JEDNO konkretne pytanie zamiast zgadywać.
- `build_appearance_rule` — waliduje ponownie wewnętrznie, tworzy i commituje, zwraca markdown z instrukcją odświeżenia.

Walidacja przed zapisem: (1) `entityName` rozwiązuje się przez `XafTypesInfo`/`SchemaDiscoveryService`; (2) `CriteriaOperator.Parse(criteria)` nie rzuca — komunikat parsera wraca do użytkownika; (3) każda właściwość w drzewie kryterium (węzły `OperandProperty`) istnieje w `ITypeInfo.Members` — odrzucać z podpowiedzią („Property 'Kwotaa' does not exist on 'Faktura'; did you mean 'Kwota'?"); (4) każda nazwa w `targetItems` istnieje i jest widoczna/nieserwisowa; (5) kolory parsują się jako nazwa lub hex, normalizowane do hex przy zapisie.

Parametry prymitywne, `[Description]` po angielsku, stringi zwrotne dla ścieżek odmowy po polsku, `try/catch` zwracający tekst zamiast rzucać.

### 4. Prompt systemowy

Dopisać sekcję „## Appearance Rules" w `SchemaDiscoveryService.GenerateSystemPrompt`, w stylu istniejących sekcji „## Workflows" i „## Reports": kiedy użyć narzędzia, jaka jest składnia kryteriów DevExpress, obowiązek dopytania zamiast zgadywania, komunikat końcowy „odśwież F5, deploy niepotrzebny".

### 5. Integralność referencyjna

Rozszerzyć `FieldTypeChangeGuard`/`CustomFieldDeleteGuardController` o sprawdzenie „czy to pole jest wskazywane przez aktywną regułę wyglądu" — ostrzeżenie albo blokada, spójnie z istniejącym zachowaniem dla pól w użyciu.

---

## Następne kroki

1. **Orkiestrator: przedstawić użytkownikowi fork A/B** wraz z kryterium rozstrzygającym. To decyzja blokująca — bez niej gap-analyzer i specyfikacja będą pisane pod niewiadomą.
2. Wykonać test wynikowy: wiersz `AppearanceRule` wstawiony wprost do PostgreSQL → świeża sesja na replice bez zapisu i bez restartu → czy koloruje? Dla Wariantu B powtórzyć na kliencie WinForms.
3. Rozstrzygnąć otwarte pytanie o `FullName` przy graduacji Runtime → Compiled.
4. Zdecydować, czy zapis przez `INonSecuredObjectSpaceFactory` jest akceptowalny dla tego narzędzia, czy wymaga obudowania.
5. Dopiero potem: gap-analyzer → specyfikacja → plan implementacji.
