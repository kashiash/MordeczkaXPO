# Analiza luk — reguły wyglądu z dyktowania

**Data**: 2026-08-31
**Zadanie**: Reguły wyglądu trzymane w bazie + narzędzie AI zamieniające podyktowane polskie zdanie na rekord reguły + wdrożenie na Proxmox LXC 200
**Wejście**: `analysis/codebase-analysis.md` (faza 1) + własna weryfikacja w kodzie i w `deploy/`

> **Ścieżki**: wszystkie bezwzględne, z realnym zdublowanym segmentem `/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/...`

---

## TL;DR

Funkcji nie ma — zero linii kodu reguł sterowanych danymi; jest za to komplet klocków (silnik ConditionalAppearance podpięty w obu hostach, wzorzec `WorkflowDefinition`, warstwa narzędzi AI rozszerzalna jedną linią).
Do zbudowania: 1 nowa encja, 1 mechanizm dostarczania, 2 narzędzia AI, 5 punktów integracji — ale **forka A/B nie da się rozstrzygnąć statycznie**, bo w repo nie ma śladu ani `CollectAppearanceRules`, ani `IModelAppearanceRule`; rozstrzyga test na prototypie.
**Nowy, twardy fakt (nie było w fazie 1)**: `GraduationService` **nie emituje deklaracji `namespace`** — po graduacji `FullName` typu zmienia się z `XafXPODynAssem.RuntimeEntities.X` na `XafXPODynAssem.Module.BusinessObjects.X`, więc **każda zapisana reguła cicho przestaje działać**.
**Drugi nowy fakt**: pól wdrożonych nie da się usunąć (`CustomFieldDeleteGuardController`), ale `delete_entity` w narzędziach AI kasuje całą encję runtime — orphan jest osiągalny z samego czatu.
Wdrożenie na trójkę replik nowym obrazem jest **nienapisane**, a transfer paczki jest wg README „blokowany dla agenta" — punkt 3 zadania nie jest w pełni wykonalny agentowo w obecnym stanie.

## Kluczowe decyzje

- **Encja, narzędzia AI, prompt, uprawnienia, cykl życia i wdrożenie opisuję niezależnie od forka** — tylko sekcja „mechanizm dostarczania" się rozgałęzia. Dzięki temu 80% specyfikacji da się napisać przed rozstrzygnięciem A/B i fork nie blokuje reszty planu.
- **Forka nie rozstrzygam i nie da się go rozstrzygnąć czytaniem kodu** — obie strony mają w tym repo zero śladu ładunku (`CollectAppearanceRules`: 0 trafień; `IModelAppearanceRule`: 0 trafień). Jedyny rozstrzygający dowód to test na prototypie, a to praca implementacyjna, nie analityczna. Zwracam jako decyzję krytyczną.
- **Ryzyko oceniam jako Wysokie i nie zmiękczam** — funkcja przekrojowa dotykająca każdego widoku, zero testów w repo, nierozstrzygnięty fork, nienapisana procedura wdrożenia trójki, zapisy omijające bezpieczeństwo XAF.
- **Cykl życia opisuję z własnych dowodów, nie z założenia fazy 1** — faza 1 mówiła ogólnie „reguła może przeżyć pole"; realnie są cztery różne scenariusze o różnym prawdopodobieństwie i różnym objawie (niżej, sekcja „Cykl życia danych").
- **Brak widoku przeglądu reguł traktuję jako orphan CREATE-bez-READ i eskaluję do decyzji krytycznej** — narzędzie AI tworzy rekord, a jedyna bramka jest promptowa; bez listy reguł użytkownik nie ma czym cofnąć pomyłki AI.

## Otwarte pytania / ryzyka

- **[BLOKUJĄCE]** Fork A/B. Kryterium: wiersz wstawiony wprost do PostgreSQL → świeża sesja na replice, która nie obsłużyła zapisu i nie była restartowana → czy koloruje? Dla Wariantu B powtórzyć na kliencie WinForms.
- **[BLOKUJĄCE]** Ścieżka wdrożenia na trójkę replik. Wymiana obrazu przy trzech replikach jest **zaprojektowana, ale nienapisana**; `wdroz.sh:49-50` odmawia startu, gdy `upstream.conf` ma więcej niż jeden `server`.
- **Graduacja niszczy reguły — potwierdzone.** `Services/GraduationService.cs` ma tylko jedną deklarację `namespace` (linia 4, własna przestrzeń serwisu). `GenerateEntityClass` nie emituje żadnej — komentarz w wygenerowanym źródle mówi „Place this file in your BusinessObjects folder". Zmiana `FullName` jest pewna, nie hipotetyczna.
- **Sprzeczność w dokumentacji wdrożeniowej.** `deploy/bezprzerwy/README.md` twierdzi „każda ma `XAF_UPDATE_DB=1`", a faza 1 zweryfikowała, że **takiej zmiennej nie ma** (aktualizacja idzie flagą `--updateDatabase`). To nie jest kosmetyka: całe uzasadnienie „dlaczego stara kopia jest zatrzymywana" opiera się na tej zmiennej.
- **Transfer paczki jest ręczny.** `deploy/README-wdrozenie.md` §1: „do wykonania recznie — transfer jest blokowany dla agenta", paczka 223 MB. Punkt 3 zadania („wdrożenie i test na żywo") nie jest w całości wykonalny przez agenta bez zmiany tej reguły albo bez udziału człowieka w kroku scp/pct push.
- **[NIEZWERYFIKOWANE, MATERIALNE]** `DatabaseUpdate/Updater.cs` woła `CreateDefaultRole()`/`CreateAdminRole()` **wyłącznie wewnątrz `#if !RELEASE`**. Jeśli obraz na LXC 200 powstał z `dotnet publish -c Release`, to `GrantNavigationHubPermissions` na wdrożonej instancji **nigdy się nie wykonuje** — a więc plan „dodać uprawnienia dla nowego typu w Updater.cs" byłby tam no-opem. `deploy/Dockerfile` jest runtime-only (`COPY publish/`), więc konfiguracji buildu nie da się z repo odczytać. **Do sprawdzenia**: z jakim `-c` powstała paczka `/tmp/xpo-publish.tgz`.
- Zapisy narzędzi AI idą przez `INonSecuredObjectSpaceFactory` — omijają uprawnienia XAF. Dowolny rozmówca czatu może stworzyć regułę na dowolnej encji.
- Zero testów w repo. Weryfikacja tej funkcji będzie w całości ręczna, na żywej aplikacji.

---

## Podsumowanie

- **Poziom ryzyka**: **Wysoki**
- **Szacowany nakład**: **Wysoki**
- **Wykryte charakterystyki**: `modifies_existing_code`, `creates_new_entities`, `involves_data_operations`, `ui_heavy`
- **Typ zmiany**: **addytywna** (nic istniejącego nie zmienia zachowania; cztery statyczne `[Appearance]` zostają nietknięte)
- **Wymagania zgodności**: **umiarkowane** — nowy typ trwały wymaga tabeli w bazie i musi współistnieć ze starym kodem podczas wymiany obrazu

## Charakterystyki zadania

| Charakterystyka | Wartość | Uzasadnienie |
|---|---|---|
| `has_reproducible_defect` | **false** | Nic nie jest zepsute — to funkcja od zera; w opisie nie ma ani jednego scenariusza awarii, stack trace'a ani „działa źle". |
| `modifies_existing_code` | **true** | Trzeba zmienić `Module.cs`, `SchemaAIToolsProvider.cs`, `SchemaDiscoveryService.cs`, oba `Startup.cs`, `Updater.cs`, `AIChatDefaults.cs` i strażniki. |
| `creates_new_entities` | **true** | Encja `AppearanceRule` nie istnieje — grep za `AppearanceController`/`CollectAppearanceRules`/`IModelAppearanceRule`/`IAppearanceRuleProperties` daje 0 trafień w całym repo. |
| `involves_data_operations` | **true** | Pełny CRUD na rekordach reguł (tworzenie przez AI, odczyt przy renderowaniu każdego widoku, edycja i wyłączanie przy pomyłce AI) plus odczyt reguł na gorącej ścieżce UI. |
| `ui_heavy` | **true** | Cały obserwowalny efekt funkcji to UI (kolor wiersza, styl czcionki, widoczność), dochodzi nowa pozycja nawigacji i para widoków list/detail dla samych reguł. |

---

## Stan obecny vs docelowy

### Conditional appearance

| Element | Dziś | Docelowo | Dowód stanu obecnego |
|---|---|---|---|
| Pakiet `DevExpress.ExpressApp.ConditionalAppearance` 26.1.4 | jest | bez zmian | `XafXPODynAssem.Module.csproj` |
| `RequiredModuleTypes.Add(ConditionalAppearanceModule)` | jest | bez zmian | `Module.cs:107` |
| `.AddConditionalAppearance()` Blazor | jest | bez zmian | `Blazor.Server/Startup.cs:53` |
| `.AddConditionalAppearance()` Win | jest | bez zmian | `Win/Startup.cs:30` |
| Reguły z atrybutów | 4 | bez zmian (zostają) | `CustomClass.cs:23,28`, `ApplicationUserLoginInfo.cs:18,25` |
| Reguły z bazy | **0** | encja + mechanizm dostarczania | 0 trafień grep |
| Reguły dla encji runtime | **niemożliwe** | działające | typ nie istnieje w czasie kompilacji |
| Narzędzie AI tworzące regułę | **0** | `validate_appearance_rule` + `build_appearance_rule` | `CreateTools()` ma 19 narzędzi, żadnego od wyglądu |
| Widok przeglądu/edycji reguł | **0** | lista + szczegóły w „Zarządzanie schematem" | typ nie istnieje |
| Kod dyktowania/mowy | **0** | **poza zakresem** — „dyktowanie" = tekst w istniejącym czacie | brak w repo |
| Wymiana obrazu przy 3 replikach | **nienapisana** | do rozstrzygnięcia (decyzja krytyczna) | `deploy/bezprzerwy/README.md`, `wdroz.sh:49-50` |

### Pliki do utworzenia

| Plik | Rola | Wzorzec do skopiowania |
|---|---|---|
| `Module/BusinessObjects/AppearanceRule.cs` | encja trwała reguły | `BusinessObjects/Workflow.cs` (`WorkflowDefinition`) — `TargetTypeName` jako string, `ResolveTargetType()` przez `XafTypesInfo.FindTypeInfo` (`Workflow.cs:129`), strażnik aktywności, polskie `[XafDisplayName]`, `[DefaultClassOptions]` + `[NavigationItem("Zarządzanie schematem")]` (`Workflow.cs:27-28`) |
| **Wariant A**: `Module/Controllers/AppearanceRuleController.cs` | `ViewController` czytający reguły z bazy w `OnActivated`, wpinający się w `AppearanceController.CollectAppearanceRules` | `Controllers/WorkflowCommitController.cs` — podpięcie pod zdarzenie kontrolera DevExpress w `OnActivated`; `CustomFieldDeleteGuardController` — `try/catch` wokół całej logiki i sprzątanie w `OnDeactivated` |
| **Wariant B**: `Module/AppearanceRulesUpdater.cs` | `ModelNodesGeneratorUpdater<ModelViewsNodesGenerator>` wstrzykujący `IModelAppearanceRule` | `AIChatDetailViewUpdater.cs` (50 linii, odczytany w całości) |

### Pliki do zmiany

| Plik | Zmiana | Kotwica |
|---|---|---|
| `Module.cs` | `AdditionalExportedTypes.Add(typeof(BusinessObjects.AppearanceRule));` | linie 90–104, tuż za trzema typami Workflow |
| `Module.cs` | **tylko Wariant B**: `updaters.Add(new AppearanceRulesUpdater());` | `AddGeneratorUpdaters` |
| `Module.cs` | **NIE DOTYKAĆ** `QueryMetadata`/`GetMetadataFingerprint`/`ComputeFingerprint` | od ~264 do końca — dopisanie reguł tam restartuje wszystkie trzy repliki przy każdym podyktowanym zdaniu |
| `Services/SchemaAIToolsProvider.cs` | 2 nowe prywatne metody z `[Description]` + 2 wpisy w `CreateTools()` | `CreateTools()` linie 40–69; wzorzec `ValidateReportSpec`/`BuildReport`; zapis przez `CreateObjectSpace()` linie 87–123 |
| `Services/SchemaDiscoveryService.cs` | nowa sekcja `## Appearance Rules` w prompcie systemowym | między `## Workflows (State Machines)` (linia 131) a `## Supported Field Types` (linia 144) |
| `Services/AIChatDefaults.cs` | nowa `PromptSuggestionItem` „Kolorowanie na listach" | lista `PromptSuggestions`, obok „Przygotuj raport" |
| `DatabaseUpdate/Updater.cs` | uprawnienia dla `AppearanceRule` + nawigacja | wzorzec `GrantNavigationHubPermissions` — **uwaga na `#if !RELEASE`** (patrz ryzyka) |
| `Validation/FieldTypeChangeGuard.cs` | nowa metoda „czy pole jest wskazywane przez aktywną regułę" | obok `IsFieldRemovalSafe` (linia 179) |
| `Win/Startup.cs` | **do rozstrzygnięcia** — czy lustrzana rejestracja, czy świadomie tylko Blazor | linia 30; precedens `AddStateMachine` jest **tylko** w Blazorze |

---

## Punkty integracji

1. **`Module.cs` — rejestracja typu trwałego.** Jedna linia w `AdditionalExportedTypes` (linie 90–104). Bez niej XPO nie założy tabeli i typ nie pojawi się w nawigacji.
2. **`Module.cs` — `AddGeneratorUpdaters`.** Tylko przy Wariancie B. Precedens: `updaters.Add(new AIChatDetailViewUpdater());`.
3. **`Module.cs` — odcisk metadanych: integracja NEGATYWNA.** `QueryMetadata` czyta surowym Npgsql wyłącznie `CustomClass` i `CustomField`. Reguł tam **nie dokładamy**. Precedens: `WorkflowDefinition` też jest poza odciskiem i restartu nie wymaga.
4. **`Blazor.Server/Startup.cs:53` i `Win/Startup.cs:30`.** Silnik już jest — **nowej rejestracji nie wymaga żaden z wariantów**. Wariant A dochodzi jako kontroler wykrywany automatycznie ze skanowania `Module/Controllers/`. Otwarta pozostaje decyzja o parytecie WinForms (patrz decyzje ważne).
5. **`Services/SchemaAIToolsProvider.cs` — `CreateTools()`.** Dwa `AIFunctionFactory.Create(...)`. `SchemaAIToolsProvider` jest singletonem (`AIServiceCollectionExtensions.cs`), więc **żadnej nowej rejestracji DI nie trzeba**.
6. **`Services/SchemaDiscoveryService.GenerateSystemPrompt`.** Prompt jest odświeżany przed każdą turą (`AIChatService.AskAsync` → `RefreshSystemPrompt()`), więc nowa sekcja działa od razu po deployu kodu. To zarazem **jedyna istniejąca bramka na człowieka** — czysto tekstowa.
7. **`DatabaseUpdate/Updater.cs`.** Uprawnienia typu + `AddNavigationPermission`. Idempotentne nadania muszą stać **poza** blokiem `if (role == null)` — komentarz w pliku wprost o tym ostrzega. Osobno: cały blok wołający `CreateDefaultRole()` stoi w `#if !RELEASE`.
8. **`Services/AIChatDefaults.PromptSuggestions`.** Gotowe miejsce na podpowiedź, która czyni funkcję odkrywalną w czacie.
9. **`Validation/FieldTypeChangeGuard` + `Controllers/CustomFieldDeleteGuardController`.** Integralność referencyjna reguł względem pól.

---

## Ścieżka użytkownika: od zdania do pokolorowanego wiersza

```
1. Użytkownik otwiera czat AI i pisze/dyktuje (systemowym dyktowaniem OS,
   nie kodem aplikacji): „zrób faktury niezapłacone na czerwono na listach"
        ↓
2. AIChatService.AskAsync → RefreshSystemPrompt() → prompt z nową sekcją
   „## Appearance Rules"
        ↓
3. LLM wybiera validate_appearance_rule (tylko odczyt):
   – czy „Faktura" rozwiązuje się przez XafTypesInfo/SchemaDiscoveryService?
   – czy CriteriaOperator.Parse("Zaplacona = False") się parsuje?
   – czy każdy OperandProperty w drzewie kryterium istnieje w ITypeInfo.Members?
   – czy nazwy w targetItems istnieją i nie są serwisowe?
   – czy „czerwony" parsuje się jako kolor?
        ↓
4a. Problem → narzędzie zwraca TEKST z podpowiedzią
    („Property 'Kwotaa' does not exist on 'Faktura'; did you mean 'Kwota'?").
    LLM zadaje JEDNO pytanie i czeka — bo tak każe prompt systemowy.
        ↓
4b. OK → LLM wywołuje build_appearance_rule
        ↓
5. Nowy scope DI → INonSecuredObjectSpaceFactory → CreateObject<AppearanceRule>()
   → CommitChanges()   ⚠ z pominięciem uprawnień XAF
        ↓
6. Narzędzie zwraca markdown: co utworzono + „odśwież F5, deploy niepotrzebny"
        ↓
7. Użytkownik wchodzi na listę Faktur →
   [A] kontroler w OnActivated czyta reguły z bazy i dorzuca je w CollectAppearanceRules
   [B] model aplikacji zawiera węzły IModelAppearanceRule
        ↓
8. Natywny AppearanceController DevExpressa maluje wiersze
```

### Gdzie użytkownik potwierdza

**Nigdzie technicznie.** Jedyna bramka to prompt systemowy w `SchemaDiscoveryService.GenerateSystemPrompt`: „Always confirm with the user before executing any schema change" oraz „NEVER guess the parts the user did not say... ask ONE specific question about it and wait for the answer". Nie ma popupu, podglądu ani dwufazowego commitu. Konwencja `validate_*` → `build_*` jest **konwencją, nie bramką** — nic technicznie nie broni LLM-owi wywołać samego `build_`.

To jest istniejąca konwencja repo dla wszystkich 19 narzędzi zapisujących. Pójście tą samą drogą nie jest odstępstwem — ale dla reguły wyglądu, która natychmiast zmienia wygląd aplikacji **wszystkim** użytkownikom, warto to nazwać wprost i przyjąć świadomie.

### Co widzi użytkownik, gdy AI się pomyli

| Rodzaj pomyłki | Objaw | Jak dziś cofnąć |
|---|---|---|
| Zły typ docelowy (`Faktura` zamiast `FakturaPozycja`) | koloruje się nie ta lista | **tylko przez edycję rekordu — a widoku reguł nie ma** |
| Złe kryterium, ale parsujące się | koloruje się złe wiersze albo żadne | jw. |
| Kryterium wskazuje nieistniejącą właściwość | **złapane w walidacji** — nie dojdzie do zapisu | — |
| Zły kolor | zły kolor, funkcjonalnie działa | jw. |
| Reguła kolorująca wszystko na czerwono | cała aplikacja na czerwono, wszystkim użytkownikom, na wszystkich trzech replikach | jw. |
| Wyjątek w handlerze (Wariant A) | **cały widok się wywala**, nie jedna komórka — `CollectAppearanceRules` odpala się raz na element UI | restart |

**To jest orphan CREATE-bez-READ i jest krytyczny.** Encja z `[DefaultClassOptions]` + `[NavigationItem("Zarządzanie schematem")]` daje listę i szczegóły automatycznie, bez edycji `.xafml` — koszt zamknięcia luki to dwa atrybuty. Bez tego jedyną drogą wycofania błędnej reguły jest SQL w bazie.

---

## Cykl życia danych

### Kompletność CRUD dla encji `AppearanceRule`

| Operacja | Backend | Komponent UI | Dostęp użytkownika | Status |
|---|---|---|---|---|
| CREATE | `build_appearance_rule` + `INonSecuredObjectSpaceFactory` | czat AI (istnieje) | `AIChatDefaults.PromptSuggestions` | ✅ **po dołożeniu narzędzia** |
| READ (przez system) | mechanizm dostarczania (A lub B) | natywny `AppearanceController` | automatyczny | ✅ **po rozstrzygnięciu forka** |
| READ (przez człowieka) | XPO ListView | **BRAK** | **BRAK** | ❌ **orphan — do zamknięcia dwoma atrybutami** |
| UPDATE | XPO DetailView | **BRAK** | **BRAK** | ❌ jw. |
| DELETE / wyłączenie | XPO + pole `IsEnabled` | **BRAK** | **BRAK** | ❌ jw. |

**Kompletność w planowanym minimum: 40%.** Po dodaniu `[DefaultClassOptions]` + `[NavigationItem("Zarządzanie schematem")]`: **100%**, przy koszcie dwóch atrybutów i wpisu w `Updater.cs`. Precedens jest gotowy: `Workflow.cs:27-28`.

### Cztery scenariusze wiszących referencji — z dowodami

**1. Zniknie pole wskazywane przez regułę — prawdopodobieństwo NISKIE, nie takie jak zakładała faza 1.**
`Controllers/CustomFieldDeleteGuardController.cs` wyłącza akcję „Usuń" na polach wdrożonych; `FieldTypeChangeGuard.IsFieldRemovalSafe` (linia 179) zwraca `false`, gdy pole jest wdrożone albo ma kolumnę w bazie, i **`false` również wtedy, gdy nie potrafi wykazać bezpieczeństwa**. Zasada w kodzie jest jawna: „pol nie usuwamy — ukrywamy je". Skasować da się tylko pole jeszcze niewdrożone, na które reguła i tak nie powinna wskazywać.
**Realne ryzyko to nie usunięcie, tylko ukrycie**: `IsVisibleInListView = false` zostawia pole w metadanych i w bazie, ale usuwa je z widoku. Reguła celująca w nie przez `TargetItems` cicho przestaje mieć w co trafić — bez błędu, bez komunikatu. Strażnik aktywności oparty wyłącznie na „czy właściwość się rozwiązuje" **tego nie wykryje**, bo właściwość istnieje.

**2. Zniknie cała encja runtime — prawdopodobieństwo ŚREDNIE, osiągalne z czatu.**
`SchemaAIToolsProvider.cs:53` rejestruje `delete_entity`; implementacja (linie 709–750) kasuje `CustomClass` i wszystkie jego `CustomField`. Jedyna blokada to `Status == Compiled` (linia 730). Encja runtime da się skasować jednym zdaniem w tym samym czacie, który tworzy reguły. Nic dziś nie sprawdza, czy jakaś reguła na nią wskazuje.
**Zabezpieczenie**: `ResolveTargetType()` zwraca `null` → strażnik aktywności wyłącza regułę → widok żyje. Wzorzec obowiązkowy, komentarz przy `IStateMachine.Active` w `Workflow.cs` (~linia 140) ostrzega wprost, że bez tego „kazdy widok w aplikacji dostalby NullReferenceException".

**3. Encja przechodzi graduację Runtime → Compiled — reguła ginie ZAWSZE. POTWIERDZONE.**
`Services/GraduationService.cs` zawiera **jedną jedyną** deklarację `namespace` — w linii 4, własną przestrzeń serwisu. `GenerateEntityClass` (linie 58–123) emituje `using`-i, atrybuty i `public class X : BaseObject`, ale **żadnej deklaracji `namespace`**. Wygenerowany komentarz mówi: „Place this file in your BusinessObjects folder", czyli w `XafXPODynAssem.Module.BusinessObjects`.
Runtime `FullName` to `XafXPODynAssem.RuntimeEntities.X` (`Services/RuntimeAssemblyBuilder.cs:22`: `RuntimeNamespace = "XafXPODynAssem.RuntimeEntities"`, wstrzykiwany w linii 125).
**Wniosek: po graduacji `FullName` zmienia się na `XafXPODynAssem.Module.BusinessObjects.X`. Każda zapisana reguła wskazująca tę encję przestaje działać — cicho, bez błędu, po prostu przestaje kolorować.** To już nie jest otwarte pytanie fazy 1, to fakt.
Dodatkowo: `Graduate()` ustawia `Status = Compiled`, a `Status == Runtime` filtruje bootstrap i `QueryMetadata` (`SchemaAIToolsProvider.cs:321,409`, `SchemaExportImportService.cs:20`, `SchemaDiscoveryService.cs:74`, `Controllers/SchemaChangeController.cs:37`). Powstaje **okno**, w którym typ nie istnieje pod żadną nazwą: od kliknięcia „Graduate" do wklejenia źródła przez programistę, przebudowy i wdrożenia nowego obrazu. W tym oknie reguła musi być bezpiecznie nieaktywna, nie wybuchowa.
**Do rozstrzygnięcia w specyfikacji**: albo migracja `TargetTypeName` przy graduacji, albo rozwiązywanie typu po samej nazwie krótkiej z fallbackiem, albo świadoma akceptacja z komunikatem w `GraduationWarningController`.

**4. Reguła wskazuje typ, którego dana replika jeszcze nie ma — prawdopodobieństwo WYSOKIE, ale objaw łagodny.**
Sytuacja normalna, nie awaryjna: użytkownik tworzy encję na replice `red`, `ReplicaSyncService` restartuje `green` po ~54 s i `blue` po ~104 s (zmierzone, `deploy/bezprzerwy/README.md`). W tym oknie reguła siedzi w bazie i wskazuje typ, którego dwie repliki jeszcze nie skompilowały.
**To dokładnie ten sam przypadek co scenariusz 2 z punktu widzenia kodu**: `ResolveTargetType()` zwraca `null`, strażnik wyłącza regułę, widok działa bez kolorowania, a po restarcie repliki kolorowanie pojawia się samo. **Żadnego kodu specjalnego to nie wymaga — pod warunkiem, że strażnik jest.** Bez strażnika: dwie z trzech replik wywracają widoki na ~100 sekund po każdym utworzeniu encji.

**5. Zmiana nazwy pola lub encji — ryzyko POMIJALNE.**
Grep za `rename`/`Rename` w `SchemaAIToolsProvider.cs`: **0 trafień**. Narzędzia zmiany nazwy nie ma. Ręczna edycja `FieldName` w UI teoretycznie możliwa, ale `FieldTypeChangeGuard` blokuje zmiany na polach wdrożonych. Nie planować pod to obsługi.

---

## Ocena obu wariantów forka

Sekcja rozgałęzia się tylko tutaj. Encja, narzędzia AI, prompt, uprawnienia i wdrożenie są wspólne.

### Wariant A — `ViewController` + `AppearanceController.CollectAppearanceRules`

**Za:**
- Reguły czytane z bazy **przy każdej aktywacji widoku** → propagacja na trzy repliki jest darmowa: te same wiersze, ta sama baza, zero sygnalizacji, zero restartu, zero wpisów w odcisku metadanych.
- Kształt pasuje do istniejącego precedensu: `WorkflowCommitController` podpina się w `OnActivated` pod zdarzenie kontrolera DevExpressa (`Frame.GetController<StateMachineController>().TransitionExecuted += ...`).
- **Zachowuje się identycznie w Blazorze i WinForms** — kontroler jest wykrywany automatycznie ze skanowania `Module/Controllers/`, żadnej rejestracji w `Startup.cs`. To ma znaczenie, bo jedyny precedens „dane zamiast atrybutów" w tym repo (`AddStateMachine`) jest **tylko Blazorowy** (zweryfikowane: 0 trafień w `Win/Startup.cs`).
- XPO pozwala obiektowi trwałemu implementować `IAppearanceRuleProperties` bezpośrednio, bez klasy-adaptera.

**Przeciw / koszty:**
- **Zerowy ślad w tym repo.** Grep za `AppearanceController` i `CollectAppearanceRules`: 0 trafień. „Sprawdzony w Fleetman/DataDrive/HIS" znaczy: sprawdzony w repozytoriach, których w tej sesji nie odczytano.
- `CollectAppearanceRules` odpala się **raz na element UI** — zapytanie do bazy w handlerze byłoby katastrofą wydajnościową; reguły trzeba wczytać raz do pola w `OnActivated`.
- Nieobsłużony wyjątek w handlerze wywraca **cały widok**, nie jedną komórkę. `try/catch` wewnątrz handlera jest obowiązkowy.
- Sekwencja `ResetRulesCache()` → subskrypcja → `Refresh()` jest nienegocjowalna. Pominięcie `ResetRulesCache()` sprawia, że zdarzenie w ogóle się nie odpala, bo DevExpress zacache'ował „brak reguł" z pierwszej aktywacji.
- Wymaga właściwości-zaślepek (`[NonPersistent]`) dla członków interfejsu, których nie przechowujemy.

### Wariant B — `ModelNodesGeneratorUpdater<ModelViewsNodesGenerator>` + `IModelAppearanceRule`

**Za:**
- **Mechanizm wstrzykiwania węzłów jest w tym repo udowodniony i chodzi produkcyjnie**: `AIChatDetailViewUpdater.cs` robi `dv.Items.AddNode<IModelAIChatViewItem>(...)`, zarejestrowany w `Module.cs` `AddGeneratorUpdaters`.
- Zero własnego interpretera `CriteriaOperator` — ewaluację robi natywny `ConditionalAppearanceModule`.
- Celowanie w widok po stringowym ID działa jednolicie dla typów statycznych i roslynowych.
- Oficjalnie wspierana ścieżka DevExpressa.

**Przeciw / koszty:**
- **Udowodniony jest mechanizm, nie ładunek.** `IModelAppearanceRule`: 0 trafień w repo. To, że updater dodaje `IModelAIChatViewItem` do `Items`, nie dowodzi, że ta sama ścieżka obsłuży `AppearanceRules`.
- **Model aplikacji bywa budowany raz i cache'owany.** Jeśli cache jest procesowy, nowa reguła nie pojawi się do restartu — co przekreśla wymóg „bez rekompilacji".
- Na trzech replikach problem się mnoży: replika, która nie obsłużyła zapisu, przebuduje model dopiero, gdy coś ją zmusi. Jedyne dostępne dziś „zmuszenie" to ścieżka restartu z `ReplicaSyncService`, napędzana odciskiem metadanych — do którego reguł dokładać **nie wolno**.
- **Host WinForms to osobna, niezależna droga, którą Wariant B może nie spełnić wymogu.** Klient desktopowy to długo żyjący proces; węzeł modelu najprawdopodobniej wymaga restartu klienta.

### Kryterium rozstrzygające (identyczne dla obu)

**Nowa reguła musi dotrzeć do trzech replik, które nigdy nie widziały zapisu, bez restartu i bez rekompilacji.**

**Test wynikowy**: wstawić wiersz `AppearanceRule` **wprost do PostgreSQL**, z pominięciem aplikacji → otworzyć świeżą sesję przeglądarki na replice, która nie obsłużyła zapisu i nie była restartowana → czy koloruje? Dla Wariantu B powtórzyć na kliencie WinForms.

**To jest bramka akceptacyjna na prototypie, nie coś, co analiza luk może rozstrzygnąć** — w repo nie ma statycznego dowodu po żadnej stronie.

**Diagnostyka pomocnicza, która NIE rozstrzyga**: licznik w `AIChatDetailViewUpdater.UpdateNode` pokazuje tylko, czy przebiega **generator**. Każdy obwód może budować własny `ModelApplication` z już wygenerowanych, zacache'owanych węzłów — licznik wygląda wtedy „per obwód", a mimo to nowy wiersz z bazy do modelu nigdy nie trafia.

---

## Ścieżka wdrożenia — stan faktyczny

Stan docelowy: LXC 200 (192.168.88.25), `/opt/mordeczka`, trzy repliki `red`/`green`/`blue` na portach **8101–8103** (`trzy-repliki.sh:29`), nginx z `ip_hash` na 8090, wspólny wolumen kluczy `mordeczka-keys`.

**Co blokuje:**
- `wdroz.sh:49-50` — odmawia startu, gdy `upstream.conf` zawiera więcej niż jeden `server`. Zna tylko układ dwóch kopii na przemian (8092/8093); uruchomiony na trójce nadpisałby listę jednym wpisem i skasował trzy kontenery.
- Rolowanie replik po jednej przy nowym obrazie jest **jawnie zabronione**: inny generator może z tych samych metadanych zbudować inny model, a schemat jest jeden.
- Wymiana całej trójki (nowa trójka obok, jedno przeładowanie nginxa, potem gaszenie starej) jest **zaprojektowana, nienapisana**.

**Trzy rzeczy, których nie pokrywa żaden z README:**
1. **Nowa tabela.** `AppearanceRule` to nowy typ trwały → potrzebna tabela. `README-wdrozenie.md` §3 robi z tego osobny przebieg `--updateDatabase --forceUpdate --silent`. Przy wariancie (a) tabela musi powstać **zanim** wstanie nowa trójka, a stara trójka wciąż chodzi na tej samej bazie. Zmiana jest addytywna (stary kod tabeli nie zna i jej nie ruszy), ale kolejność musi być w planie jawna.
2. **Sprzeczność w dokumentacji.** `bezprzerwy/README.md` opiera całe uzasadnienie „dlaczego stara kopia jest zatrzymywana" na `XAF_UPDATE_DB=1` — zmiennej, której **nie ma**. Trzeba rozstrzygnąć, co faktycznie robi wdrożona instancja, zanim zaufamy wnioskowi „dwie wersje piszą do jednego schematu".
3. **Transfer jest ręczny.** `README-wdrozenie.md` §1: „do wykonania recznie — transfer jest blokowany dla agenta", paczka 223 MB. Punkt 3 zadania („wdrożenie i test na żywo") **nie jest w całości wykonalny agentowo** bez zmiany tej reguły.

---

## Zagadnienia wymagające decyzji

### Krytyczne (do rozstrzygnięcia przed pisaniem specyfikacji)

**1. Fork architektoniczny: Wariant A czy Wariant B?**
Wybór zmienia kształt całej implementacji mechanizmu dostarczania. Statycznie nierozstrzygalny — obie strony mają w tym repo zero śladu ładunku.
- Opcje: (A) `ViewController` + `CollectAppearanceRules`; (B) `ModelNodesGeneratorUpdater` + `IModelAppearanceRule`; (C) prototypować oba i rozstrzygnąć testem.
- Rekomendacja: **(C) prototypować oba i rozstrzygnąć testem wynikowym** — analiza luk nie ma czym tego przesądzić. Do wiadomości przy decyzji: jedyny repo-weryfikowalny przechył jest po stronie A (działa identycznie w obu hostach bez rejestracji w `Startup.cs`; jedyny precedens „dane zamiast atrybutów" w tym repo jest tylko Blazorowy), a Wariant B ma dwie **niezależne** drogi porażki — procesowy cache modelu aplikacji i długo żyjący klient WinForms.
- Bramka akceptacyjna niezależna od wyboru: wiersz wprost do PostgreSQL → świeża sesja na replice bez zapisu i bez restartu → koloruje?

**2. Ścieżka wdrożenia na LXC 200.**
- (a) **Napisać wymianę całej trójki blue/green obok, na 8111–8113, z jednym przeładowaniem nginxa.** Zero przerwy, zgodne z zamysłem README. Koszt: nowy skrypt, nienapisany, bez przetestowanej ścieżki wycofania; wymaga 6 kontenerów naraz na maszynie z 4 GB RAM (`README-wdrozenie.md` podaje 4 vCPU / 4 GB / 16 GB dysku) — **do zweryfikowania, czy się mieści**.
- (b) **Zgasić trójkę i wdrożyć od nowa z przerwą.** Najprostsze i najbezpieczniejsze; korzysta z `trzy-repliki.sh`, który już działa. Koszt: jawna przerwa w działaniu.
- (c) **Wdrożyć pojedynczą instancję testową i odłożyć trójkę.** Najniższe ryzyko dla weryfikacji funkcji, ale **nie weryfikuje kryterium rozstrzygającego forka** — a to kryterium wymaga trzech replik z definicji.
- Uwaga do każdej opcji: krok `--updateDatabase` dla nowej tabeli oraz ręczny transfer paczki.

**3. Brak widoku przeglądu reguł = orphan CREATE-bez-READ.**
AI tworzy rekord, jedyna bramka jest promptowa, a użytkownik nie ma czym cofnąć pomyłki. Błędna reguła dotyka wszystkich użytkowników na wszystkich trzech replikach.
- Opcje: (A) `[DefaultClassOptions]` + `[NavigationItem("Zarządzanie schematem")]` + uprawnienia w `Updater.cs` — pełny CRUD za darmo, precedens `Workflow.cs:27-28`; (B) zostawić tylko tworzenie z czatu.
- Sugestia: (A). Koszt to dwa atrybuty i jeden wpis w `Updater.cs`; bez tego kompletność cyklu życia wynosi 40%, a jedyna droga naprawy błędnej reguły to SQL w produkcyjnej bazie.
- **Ta decyzja NIE jest zależna od sprawy `#if !RELEASE`.** Rola `Administrators` powstaje z `IsAdministrative = true` (`Updater.cs`, `CreateAdminRole()`), więc jeśli wdrożona baza już ją zawiera — a zawiera, skoro ludzie się logują — widok reguł będzie osiągalny dla administratora niezależnie od konfiguracji buildu. Sprawa `#if !RELEASE` dotyczy wyłącznie dostępu dla roli `Default` i pozostaje osobnym, niezależnym ryzykiem.

**4. Migracja `TargetTypeName` przy graduacji — potwierdzona cicha awaria.**
`GraduationService` nie emituje `namespace`; `FullName` przechodzi z `XafXPODynAssem.RuntimeEntities.X` na `XafXPODynAssem.Module.BusinessObjects.X`. Każda reguła na graduowanej encji przestaje działać bez żadnego sygnału.
- Opcje: (A) przy graduacji przepisać `TargetTypeName` we wszystkich regułach; (B) rozwiązywać typ po nazwie krótkiej z fallbackiem, gdy `FullName` nie trafia; (C) świadomie zaakceptować i ostrzec użytkownika w `GraduationWarningController`.
- Sugestia: (C) jako minimum obowiązkowe (ostrzeżenie jest tanie i uczciwe), (A) jeśli w zakresie. Sam (B) niesie ryzyko fałszywych trafień na encjach o zbieżnych nazwach krótkich.

### Ważne (do rozstrzygnięcia w specyfikacji)

**5. Parytet WinForms.** `AddStateMachine` jest tylko w `Blazor.Server/Startup.cs`; funkcja Workflow jest w praktyce tylko Blazorowa. Czy reguły wyglądu też mają być tylko Blazorowe (spójnie z precedensem), czy pełnić obie platformy?
- Domyślnie: **pełnić obie**, bo Wariant A daje to bez dodatkowego kosztu, a `.AddConditionalAppearance()` jest już w `Win/Startup.cs:30`. Przy Wariancie B parytet WinForms może być technicznie nieosiągalny bez restartu klienta i wtedy staje się częścią decyzji forka.

**6. Zapis przez `INonSecuredObjectSpaceFactory`.** Narzędzie omija uprawnienia XAF; dowolny rozmówca czatu tworzy regułę na dowolnej encji, także takiej, do której nie ma dostępu w UI.
- Domyślnie: **zaakceptować, spójnie z pozostałymi 19 narzędziami**, ale nazwać to jawnie w specyfikacji jako świadomy dług, a nie przeoczenie.

**7. Ukrycie pola jako cicha śmierć reguły.** `IsVisibleInListView = false` zostawia właściwość istniejącą, więc strażnik „czy właściwość się rozwiązuje" tego nie łapie.
- Opcje: (A) strażnik sprawdza dodatkowo widoczność pola; (B) ostrzeżenie przy ukrywaniu pola, na które wskazuje aktywna reguła (`FieldTypeChangeGuard`); (C) zignorować — objaw jest łagodny (brak kolorowania, nie awaria).
- Domyślnie: (B) — spójne z istniejącym zachowaniem strażników pól.

**8. Kolizja reguł i priorytety.** Dwie reguły na tym samym typie z różnymi kolorami. Pole `Priority` musi być realnie trwałe i realnie używane przy sortowaniu, nie być zaślepką.
- Domyślnie: `Priority` trwałe, reguły sortowane rosnąco, ostatnia wygrywa; udokumentować w prompcie systemowym, żeby AI potrafiło to wyjaśnić użytkownikowi.

**9. Twarda bramka potwierdzenia dla `build_appearance_rule`.** Konwencja `validate_*` → `build_*` nie broni LLM-owi wywołać samego `build_`.
- Domyślnie: zostać przy konwencji repo (bramka promptowa) + zamknąć orphan z decyzji 3, żeby pomyłka była odwracalna jednym kliknięciem. Dodatkowa bramka techniczna byłaby odstępstwem od wzorca całej warstwy narzędzi.

---

## Zakres

### Wymagane

- Encja `AppearanceRule` + rejestracja w `Module.cs`.
- Mechanizm dostarczania (A lub B) — po rozstrzygnięciu forka.
- Strażnik aktywności w stylu `IStateMachine.Active` z `Workflow.cs` — **nienegocjowalny**; bez niego dwie z trzech replik wywracają widoki na ~100 s po każdym utworzeniu encji.
- `validate_appearance_rule` + `build_appearance_rule` w `SchemaAIToolsProvider`.
- Sekcja `## Appearance Rules` w `SchemaDiscoveryService.GenerateSystemPrompt`.
- Uprawnienia i nawigacja w `Updater.cs` (z rozstrzygnięciem sprawy `#if !RELEASE`).
- Widok przeglądu/edycji reguł — jeśli decyzja 3 pójdzie w stronę (A).
- Ręczny scenariusz weryfikacyjny: test rozstrzygający fork, reguła na skasowanej encji, reguła na ukrytym polu, reguła przetrwała graduację.

### Poza zakresem

- **Kod dyktowania/mowy.** „Podyktowane zdanie" to zwykły tekst w istniejącym czacie — dyktowanie robi system operacyjny. W repo nie ma i nie ma być kodu rozpoznawania mowy.
- **Dokładanie reguł do odcisku metadanych.** `QueryMetadata` zostaje przy `CustomClass` i `CustomField`. Każda inna decyzja restartuje trzy repliki przy każdym podyktowanym zdaniu.
- **Rolowanie replik po jednej przy nowym obrazie.** Jawnie zabronione w `deploy/bezprzerwy/README.md`.
- **Utworzenie projektu testowego.** Nie ma go w repo i to zadanie go nie tworzy — **konsekwencja: cała weryfikacja jest ręczna, na żywej aplikacji**.
- **Zmiana zasad bezpieczeństwa warstwy narzędzi AI.** Jeśli `INonSecuredObjectSpaceFactory` ma zostać obudowany, to osobne zadanie dotykające wszystkich 19 narzędzi.
- **Przepisanie `wdroz.sh` na ogólny mechanizm N replik.** Jeśli decyzja 2 pójdzie w (a), piszemy wymianę trójki, nie uogólniamy skryptu.

---

## Ocena ryzyka

| Wymiar | Ocena | Uzasadnienie |
|---|---|---|
| Złożoność | **Wysoka** | 3 nowe/zmienione warstwy (encja trwała, przekrojowy mechanizm UI, narzędzie AI z zapisem), 8–11 plików |
| Integracja | **Wysoka** | efektywnie wszystkie widoki obiektowe; wyjątek w handlerze Wariantu A wywraca cały widok |
| Regresja | **Wysoka** | zero testów; nie da się wykazać, że istniejące widoki nie ucierpiały, inaczej niż ręcznym przeklikaniem |
| Wdrożenie | **Wysoka** | procedura wymiany obrazu przy trójce nienapisana, `wdroz.sh` odmawia, transfer ręczny, dokumentacja sama ze sobą sprzeczna |
| Dane | **Średnia** | tabela addytywna, poza odciskiem metadanych; główne ryzyko to ciche wiszące referencje, nie utrata danych |
| Bezpieczeństwo | **Średnia** | zapisy omijają uprawnienia XAF — istniejąca konwencja repo, ale rozszerzana na funkcję zmieniającą UI wszystkim użytkownikom |

**Łącznie: Wysokie.**
