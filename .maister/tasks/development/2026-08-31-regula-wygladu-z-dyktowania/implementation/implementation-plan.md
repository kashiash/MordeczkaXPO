# Plan wdrożenia: Reguły wyglądu z dyktowania

**Data**: 2026-08-31
**Specyfikacja**: [`spec.md`](spec.md) (789 linii — źródło prawdy)
**Repo**: `/Users/jacek/Projects/Brekhof/XafXPODynAssem`, kod w `XafXPODynAssem/XafXPODynAssem.Module/…` (segment nazwy jest zdublowany)
**Gałąź wyjściowa**: `testy/fundament-modulu` @ `0811cb8`

---

## TL;DR

Dwanaście grup zadań w jednym łańcuchu wymuszonym architekturą: rozpoznanie → paleta → encja → **walidator** → kontroler → ekran → narzędzia AI → prompt → cykl życia → uprawnienia → przegląd testów → weryfikacja ręczna.
Równolegle da się puścić tylko dwie pary (5 z 6, oraz 9 z 8) — reszta jest szeregowa, bo walidator jest wspólnym korzeniem trzech ścieżek użycia.
Testy piszemy do **istniejącego** projektu `XafXPODynAssem.Module.Tests` (61 testów, wszystkie zielone); obchodzenie drzewa kryterium z `CriteriaTests.cs` przenosimy do walidatora zamiast pisać od nowa.
Wdrożenie na Proxmox jest **poza tym planem** — użytkownik wstrzymał aktualizację środowisk, więc kryteria trzech replik (A-01…A-03) czekają, a ich lokalny odpowiednik weryfikujemy na jednej instancji.

## Kluczowe decyzje

- **Rozpoznanie empiryczne jest grupą zadań, nie założeniem** — dwa pytania ze specyfikacji (odczyt motywu z kontrolera, kanał alfa w `IAppearance.BackColor`) rozstrzygamy sondą na uruchomionej aplikacji, zanim ktokolwiek napisze odcienie palety. Wynik jest plikiem z werdyktem, nie zdaniem w commicie.
- **Enumy i paleta stoją w osobnej grupie przed encją** — `AppearanceColorRole` jest w sygnaturze palety, a `AppearanceRuleHealth` jest jednocześnie typem pola trwałego encji i typem zwracanym walidatora. Wydzielenie ich do `Module/Appearance/` rozcina cykl encja ↔ walidator, który powstałby, gdyby enum siedział w pliku walidatora (jak w specyfikacji). To jedyne odstępstwo od układu plików ze specyfikacji.
- **Walidator dostaje własną grupę przed wszystkim, co go używa** (kontroler, ekran, narzędzia AI). Pierwszy krok tej grupy nie pisze walidatora, tylko ustala, jak zbudować `ITypesInfo` w teście — z zapisanym wariantem awaryjnym.
- **Ekran reguł idzie przed narzędziami AI** i jest to zależność treściowa, nie kolejność z grzeczności: komunikaty odmowy i sukcesu kierują użytkownika do „Zarządzanie schematem → Reguły wyglądu", a ten ekran jest drogą cofnięcia, która czyni zapis przez `INonSecuredObjectSpaceFactory` akceptowalnym.
- **Kryteria negatywne pilnujemy w grupie, która mogłaby je złamać**, nie w zbiorczej grupie higieny: A-26 (odcisk metadanych nietknięty) na grupie encji, A-14 (`.xafml` nietknięty) na grupie ekranu, zakaz zapytania do bazy w `OnCollectAppearanceRules` na grupie kontrolera. `git diff` na zamknięciu grupy jest tani i łapie szkodę w miejscu jej powstania.
- **Nie optymalizujemy przedwcześnie.** Pomiar na żywej liście (`/Users/jacek/Projects/Brekhof/notatki/pomiar-regul-na-zywej-liscie.md`) pokazał, że przy 3 regułach na 1000 wierszy narzut mechanizmu jest nierozstrzygalny w czasie (0,8 ms przy szumie 6,3 ms), a sam silnik DevExpressa kosztuje +38 ms. Różnica pojawia się dopiero przy ~30 regułach. Czytelność i poprawność mają pierwszeństwo; jedyny twardy wymóg wydajnościowy zostaje: pre-ładowanie w `OnActivated`, nigdy zapytanie w `CollectAppearanceRules`.
- **Testy piszemy razem z kodem, do istniejącego projektu.** Specyfikacja mówi „zero testów w repo" — to nieaktualne od commita `0811cb8`. Sekcja „Podejście testowe" ze specyfikacji jest w tym punkcie nadpisana.

## Otwarte pytania i ryzyka

- **Seam `ITypesInfo` w testach walidatora — największe ryzyko wykonawcze.** Projekt testowy to gołe xUnit z `ProjectReference`; `GraduationTests` używa `ReflectionDictionary` + `InMemoryDataStore`, czyli metadanych XPO, a nie `ITypesInfo` XAF-a. Nie jest zweryfikowane, że `new TypesInfo()` + rejestracja typu zadziała bez rozgrzewki XAF-a. Krok 4.1 ma to rozstrzygnąć i ma zapisany wariant awaryjny: zwężenie seamu do wstrzykiwanego `Func<string, ITypeInfo>`. Serce mechanizmu nie może zostać bez testów.
- **Kierunek rozstrzygania konfliktu priorytetów (A-22) jest niepotwierdzony.** `AppearanceController.CombineAppearanceResults` jest wewnętrzny. Kontrakt specyfikacji: wyższy `Priority` wygrywa, sortujemy rosnąco. Sprawdzamy dopiero na uruchomionej aplikacji (krok 12.5). Wariant awaryjny zapisany: odwrócić `OrderBy` w `LoadRules()` i poprawić jedno zdanie w prompcie systemowym.
- **Liczba testów przekracza ramowy zakres 16–34.** Ten zakres zakłada 3–5 grup; tutaj grup kodowych jest osiem. Wiążący jest limit **2–8 na grupę**, który powtarza także sekcja „Podejście testowe" specyfikacji. Świadome odstępstwo, nie przeoczenie.
- **Kryteria trzech replik (A-01, A-02, A-03) i cała higiena wdrożeniowa (A-27) są odłożone razem z wdrożeniem na Proxmox.** Lokalny odpowiednik A-02 (`INSERT` wprost do bazy → świeża sesja przeglądarki → kolorowanie) wchodzi do grupy 12, bo dowodzi braku bufora w procesie bez trzech replik. A-21 (WinForms) też zostaje lokalnie — `XafXPODynAssem.Win` jest w rozwiązaniu.
- **Cały łańcuch grup 2–12 jest przechodnio zablokowany przez grupę 1, która wymaga uruchomionej aplikacji lokalnie.** Jeśli lokalne środowisko nie wstanie (rozbieżność bazy w `CLAUDE.md`, prostowana w kroku 10.4, jest tu sygnałem ostrzegawczym), jedenaście grup czeka na sondę. Tanie obejście: grupa 2 może wtedy wystawić enumy i **kształt** palety z odcieniami oznaczonymi jako oczekujące, odblokowując grupy 3–12, a same wartości odcieni wracają jako jeden krok uzupełniający po zapisaniu werdyktu. Zasada „nie zgadujemy" zostaje nienaruszona — wartości dalej czekają na dowód, tylko nie trzymają całego planu jako zakładnika.
- **Sonda z grupy 1 musi zostać cofnięta.** Tymczasowy `[Appearance(BackColor="#20…")]` na `CustomClass` nie ma prawa trafić do commita.
- **Pliki dzielone między grupami**: `AppearanceRule.cs` (grupy 3 i 6), `SchemaAIToolsProvider.cs` (grupy 7 i 9), `CriteriaTests.cs` (grupy 4 i 11), `GraduationTests.cs` (grupy 9 i 11), `SchemaGuardTests.cs` (grupy 9 i 11). Zadeklarowane w obu grupach każdej pary — wykonawca ma je szeregować, nie puszczać równolegle.

---

## Przegląd

| | |
|---|---|
| Grup zadań | **12** (8 kodowych, 1 rozpoznawcza, 1 higieny, 1 przeglądu testów, 1 weryfikacji ręcznej) |
| Kroków łącznie | **72** |
| Nowych testów | **36** w grupach kodowych + do **10** w przeglądzie = **36–46** |
| Zestaw testów po zakończeniu | 61 istniejących + 36–46 = **97–107** |
| Złożoność | **Wysoka** — 5 nowych plików produkcyjnych, 7 zmienianych, 3 punkty integracji z istniejącym silnikiem DevExpressa, 29 kryteriów akceptacji |
| Poza planem | Wdrożenie na Proxmox LXC 200 (świadomie odłożone przez użytkownika) |

**Projekt testowy**: `XafXPODynAssem/XafXPODynAssem.Module.Tests` (xUnit 2.9.2, net8.0, w `XafXPODynAssem.slnx`). Uruchamianie tylko nowych testów: `dotnet test XafXPODynAssem/XafXPODynAssem.Module.Tests --filter "FullyQualifiedName~<NazwaKlasyTestów>"`.

---

## Kroki wdrożenia

### Grupa 1: Rozpoznanie empiryczne — motyw i kanał alfa

**Zależności:** brak
**Pliki do zmiany:** `XafXPODynAssem/XafXPODynAssem.Module/BusinessObjects/CustomClass.cs` (wyłącznie sonda tymczasowa, cofana w kroku 1.5); wynik zapisywany do `.maister/tasks/development/2026-08-31-regula-wygladu-z-dyktowania/analysis/rozpoznanie-motyw-i-alfa.md`
**Szacowana liczba kroków:** 6

Ta grupa nie pisze kodu produkcyjnego. Jej jedynym produktem jest werdykt, od którego zależy kształt palety w grupie 2. Bez niego paleta byłaby zgadywana — a specyfikacja wprost zakazuje zgadywania w tych dwóch punktach.

- [ ] 1.0 Rozstrzygnąć dwa pytania empiryczne przed napisaniem palety
  - [ ] 1.1 Sprawdzić w kodzie, czy aktywny motyw DevExpress Blazor jest odczytywalny z kontrolera
    - Punkt wyjścia: `XafXPODynAssem.Blazor.Server/appsettings.json` — sekcja `ThemeSwitcher` z czterema motywami, w tym `Blazing Dark` (`blazing-dark.bs5.min.css`) i `Office White`
    - Ustalić, czy przełącznik zapisuje wybór per użytkownik (model aplikacji / `ModelDifference`), czy globalnie
    - Ustalić, czy z poziomu `ViewController` istnieje ścieżka do nazwy aktywnego motywu (usługa DI, model aplikacji, cookie obwodu)
  - [ ] 1.2 Wstawić sondę alfy: tymczasowy `[Appearance("SondaAlfa", TargetItems = "*", Criteria = "Status = 0", Context = "ListView", BackColor = "#20DC3545")]` na `CustomClass`
    - Osiem cyfr szesnastkowych, alfa `0x20` — tak jak przyszła paleta półprzezroczysta
  - [ ] 1.3 Uruchomić aplikację (`dotnet run --project XafXPODynAssem/XafXPODynAssem.Blazor.Server`, `https://localhost:5001`, logowanie `Admin` bez hasła), otworzyć listę klas użytkownika i odczytać z DOM wyliczony `background-color` pokolorowanego wiersza
    - Wynik `rgba(220, 53, 69, 0.125)` → alfa przechodzi; wynik `rgb(...)` bez alfy → nie przechodzi
  - [ ] 1.4 Powtórzyć krok 1.3 po przełączeniu motywu na `Blazing Dark` i zanotować, czy ta sama wartość jest czytelna na obu motywach
  - [ ] 1.5 **Cofnąć sondę** — usunąć atrybut z `CustomClass.cs`, potwierdzić czystym `git diff` na tym pliku
  - [ ] 1.6 Zapisać werdykt do `analysis/rozpoznanie-motyw-i-alfa.md`

**Kryteria odbioru:**
- Plik `analysis/rozpoznanie-motyw-i-alfa.md` zawiera **dwie odpowiedzi tak/nie** wraz z konsekwencją każdej:
  - motyw odczytywalny z kontrolera? → paleta ma **jeden** zestaw odcieni czy **dwa** (jasny/ciemny)
  - alfa przechodzi do CSS? → paleta ma **sześć** wartości półprzezroczystych czy **dwanaście** wartości nieprzezroczystych
- Werdykt jest poparty odczytem z DOM lub z kodu, nie rozumowaniem
- `git diff XafXPODynAssem/XafXPODynAssem.Module/BusinessObjects/CustomClass.cs` jest pusty — sonda cofnięta

---

### Grupa 2: Enumy i paleta ról

**Zależności:** Grupa 1 (wyłącznie dla doboru odcieni; kształt palety i enumy nie zależą od rozpoznania)
**Pliki do zmiany:**
`XafXPODynAssem/XafXPODynAssem.Module/Appearance/AppearanceColorRole.cs`,
`XafXPODynAssem/XafXPODynAssem.Module/Appearance/AppearanceRuleHealth.cs`,
`XafXPODynAssem/XafXPODynAssem.Module/Appearance/AppearanceRolePalette.cs`,
`XafXPODynAssem/XafXPODynAssem.Module.Tests/AppearancePaletteTests.cs`
**Visual References:**
- mockup: `analysis/design-context/mockups/regu-a-zak-adka-wygl-d.html`
  element: `component:probka-roli`
  locator: grupa „Rola koloru" — lista rozwijana z próbką koloru przy każdej z sześciu nazw
  acceptance: paleta zwraca parę (tło, czcionka) dla każdej z sześciu ról; sześć ról i ich polskie etykiety dokładnie jak na makiecie: Błąd, Ostrzeżenie, Sukces, Informacja, Wyciszone, Wyróżnione; `None` zwraca parę `null`/`null`, nie `Color.Empty`
**Szacowana liczba kroków:** 6

- [ ] 2.0 Wystawić rolę koloru i paletę rozwiązującą ją na `Color?`
  - [ ] 2.1 Napisać 5 testów w `AppearancePaletteTests.cs`
    - Parser `#AARRGGBB` z alfą `0x80` i wyżej **nie rzuca** — `uint.Parse(..., NumberStyles.HexNumber)` + `Color.FromArgb(unchecked((int)value))`; przypadek `#FF0000FF` to dokładna pułapka, na której wykłada się `ColorValueConverter` Fleetmana
    - Wejście puste, `null` i śmieciowe zwracają **`null`**, nigdy `Color.Empty` (bo `Color.Empty.Name` to `"0"` i cicho przechodzi przez sprawdzenia na `null`)
    - `Resolve(AppearanceColorRole.None)` zwraca `(null, null)`
    - Każda z sześciu ról zwraca parę, w której **oba** składniki są ustawione (nigdy samo tło)
    - Nadpisanie ustawione tylko z jednej strony jest **ignorowane w całości** i rozstrzyganie spada na rolę
  - [ ] 2.2 Zadeklarować `enum AppearanceColorRole { None, Error, Warning, Success, Info, Muted, Highlight }` z polskimi `[XafDisplayName]`/`[ImageName]` wg tabeli ról ze specyfikacji
  - [ ] 2.3 Zadeklarować `enum AppearanceRuleHealth { Healthy = 0, Warning = 1, Broken = 2 }` z etykietami „Sprawna" / „Ostrzeżenie" / „Niesprawna"
    - **Odstępstwo od specyfikacji**: enum stoi we własnym pliku w `Module/Appearance/`, nie w pliku walidatora — dzięki temu encja nie zależy od walidatora, a walidator od encji
  - [ ] 2.4 Napisać `AppearanceRolePalette` — statyczna, bezstanowa, bez zależności od bazy: `Resolve(AppearanceColorRole)` oraz `TryParseArgb(string, out Color)`
    - Odcienie dobrane wg werdyktu z grupy 1 (jeden zestaw vs dwa; półprzezroczyste vs nieprzezroczyste)
  - [ ] 2.5 Napisać metodę rozstrzygania kolejności: nadpisanie (oba ustawione i oba parsujące) → rola → `null`
  - [ ] 2.6 Uruchomić **wyłącznie** `--filter "FullyQualifiedName~AppearancePaletteTests"`

**Kryteria odbioru:**
- 5 nowych testów przechodzi; istniejące 61 nietknięte
- Żadna ścieżka palety nie rzuca wyjątku na wejściu śmieciowym
- Odcienie mają zapisane uzasadnienie odsyłające do `analysis/rozpoznanie-motyw-i-alfa.md`
- Implementacja realizuje kryterium `acceptance` z sekcji Visual References

---

### Grupa 3: Encja `AppearanceRule` i rejestracja typu

**Zależności:** Grupa 2
**Pliki do zmiany:**
`XafXPODynAssem/XafXPODynAssem.Module/BusinessObjects/AppearanceRule.cs` (**dzielony z grupą 6**),
`XafXPODynAssem/XafXPODynAssem.Module/Module.cs`,
`XafXPODynAssem/XafXPODynAssem.Module.Tests/AppearanceRuleTests.cs`
**Szacowana liczba kroków:** 7

- [ ] 3.0 Wystawić regułę jako obiekt trwały implementujący `IAppearanceRuleProperties`
  - [ ] 3.1 Napisać 5 testów w `AppearanceRuleTests.cs` (wzorzec `GraduationTests`: `SimpleDataLayer` + `ReflectionDictionary` + `InMemoryDataStore`)
    - Puste `Criteria` widziane przez interfejs jako `"True"`; puste `TargetItems` jako `"*"` — normalizacja, na której brakuje Brekhofowi
    - `IAppearanceRuleProperties.Method` zwraca `string.Empty`, **nigdy `null`**; `AppearanceItemType` zwraca `"ViewItem"`; `IAppearance.Enabled` zwraca `null`
    - Wszystkie settery członków interfejsu są puste — zapis przez interfejs nie zmienia pola trwałego
    - `ResolveTargetType()` na nieistniejącej nazwie zwraca `null`, nie rzuca
    - `Priority` domyślnie `10`, `IsEnabled` domyślnie `true`, `AppearanceContext` domyślnie `ListView`
  - [ ] 3.2 Napisać klasę: `BaseObject`, konstruktor `(Session session)`, wszystkie pola przez `SetPropertyValue(nameof(X), ref x, value)` — wzorzec `Workflow.cs`
    - Pola trwałe dokładnie wg tabeli ze specyfikacji: `Name`, `TargetTypeName`, `Criteria`, `TargetItems`, `AppearanceContext`, `ViewId`, `ColorRole`, `BackColorOverride`, `FontColorOverride`, `RuleFontStyle`, `ItemVisibility`, `Priority`, `IsEnabled`, `Health`, `Diagnosis`, `CheckedOn`
    - `TargetTypeName` przechowuje **`FullName`**, nigdy `AssemblyQualifiedName` — assembly runtime jest generowane w pamięci i jego tożsamość zmienia się przy każdej rekompilacji
    - `AppearanceContext` to enum **DevExpressa** (`DevExpress.ExpressApp.ConditionalAppearance.AppearanceContext`), nie własny
    - `RuleFontStyle` to `DevExpress.Drawing.DXFontStyle?`, **nie** `System.Drawing.FontStyle`
  - [ ] 3.3 Zaimplementować dwanaście członków `IAppearanceRuleProperties` wg mapy ze specyfikacji
    - `Color? IAppearance.BackColor` i `DXFontStyle? IAppearance.FontStyle` **jawną implementacją interfejsu** — nazwy kolidują z polami trwałymi
    - Kolor rozstrzygany przez `AppearanceRolePalette` z grupy 2
  - [ ] 3.4 Dodać `ResolveTargetType()` przez `typesInfo.FindTypeInfo(TargetTypeName)?.Type`, **bez cache'owania wyniku** (wzorzec `Workflow.cs:129`) — nigdy `Type.GetType`, który nie widzi typów kompilowanych Roslynem
  - [ ] 3.5 Dodać `[NonPersistent] Type TargetType` z `[TypeConverter(typeof(LocalizedClassInfoTypeConverter))]` + `[ImmediatePostData]`; setter czyści `Criteria` i `TargetItems` przy zmianie typu
  - [ ] 3.6 Dopisać `AdditionalExportedTypes.Add(typeof(BusinessObjects.AppearanceRule));` w `Module.cs`, tuż za trzema typami `Workflow` (linie 102–104)
  - [ ] 3.7 Uruchomić `--filter "FullyQualifiedName~AppearanceRuleTests"`

**Kryteria odbioru:**
- 5 nowych testów przechodzi
- **A-26 zweryfikowane na zamknięciu grupy**: `git diff XafXPODynAssem/XafXPODynAssem.Module/Module.cs` pokazuje **wyłącznie** dopisaną linię `AdditionalExportedTypes` — zero zmian w `QueryMetadata`, `GetMetadataFingerprint`, `ComputeFingerprint`. Dopisanie reguł do odcisku restartowałoby wszystkie trzy repliki przy każdym podyktowanym zdaniu
- `dotnet build XafXPODynAssem.slnx` przechodzi

---

### Grupa 4: Walidator reguł — serce mechanizmu

**Zależności:** Grupa 2, Grupa 3
**Pliki do zmiany:**
`XafXPODynAssem/XafXPODynAssem.Module/Validation/AppearanceRuleValidator.cs`,
`XafXPODynAssem/XafXPODynAssem.Module.Tests/AppearanceRuleValidatorTests.cs`,
`XafXPODynAssem/XafXPODynAssem.Module.Tests/CriteriaTests.cs` (**dzielony z grupą 11**)
**Szacowana liczba kroków:** 7

Jeden walidator, trzy miejsca użycia (narzędzie AI przed zapisem, kontroler przy aktywacji widoku, ekran reguł). Warunek konieczny: **bez `IObjectSpace` i bez zapytań do bazy** — miejsce drugie to ścieżka aktywacji widoku, na której zapytanie jest zakazane.

- [ ] 4.0 Napisać jeden walidator używany w trzech miejscach
  - [ ] 4.1 **Ustalić seam `ITypesInfo` na potrzeby testów — zanim powstanie kod walidatora**
    - Sprawdzić, czy `DevExpress.ExpressApp.DC.TypesInfo` da się skonstruować i zasilić typem testowym bez rozgrzewki XAF-a
    - **Wariant awaryjny, jeśli nie**: zwęzić seam — rdzeń walidatora przyjmuje wstrzykiwane `Func<string, ITypeInfo>` (albo minimalny własny interfejs), a nakładka publiczna `Check(..., ITypesInfo)` go opakowuje. Testy podstawiają atrapę
    - Wynik zapisać jednym akapitem w komentarzu klasy — kolejny czytelnik nie może na to wpadać od nowa
  - [ ] 4.2 Napisać 8 testów w `AppearanceRuleValidatorTests.cs` — po jednym na sprawdzenie 1–10 ze specyfikacji, sklejając te, które dzielą kształt
    - `Broken`: brak encji docelowej; encja nierozwiązywalna; celowanie w klasę bazową (`BaseObject`/`object`/`XPBaseObject`) — **A-13**
    - `Broken`: puste kryterium; kryterium nieparsujące (komunikat parsera trafia do `Diagnosis`)
    - `Broken`: kryterium nad nieistniejącą właściwością, z podpowiedzią „Czy chodziło o …?" przy odległości Levenshteina ≤ 2
    - **`Healthy`: kryterium z funkcją — `[TerminPlatnosci] < LocalDateTimeToday()` przechodzi** (A-07). To jest test, który pilnuje, że `FunctionOperator` nie jest brany za właściwość
    - `Warning`, nie `Broken`: pole istnieje, ale `IsVisibleInListView = false` (A-25); ustawione tylko jedno z dwóch nadpisań koloru; reguła nie zmieniająca niczego
    - Walidator zatrzymuje się na pierwszym `Broken`; ostrzeżenia zbiera i skleja spacją
  - [ ] 4.3 Przenieść obchodzenie drzewa kryterium z `CriteriaTests.CollectProperties`/`Walk` do walidatora
    - Kod jest gotowy i przetestowany: `OperandProperty`, `UnaryOperator`, `BinaryOperator`, `GroupOperator`, `FunctionOperator`, `InOperator`, `BetweenOperator`
    - Sprawdzamy **wyłącznie węzły `OperandProperty`**; wywołania funkcji i stałe pomijamy
    - W `CriteriaTests.cs` zostawić testy zachowania `CriteriaOperator`, a prywatne `Walk` zastąpić wywołaniem publicznej metody walidatora — jedna implementacja, nie dwie
  - [ ] 4.4 Rozwijać ścieżki kropkowane (`Kontrahent.NazwaKlienta`) człon po członie przez `ITypeInfo.FindMember(...)`, przechodząc na `MemberTypeInfo` referencji
  - [ ] 4.5 Napisać `Check(string targetTypeName, string criteria, string targetItems, string backColorOverride, string fontColorOverride, ITypesInfo typesInfo)` zwracające `readonly record struct AppearanceRuleCheck(AppearanceRuleHealth Health, string Diagnosis)`
    - `typesInfo` **wstrzykiwany, nigdy pobierany w środku** — kontroler poda `ObjectSpace.TypesInfo` swojej repliki, narzędzie AI `XafTypesInfo.Instance`
    - Komunikaty po polsku, dokładnie wg tabeli ze specyfikacji (wiersze 1–10)
  - [ ] 4.6 Dodać nakładkę `Check(AppearanceRule rule, ITypesInfo typesInfo)` i wpiąć ją w `AppearanceRule.OnSaving()` — jedno miejsce pokrywające wszystkie ścieżki zapisu (ekran, narzędzie AI, import), ustawiające `Health`, `Diagnosis` i `CheckedOn` **tylko na własnym obiekcie**
  - [ ] 4.7 Uruchomić `--filter "FullyQualifiedName~AppearanceRuleValidatorTests|FullyQualifiedName~CriteriaTests"`

**Kryteria odbioru:**
- 8 nowych testów przechodzi; wszystkie dotychczasowe testy `CriteriaTests` dalej zielone po przeniesieniu obchodzenia drzewa
- Walidator nie ma ani jednego odwołania do `IObjectSpace`, `Session` ani `XafTypesInfo.Instance` — przegląd kodu przez `grep`
- `OnSaving()` woła walidator i nie zapisuje niczego poza własnym obiektem (żadnego zewnętrznego bufora — to błąd DataDrive'a, który przy wycofanej transakcji zostawia regułę-widmo)

---

### Grupa 5: Kontrolery — dostarczenie reguł do silnika

**Zależności:** Grupa 3, Grupa 4
**Pliki do zmiany:**
`XafXPODynAssem/XafXPODynAssem.Module/Controllers/AppearanceRuleViewController.cs`,
`XafXPODynAssem/XafXPODynAssem.Module/Controllers/AppearanceRuleEditorController.cs`,
`XafXPODynAssem/XafXPODynAssem.Module.Tests/AppearanceRuleSelectionTests.cs`
**Visual References:**
- mockup: `analysis/design-context/mockups/efekt-lista-faktur.html`
  element: `screen:efekt-faktury`
  locator: cała tabela faktur — pokolorowane wiersze niezapłaconych
  acceptance: efekt widoczny na **zwykłej liście faktur, bez żadnego dodatkowego elementu interfejsu** — kontroler nie dokłada paska, przycisku ani kolumny; kolorowanie obejmuje cały wiersz przy `TargetItems = "*"`
**Szacowana liczba kroków:** 7

- [ ] 5.0 Wpiąć reguły z bazy w `AppearanceController.CollectAppearanceRules`
  - [ ] 5.1 Napisać 4 testy w `AppearanceRuleSelectionTests.cs` na wydzieloną, czystą metodę selekcji i sortowania (bez `Frame`, bez XAF-a)
    - Reguła, której typ się nie rozwiązuje, jest pomijana (A-11 — replika jeszcze nie skompilowała encji)
    - Dopasowanie typu przez `IsAssignableFrom`, nigdy `StartsWith`: reguła napisana dla `Faktura` **nie** łapie `FakturaPozycja`
    - Niepusty `ViewId` różny od `View.Id` odsiewa regułę; pusty `ViewId` przepuszcza
    - Sortowanie `OrderBy(Priority).ThenBy(Name).ThenBy(Oid)` jest deterministyczne przy równych priorytetach
  - [ ] 5.2 Napisać `AppearanceRuleViewController : ObjectViewController<ObjectView, object>` z sekwencją `OnActivated` **bez odstępstw**:
    `base` → strażniki wejściowe (`View is not ObjectView`; `View.ObjectSpace is NonPersistentObjectSpace` — dopasowaniem **wzorca**, nie `GetType() != typeof(...)`; `IsDisposed`; `ObjectTypeInfo?.Type == null`) → `LoadRules()` w `try/catch` → pusta lista kończy bez subskrypcji → `Frame.GetController<AppearanceController>()` → **`ResetRulesCache()` → subskrypcja → `Refresh()`**
    - Kolejność trzech ostatnich kroków jest nienegocjowalna: bez `ResetRulesCache()` zdarzenie w ogóle się nie odpala, bo DevExpress zacache'ował „brak reguł dla tego typu"
  - [ ] 5.3 Napisać `LoadRules()` — jedyne miejsce rozmawiające z bazą, w `OnActivated`
    - Przestrzeń obiektów z `INonSecuredObjectSpaceFactory` (`Application.ServiceProvider`), zapamiętana w polu, zwalniana w `OnDeactivated` — **nie** `View.ObjectSpace`, bo zabezpieczona przestrzeń zwróciłaby nie-adminowi pustą listę i kolorowanie umarłoby po cichu (A-28)
    - Zapytanie filtruje **wyłącznie** po `IsEnabled = True`. `Health`/`Diagnosis` **nie mogą** być filtrem — reguła oznaczona jako niesprawna w oknie graduacji nigdy nie zostałaby ponownie oceniona (A-23)
    - Filtrowanie w pamięci: rozwiązanie typu → `IsAssignableFrom` → `ViewId` → `Check(...).Health == Broken` pomija + log. `try/catch` **per reguła**, nigdy wokół całego ładowania (błąd Brekhofa: jedna zła reguła wyłącza wszystkie)
  - [ ] 5.4 Napisać handler `OnCollectAppearanceRules` — `try/catch` wokół **całego** ciała, `e.AppearanceRules.AddRange(_rules)`, zero logiki poza tym (A-12)
  - [ ] 5.5 Napisać `OnDeactivated`: odsubskrybowanie → `_rules = null` → zwolnienie własnej przestrzeni obiektów → `base`
  - [ ] 5.6 Napisać `AppearanceRuleEditorController : ObjectViewController<ObjectView, AppearanceRule>` — na `ObjectSpace.Committed` (**nigdy `OnSaving`**) resetuje cache własnej ramki i woła `Refresh()`
    - Na widokach reguł oba kontrolery działają naraz i **tak ma być** — udokumentować to komentarzem, żeby nikt nie „naprawił" tego przez usunięcie subskrypcji
    - Żadnego statycznego eventu rozgłaszającego: trzy repliki to trzy procesy, a w Blazor Server każdy użytkownik to osobny obwód
  - [ ] 5.7 Uruchomić `--filter "FullyQualifiedName~AppearanceRuleSelectionTests"`

**Kryteria odbioru:**
- 4 nowe testy przechodzą
- **Twardy wymóg wydajnościowy zweryfikowany przeglądem kodu**: w ciele `OnCollectAppearanceRules` nie ma ani jednego wywołania `ObjectSpace`, `CreateObjectSpace`, `GetObjects` ani `Session` — reguły są pre-ładowane w `OnActivated`
- `try/catch` obejmuje całe ciało handlera (A-12)
- Kontrolery leżą w `Module/Controllers/` i nie są nigdzie rejestrowane ręcznie — parytet Blazor/WinForms wychodzi z automatycznego skanowania XAF-a (W-20)
  - **Zweryfikowane, nie odziedziczone**: `.AddConditionalAppearance()` stoi w **obu** hostach — `Blazor.Server/Startup.cs:53` i `Win/Startup.cs:30`. Silnik jest podpięty po obu stronach, dokładamy źródło reguł, nie silnik. (Dla kontrastu `.AddStateMachine` jest **tylko** w Blazorze, linia 70 — asymetria, która dotyczy przepływów, nie reguł wyglądu.) Grupa 5 **nie** ma kroku rejestracji po stronie WinForms i nie potrzebuje go
- Implementacja realizuje kryterium `acceptance` z sekcji Visual References

---

### Grupa 6: Ekran reguł

**Zależności:** Grupa 3, Grupa 4
**Pliki do zmiany:**
`XafXPODynAssem/XafXPODynAssem.Module/BusinessObjects/AppearanceRule.cs` (**dzielony z grupą 3**),
`XafXPODynAssem/XafXPODynAssem.Module/Controllers/AppearanceRuleActionsController.cs`,
`XafXPODynAssem/XafXPODynAssem.Module.Tests/AppearanceRuleScreenTests.cs`
**Visual References:**
- mockup: `analysis/design-context/mockups/lista-regu-wygl-du.html`
  element: `screen:lista-regul`
  locator: nagłówek tabeli, wiersz filtra, pasek akcji nad tabelą, podsumowanie pod tabelą
  acceptance: kolumny dokładnie w kolejności Nazwa, Encja docelowa, Kryterium, Rola koloru (z próbką), Kontekst, Priorytet, Włączona, **Stan**; pasek akcji: Nowa / Usuń / Odśwież / **Sprawdź reguły** / **Klonuj**; wiersze niesprawne na czerwono, ostrzeżenia na żółto, wyłączone wyszarzone kursywą; podsumowanie w formie „N reguł · M niesprawnych · …"
- mockup: `analysis/design-context/mockups/lista-regu-wygl-du.html`
  element: `component:odznaka-stanu`
  locator: kolumna „Stan" — odznaka w komórce
  acceptance: cztery warianty odznaki — Sprawna / Ostrzeżenie / Niesprawna / Wyłączona; stan wyłączenia wynika z `IsEnabled`, nie z `Health`
- mockup: `analysis/design-context/mockups/regu-a-zak-adka-definicja.html`
  element: `screen:regula-definicja`
  locator: zakładka „Definicja", grupa „Co reguła obejmuje" + panel pod formularzem
  acceptance: pola w kolejności Nazwa, Encja docelowa (lista rozwijana), Kryterium (edytor filtra podpowiadający **wyłącznie** pola wybranej encji), Pola docelowe (**checkboxy**, `wszystkie (*)` pierwsze — nie wolne pole tekstowe), Kontekst, Ogranicz do widoku, Priorytet, Włączona; akcje „Sprawdź tę regułę" i „Klonuj"
- mockup: `analysis/design-context/mockups/regu-a-zak-adka-definicja.html`
  element: `component:panel-diagnozy`
  locator: panel pod formularzem, widoczny tylko dla reguły niesprawnej
  acceptance: panel pokazuje powód niesprawności, podpowiedź naprawy oraz znacznik „Wykryto przy zapisie · {data}" zasilany z pola `CheckedOn`
- mockup: `analysis/design-context/mockups/regu-a-zak-adka-wygl-d.html`
  element: `screen:regula-wyglad`
  locator: zakładka „Wygląd" — trzy grupy od góry do dołu
  acceptance: kolejność grup to Rola koloru (z podglądem) → Pozostałe efekty (Styl czcionki jako cztery pola wyboru, Widoczność) → **na samym dole** „Nadpisanie kolorem — dla administratora, pomija rolę" z notkami „Ustawiając jedno, ustaw drugie" i „Asystent nie korzysta z tych pól"
- mockup: `analysis/design-context/mockups/regu-a-zak-adka-wygl-d.html`
  element: `component:probka-roli`
  locator: grupa „Rola koloru" — podgląd trzech wierszy
  acceptance: próbka pokazuje **rolę rozwiązaną w bieżącej skórce**, nigdy zapisanego ARGB
**Szacowana liczba kroków:** 7

- [ ] 6.0 Dać człowiekowi ekran, na którym błąd asystenta da się cofnąć
  - [ ] 6.1 Napisać 3 testy w `AppearanceRuleScreenTests.cs`
    - `GetCheckedListBoxItems(nameof(TargetItems))` stawia `*` **zawsze pierwsze** i pomija składowe z `IsService == true` oraz `IsVisible == false`
    - Zmiana `TargetType` wyzwala `ItemsChanged` i czyści `Criteria` oraz `TargetItems`
    - Klonowanie kopiuje wszystkie pola definicji, ale **nie** kopiuje `Health`, `Diagnosis` ani `CheckedOn` — klon przelicza stan sam, przy zapisie
  - [ ] 6.2 Dodać atrybuty klasy: `[DefaultClassOptions]`, `[NavigationItem("Zarządzanie schematem")]`, `[DefaultProperty(nameof(Name))]`, `[XafDisplayName("Reguła wyglądu")]` — wzorzec `Workflow.cs:27-28`, **bez tykania `.xafml`** (W-08)
  - [ ] 6.3 Dodać trzy statyczne `[Appearance]` wyróżniające wiersze na liście samych reguł — wzorzec `CustomClass.cs:23-32`: `Health = 2` na czerwono, `Health = 1` na żółto, `IsEnabled = False` szare kursywą (W-16)
    - To jedyne miejsce w projekcie, gdzie regułę wyglądu wolno zapisać atrybutem — dotyczy klasy znanej w czasie kompilacji
  - [ ] 6.4 Zaimplementować `ICheckedListBoxItemsProvider` (`GetCheckedListBoxItems` + event `ItemsChanged`) i dodać `[EditorAlias(EditorAliases.CheckedListBoxEditor)]` na `TargetItems` oraz `[CriteriaOptions(nameof(TargetType))]` + `[EditorAlias(EditorAliases.CriteriaPropertyEditor)]` na `Criteria` (A-18)
  - [ ] 6.5 Ułożyć zakładki „Definicja" i „Wygląd" atrybutami układu XAF (`[XafDisplayName]`, grupy, indeksy) wg makiet — nadpisanie kolorem na samym dole zakładki „Wygląd", opisane jako wyjście awaryjne
  - [ ] 6.6 Napisać `AppearanceRuleActionsController` z trzema akcjami: „Sprawdź reguły" (lista — przelicza `Health`/`Diagnosis`/`CheckedOn` dla zaznaczonych lub wszystkich), „Sprawdź tę regułę" (szczegóły), „Klonuj" (W-10, W-19, A-20)
  - [ ] 6.7 Uruchomić `--filter "FullyQualifiedName~AppearanceRuleScreenTests"`

**Kryteria odbioru:**
- 3 nowe testy przechodzą
- **A-14 zweryfikowane na zamknięciu grupy**: `git status` nie pokazuje ani jednego zmienionego pliku `.xafml`
- Pozycja „Reguły wyglądu" pojawia się w grupie „Zarządzanie schematem", obok „Klasy użytkownika" i „Przepływy"
- Implementacja realizuje **każde** kryterium `acceptance` z sekcji Visual References (sześć pozycji)

---

### Grupa 7: Narzędzia AI

**Zależności:** Grupa 4, Grupa 6
**Pliki do zmiany:**
`XafXPODynAssem/XafXPODynAssem.Module/Services/SchemaAIToolsProvider.cs` (**dzielony z grupą 9**),
`XafXPODynAssem/XafXPODynAssem.Module.Tests/AppearanceToolsTests.cs`
**Visual References:**
- mockup: `analysis/design-context/mockups/czat-utworzenie-regu-y-z-dyktowania.html`
  element: `screen:czat-utworzenie`
  locator: cała rozmowa od pierwszej wiadomości użytkownika do potwierdzenia zapisu
  acceptance: przebieg dokładnie w tej kolejności — dyktowanie → wywołanie `sprawdz_regule_wygladu` widoczne w czacie → **jedno** dopytanie → podsumowanie tabelką → `utworz_regule_wygladu` → komunikat sukcesu z tabelką i zdaniem o F5 („wdrożenie nie jest potrzebne"), z odnośnikiem do „Zarządzanie schematem → Reguły wyglądu"
- mockup: `analysis/design-context/mockups/czat-walidacja-odrzuca-bubla.html`
  element: `screen:czat-blad`
  locator: blok odmowy i następująca po nim wiadomość asystenta
  acceptance: odmowa wraca z **listą realnych pól encji**; asystent sam proponuje najbliższe pole i czeka na potwierdzenie zamiast pytać, którego użyć; o rolę koloru dopytuje osobno, mówiąc wprost, że jej nie zgaduje; **zero rekordów w tabeli** po odmowie
- mockup: `analysis/design-context/mockups/czat-utworzenie-regu-y-z-dyktowania.html`
  element: `component:wywolanie-narzedzia`
  locator: bloki wywołania narzędzia w obu makietach czatu
  acceptance: blok pokazuje nazwę narzędzia i jego wynik, także na ścieżce odmowy — konwencja `PROBLEM:` / `MISSING:` jak w istniejącym `validate_report_spec`
**Szacowana liczba kroków:** 6

- [ ] 7.0 Dodać dwa narzędzia tworzące regułę z dyktowanego zdania
  - [ ] 7.1 Napisać 5 testów w `AppearanceToolsTests.cs` — na wydzielone, czyste metody budujące odpowiedzi (bez `IObjectSpace`, bez czatu)
    - Odmowa przy nieistniejącym polu zawiera **listę dostępnych pól** encji (A-05)
    - Brak roli koloru daje `MISSING:` i sześć polskich opcji, nigdy domyślnej roli (A-06)
    - Komunikat sukcesu zawiera zdanie „wdrożenie nie jest potrzebne" i odnośnik do „Zarządzanie schematem → Reguły wyglądu" (A-08)
    - Rola spoza sześciu wartości jest odrzucana, nie mapowana „na najbliższą"
    - Każda ścieżka odmowy kończy się zdaniem „Żadna reguła nie została zapisana."
  - [ ] 7.2 Napisać `ValidateAppearanceRule` (`sprawdz_regule_wygladu`) — tylko odczyt, `[Description]` po angielsku dokładnie wg specyfikacji, konwencja `PROBLEM:` / `MISSING:` z `validate_report_spec` jako wzorzec
  - [ ] 7.3 Napisać `CreateAppearanceRule` (`utworz_regule_wygladu`) — waliduje **ponownie, wewnętrznie**, przed każdym zapisem
    - Bramka potwierdzenia jest wyłącznie promptowa; nic technicznie nie broni modelowi pominąć kroku sprawdzenia, dlatego narzędzie zapisujące nie ufa mu na słowo
    - Kontrakt **nie przyjmuje** `backColor`, `fontColor`, `viewId`, `fontStyle` ani `visibility` — to furtki administratora
    - Zapis: `CreateObjectSpaceForType(typeof(AppearanceRule))` → `CreateObject<AppearanceRule>()` → `CommitChanges()`, wzorzec `BuildReport` (linie 87–123)
    - Odnotować w komentarzu, że jest to świadome obejście bezpieczeństwa XAF przez `INonSecuredObjectSpaceFactory`, spójne z 19 istniejącymi narzędziami; odwracalność zapewnia ekran z grupy 6
  - [ ] 7.4 Dopisać dwa wpisy w `CreateTools()`, w nowej grupie za narzędziami workflow — `SchemaAIToolsProvider` jest singletonem, **żadnej nowej rejestracji DI**
  - [ ] 7.5 Objąć całe ciała obu metod `try/catch` zwracającym `$"Error ...: {ex.Message}"` — narzędzie nigdy nie rzuca przez granicę
  - [ ] 7.6 Uruchomić `--filter "FullyQualifiedName~AppearanceToolsTests"`

**Kryteria odbioru:**
- 5 nowych testów przechodzi
- Walidator z grupy 4 jest wołany na **obu** ścieżkach, a `Broken` blokuje zapis
- Teksty `[Description]` po angielsku, teksty odmowy po polsku — konwencja repo
- Implementacja realizuje każde kryterium `acceptance` z sekcji Visual References (trzy pozycje)

---

### Grupa 8: Prompt systemowy i podpowiedź w czacie

**Zależności:** Grupa 7
**Pliki do zmiany:**
`XafXPODynAssem/XafXPODynAssem.Module/Services/SchemaDiscoveryService.cs`,
`XafXPODynAssem/XafXPODynAssem.Module/Services/AIChatDefaults.cs`,
`XafXPODynAssem/XafXPODynAssem.Module.Tests/SystemPromptTests.cs`
**Szacowana liczba kroków:** 4

To **jedyna istniejąca bramka na człowieka** — czysto tekstowa. Prompt jest odświeżany przed każdą turą (`AIChatService.AskAsync` → `RefreshSystemPrompt()`), więc sekcja działa od razu po wdrożeniu kodu.

- [ ] 8.0 Nauczyć model, kiedy i jak sięgać po nowe narzędzia
  - [ ] 8.1 Napisać 2 testy w `SystemPromptTests.cs`
    - Wygenerowany prompt zawiera sekcję `## Appearance Rules (colouring rows and fields)` i stoi ona **między** `## Workflows (State Machines)` a `## Supported Field Types`
    - Sekcja wymienia obie nazwy narzędzi, wszystkie sześć ról i zdanie zakazujące zgadywania („NEVER guess")
  - [ ] 8.2 Wstawić sekcję do `GenerateSystemPrompt` w konwencji `sb.AppendLine("...")`, **tekst dosłowny** ze specyfikacji (10 punktów)
  - [ ] 8.3 Dopisać `PromptSuggestionItem` „Kolorowanie na listach" do `AIChatDefaults.PromptSuggestions`, obok „Przygotuj raport" (W-15)
  - [ ] 8.4 Uruchomić `--filter "FullyQualifiedName~SystemPromptTests"`

**Kryteria odbioru:**
- 2 nowe testy przechodzą
- Tekst sekcji jest identyczny z blokiem ze specyfikacji — bez parafrazy, bo to kontrakt zachowania modelu
- Podpowiedź widoczna w czacie czyni funkcję odkrywalną bez czytania dokumentacji

---

### Grupa 9: Cykl życia — graduacja, usunięcie encji, strażnik pól

**Zależności:** Grupa 4, Grupa 7 (współdzielony `SchemaAIToolsProvider.cs`)
**Pliki do zmiany:**
`XafXPODynAssem/XafXPODynAssem.Module/Services/GraduationService.cs`,
`XafXPODynAssem/XafXPODynAssem.Module/Controllers/GraduationWarningController.cs`,
`XafXPODynAssem/XafXPODynAssem.Module/Services/SchemaAIToolsProvider.cs` (**dzielony z grupą 7**),
`XafXPODynAssem/XafXPODynAssem.Module/Validation/FieldTypeChangeGuard.cs`,
`XafXPODynAssem/XafXPODynAssem.Module.Tests/GraduationTests.cs` (**dzielony z grupą 11**),
`XafXPODynAssem/XafXPODynAssem.Module.Tests/SchemaGuardTests.cs` (**dzielony z grupą 11**)
**Szacowana liczba kroków:** 6

Graduacja jest tu jednym z wyzwalaczy przeliczenia, nie przypadkiem szczególnym. Fakt bazowy: `GenerateEntityClass` nie emituje deklaracji `namespace`, więc `FullName` przechodzi z `XafXPODynAssem.RuntimeEntities.X` na `XafXPODynAssem.Module.BusinessObjects.X` i **każda reguła cicho przestaje kolorować**.

- [ ] 9.0 Sprawić, żeby reguła przeżyła zmiany schematu, na które wskazuje
  - [ ] 9.1 Napisać 4 testy — 2 dopisane do `GraduationTests.cs`, 2 do `SchemaGuardTests.cs`
    - Czysta funkcja migracji nazwy: `XafXPODynAssem.RuntimeEntities.Faktura` → `XafXPODynAssem.Module.BusinessObjects.Faktura`; nazwa spoza przestrzeni runtime zostaje nietknięta
    - Wygenerowane źródło zawiera sekcję `// --- Appearance Note ---` z liczbą reguł, gdy encja ma ≥ 1 regułę, i **nie zawiera** jej przy zerze (A-24)
    - `FindRulesTargetingField(className, fieldName)` znajduje regułę wskazującą pole przez `TargetItems` oraz przez węzeł `OperandProperty` w kryterium
    - Ukrycie pola daje **ostrzeżenie, nie blokadę** — spójnie z istniejącym zachowaniem strażników (A-25)
  - [ ] 9.2 Dodać migrację `TargetTypeName` przy przejściu `CustomClass.Status` na `Graduating`/`Compiled`, z przeliczeniem `Health`/`Diagnosis` przez walidator (W-11)
  - [ ] 9.3 Dopisać do istniejącego komunikatu w `GraduationWarningController` zdanie o regułach: ile zmigrowano i że zaczną działać po wklejeniu źródła i przebudowie obrazu (A-23)
  - [ ] 9.4 Dodać sekcję `// --- Appearance Note ---` do `GraduationService.GenerateSource`, obok istniejących `// --- XPO Note ---` i `// --- Web API Note ---`, treść dosłownie ze specyfikacji (W-12)
    - Reguł **nie** generujemy jako atrybutów `[Appearance]` — atrybut zamroziłby odcień i wymagał przebudowy obrazu na każdą zmianę koloru, czyli dokładnie tego, czego ta funkcja ma nie wymagać
  - [ ] 9.5 Rozszerzyć `delete_entity` o przeliczenie reguł wskazujących skasowany typ i dopisanie do odpowiedzi, ile reguł osierocono. Reguł **nie kasujemy** (W-14). Dodać `FindRulesTargetingField` do `FieldTypeChangeGuard` obok `IsFieldRemovalSafe` (linia 179) i wystawić ostrzeżenie przy `IsVisibleInListView = false` na polu wskazywanym przez aktywną regułę (W-13)
  - [ ] 9.6 Uruchomić `--filter "FullyQualifiedName~GraduationTests|FullyQualifiedName~SchemaGuardTests"`

**Kryteria odbioru:**
- 4 nowe testy przechodzą; dotychczasowe testy w obu plikach dalej zielone
- Migracja nazwy jest czystą funkcją, testowalną bez uruchamiania aplikacji
- Ostrzeżenie przy ukrywaniu pola jest **ostrzeżeniem**, a nie blokadą — grep potwierdza, że nie dodano żadnej ścieżki `Active[...] = false`

---

### Grupa 10: Uprawnienia i higiena schematu

**Zależności:** Grupa 3, Grupa 6
**Pliki do zmiany:**
`XafXPODynAssem/XafXPODynAssem.Module/DatabaseUpdate/Updater.cs` (**warunkowo** — tylko jeśli padnie decyzja o nadaniu dostępu roli `Default`),
`CLAUDE.md`
**Szacowana liczba kroków:** 5

Wniosek ze specyfikacji: **żadna zmiana w `Updater.cs` nie jest wymagana i projekt celowo od niej nie zależy.** `UpdateDatabaseAfterUpdateSchema` we wdrożonej paczce ma 7 bajtów IL — cały blok `#if !RELEASE` jest wycięty przez kompilator, więc jakikolwiek nowy wpis tam po prostu się nie wykona. Ta grupa domyka temat świadomie, zamiast zostawiać go otwartym.

- [ ] 10.0 Domknąć uprawnienia i sprostować dokumentację
  - [ ] 10.1 Odnotować decyzję o uprawnieniach w komentarzu przy `AdditionalExportedTypes.Add(typeof(AppearanceRule))`: kolorowanie działa dla każdego zalogowanego, bo idzie przez `INonSecuredObjectSpaceFactory`; ekran jest dostępny dla `Administrators` (`IsAdministrative = true`) bez ani jednego wpisu, dokładnie tak jak dziś `CustomClass` i `WorkflowDefinition`
  - [ ] 10.2 **Jeśli** użytkownik zdecyduje, że rola `Default` ma dostać ekran reguł (nie jest to wymagane): dodać nową metodę wołaną **bezwarunkowo** z `UpdateDatabaseAfterUpdateSchema`, **poza** `#if !RELEASE`, z nadaniami **poza** blokiem `if (role == null)` — inaczej na istniejącej bazie uprawnienia nigdy nie wejdą
  - [ ] 10.3 Zweryfikować lokalnie, że tabela `AppearanceRule` powstaje: `dotnet run --project XafXPODynAssem/XafXPODynAssem.Blazor.Server -- --updateDatabase`, potem sprawdzić kolumny w bazie
  - [ ] 10.4 Nanieść korekty do `CLAUDE.md` wskazane przez fazy 1–5: baza to **PostgreSQL** (`XpoProvider=Postgres`), nie SQL Server localdb; target **net8.0**, DevExpress **26.1.4**, nie 25.2; zmienna `XAF_UPDATE_DB` **istnieje** (`Blazor.Server/BlazorApplication.cs:37`, `deploy/bezprzerwy/trzy-repliki.sh:62`) — notatka fazy 1 była błędna, `deploy/bezprzerwy/README.md` ma rację. Dopisać jedno zdanie o regułach wyglądu do sekcji „File Locations"
  - [ ] 10.5 Zweryfikować pełnym `git diff`, że żaden `.xafml` się nie zmienił i że `QueryMetadata` / `GetMetadataFingerprint` / `ComputeFingerprint` są nietknięte

**Kryteria odbioru:**
- Tabela `AppearanceRule` powstaje krokiem `--updateDatabase` (lokalny odpowiednik A-27)
- Decyzja o uprawnieniach jest zapisana w kodzie, nie w głowie autora
- `CLAUDE.md` nie kłamie już o bazie, wersji DevExpressa ani o `XAF_UPDATE_DB`
- Zbiorcze potwierdzenie A-14 i A-26

---

### Grupa 11: Przegląd testów i uzupełnienie luk

**Zależności:** grupy 2, 3, 4, 5, 6, 7, 8, 9, 10
**Pliki do zmiany:**
`XafXPODynAssem/XafXPODynAssem.Module.Tests/CriteriaTests.cs` (**dzielony z grupą 4**),
`XafXPODynAssem/XafXPODynAssem.Module.Tests/GraduationTests.cs` (**dzielony z grupą 9**),
`XafXPODynAssem/XafXPODynAssem.Module.Tests/SchemaGuardTests.cs` (**dzielony z grupą 9**),
`XafXPODynAssem/XafXPODynAssem.Module.Tests/*.cs` (pliki testowe grup 2–9)
**Szacowana liczba kroków:** 5

- [ ] 11.0 Przejrzeć testy z grup 2–9 i dopisać najwyżej 10 strategicznych
  - [ ] 11.1 Przejrzeć 36 testów napisanych w grupach 2–9 — czy któryś sprawdza implementację zamiast zachowania
  - [ ] 11.2 Zestawić testy z 29 kryteriami akceptacji i wskazać, które kryteria nie mają żadnego odpowiednika automatycznego (część jest z natury ręczna — nie dopisywać testów na siłę)
  - [ ] 11.3 Dopisać **maksymalnie 10** testów w miejscach realnie odsłoniętych; kandydaci pierwszego wyboru: ścieżka `OnSaving()` → `Health`/`Diagnosis`/`CheckedOn` w jednym przebiegu, sklejanie wielu ostrzeżeń, reguła z pustym `Criteria` i pustym `TargetItems` przechodząca przez pełny łańcuch normalizacji
  - [ ] 11.4 Sprawdzić, że po przeniesieniu obchodzenia drzewa do walidatora w `CriteriaTests.cs` nie została żadna martwa kopia
  - [ ] 11.5 Uruchomić **cały** zestaw: `dotnet test XafXPODynAssem/XafXPODynAssem.Module.Tests`

**Kryteria odbioru:**
- Cały zestaw zielony: 61 istniejących + 36 nowych + do 10 dopisanych = **97–107 testów**
- Nie dopisano więcej niż 10 testów
- Żaden istniejący test nie został zmieniony w sposób, który osłabia jego wymowę

---

### Grupa 12: Weryfikacja ręczna na uruchomionej aplikacji

**Zależności:** Grupa 11
**Pliki do zmiany:** brak kodu produkcyjnego; produktem jest `verification/weryfikacja-reczna.md`
**Visual References:**
- mockup: `analysis/design-context/mockups/lista-regu-wygl-du.html`
  element: `screen:lista-regul`
  locator: cały ekran
  acceptance: zrzut z uruchomionej aplikacji odpowiada makiecie w kolumnach, pasku akcji i wyróżnieniu wierszy (A-15)
- mockup: `analysis/design-context/mockups/regu-a-zak-adka-definicja.html`
  element: `screen:regula-definicja`
  locator: zakładka „Definicja" wraz z panelem diagnozy
  acceptance: reguła z literówką w nazwie pola pokazuje diagnozę z podpowiedzią „Czy chodziło o …?" (A-16)
- mockup: `analysis/design-context/mockups/regu-a-zak-adka-wygl-d.html`
  element: `screen:regula-wyglad`
  locator: zakładka „Wygląd", kolejność trzech grup
  acceptance: rola z próbką na górze, nadpisanie kolorem na dole i opisane jako awaryjne (A-17)
- mockup: `analysis/design-context/mockups/czat-utworzenie-regu-y-z-dyktowania.html`
  element: `screen:czat-utworzenie`
  locator: cała rozmowa
  acceptance: zdanie „zrób faktury niezapłacone na czerwono na listach" prowadzi do reguły nad istniejącym polem, z dopytaniem o brakującą część (A-04, A-08)
- mockup: `analysis/design-context/mockups/czat-walidacja-odrzuca-bubla.html`
  element: `screen:czat-blad`
  locator: blok odmowy
  acceptance: „pokoloruj faktury gdzie saldo jest dodatnie" → odmowa z listą pól liczbowych i **zero rekordów** w tabeli (A-05, A-06)
- mockup: `analysis/design-context/mockups/efekt-lista-faktur.html`
  element: `screen:efekt-faktury`
  locator: lista faktur
  acceptance: wiersze niezapłaconych pokolorowane, bez żadnego dodatkowego elementu interfejsu
**Szacowana liczba kroków:** 6

W repo nie ma testów E2E, więc ta grupa jest jedyną weryfikacją zachowania na żywym systemie. Uruchomienie: `dotnet run --project XafXPODynAssem/XafXPODynAssem.Blazor.Server`, `https://localhost:5001`, logowanie `Admin` bez hasła.

- [ ] 12.0 Przejść ścieżki, których test jednostkowy nie obejmie
  - [ ] 12.1 Ścieżka AI od zdania do koloru — A-04, A-05, A-06, A-07, A-08, wraz z porównaniem przebiegu rozmowy z dwiema makietami czatu
  - [ ] 12.2 Ekran reguł — A-15, A-16, A-17, A-18, A-19, A-20; wyłączenie reguły suwakiem zdejmuje kolorowanie po F5, bez restartu
  - [ ] 12.3 Odporność — A-09 (reguła na skasowanej encji nie wywraca żadnego widoku), A-10 (`INSERT` wprost do bazy z `Criteria = "[Kwotaa] > 10"` → lista otwiera się bez kolorowania, wpis w logu), A-13
  - [ ] 12.4 **Lokalny odpowiednik A-02** — `INSERT` reguły wprost do bazy z pominięciem aplikacji, potem świeża sesja przeglądarki. Kolorowanie musi się pojawić bez restartu procesu. To dowodzi braku bufora w pamięci procesu **bez** potrzeby trzech replik
  - [ ] 12.5 **A-22 — kierunek rozstrzygania priorytetu.** Dwie reguły na tym samym polu, `Priority` 10 (zielona) i 20 (czerwona), ten sam warunek. Oczekiwanie: wiersz czerwony, powtarzalnie po odświeżeniu
    - **Wariant awaryjny, jeśli DevExpress rozstrzyga odwrotnie**: odwrócić `OrderBy` w `LoadRules()` (grupa 5, krok 5.3) i poprawić jedno zdanie w sekcji promptu systemowego (grupa 8, krok 8.2). Nic więcej się nie zmienia
  - [ ] 12.6 **A-21 — parytet WinForms.** Uruchomić `XafXPODynAssem.Win` (projekt jest w `XafXPODynAssem.slnx`), otworzyć tę samą listę, potwierdzić identyczne kolorowanie. Kryterium **nie** jest odłożone razem z wdrożeniem — działa lokalnie

**Kryteria odbioru:**
- `verification/weryfikacja-reczna.md` zawiera wynik dla każdego z kryteriów A-04…A-22 osiągalnych lokalnie, każdy z dowodem (zrzut ekranu, fragment logu albo wynik zapytania SQL)
- A-22 ma zapisany **rozstrzygnięty** kierunek, nie hipotezę; jeśli wypadł odwrotnie niż kontrakt — poprawka jest naniesiona i odnotowana
- Odchylenia od makiet są wypisane jako lista, nie schowane w zdaniu „zgodne"
- A-01, A-02 (wersja trzyreplikowa), A-03 i A-27 są **jawnie odnotowane jako odłożone** wraz z powodem

---

## Kolejność wykonania

```
 1. Rozpoznanie empiryczne              (6 kroków)   ────┐
 2. Enumy i paleta ról                  (6 kroków)   ←───┘
 3. Encja + rejestracja                 (7 kroków)   ← 2
 4. Walidator                           (7 kroków)   ← 2, 3
       ├──────────────┐
 5. Kontrolery        │                 (7 kroków)   ← 3, 4   ⟂ równolegle z 6
 6. Ekran reguł  ←────┘                 (7 kroków)   ← 3, 4   ⟂ równolegle z 5
 7. Narzędzia AI                        (6 kroków)   ← 4, 6
       ├──────────────┐
 8. Prompt systemowy  │                 (4 kroki)    ← 7      ⟂ równolegle z 9
 9. Cykl życia   ←────┘                 (6 kroków)   ← 4, 7   ⟂ równolegle z 8
10. Uprawnienia i higiena               (5 kroków)   ← 3, 6
11. Przegląd testów i luki              (5 kroków)   ← 2…10
12. Weryfikacja ręczna                  (6 kroków)   ← 11
```

**Ścieżka krytyczna**: 1 → 2 → 3 → 4 → 6 → 7 → 9 → 11 → 12 (dziewięć grup). Równolegle da się puścić wyłącznie pary **5 ‖ 6** oraz **8 ‖ 9** — pod warunkiem, że wykonawca uszanuje pliki dzielone (`AppearanceRule.cs` między 3 a 6, `SchemaAIToolsProvider.cs` między 7 a 9).

**Poza planem — świadomie odłożone**: wdrożenie na Proxmox LXC 200 wariantem (b) (`docker compose down` → nowy obraz → `--updateDatabase` → `trzy-repliki.sh` → test na 8101/8102/8103) oraz kryteria A-01, A-02 w wersji trzyreplikowej, A-03 i A-27. Użytkownik wstrzymał aktualizację środowisk. Transfer paczki 223 MB i tak jest krokiem zablokowanym dla agenta (`deploy/README-wdrozenie.md` §1).

---

## Zgodność ze standardami

`.maister/docs/standards/` ani `.maister/docs/INDEX.md` w tym repozytorium **nie istnieją** — nie ma plików standardów do zastosowania. Obowiązują konwencje odczytane z kodu i zapisane w specyfikacji:

- Identyfikatory C# angielskie, napisy dla użytkownika polskie przez `[XafDisplayName]`; brak warstwy `.resx`
- Teksty `[Description]` narzędzi AI **angielskie** (kontrakt dla modelu), teksty zwracane na ścieżkach odmowy **polskie**
- ORM: XPO. Konstruktor `(Session session)`, `SetPropertyValue(nameof(X), ref x, value)`
- Kontrolery w `Module/Controllers/*.cs`, wykrywane automatycznie — bez ręcznej rejestracji w żadnym `Startup.cs`
- Narzędzia AI: prywatne metody z `[Description]`, parametry wyłącznie prymitywne, błędy jako tekst, nigdy wyjątek przez granicę
- Rejestracja typów trwałych przez `AdditionalExportedTypes.Add` w konstruktorze modułu
- `FontStyle` to `DevExpress.Drawing.DXFontStyle`, nie `System.Drawing.FontStyle`
- Brak koloru zwracamy jako `null`, nigdy `Color.Empty`
- Zasada repo: **pól nie usuwamy, dodajemy obok i ukrywamy stare** (`FieldTypeChangeGuard`)

---

## Uwagi

- **Testy razem z kodem.** Każda grupa kodowa zaczyna od 2–8 testów i kończy uruchomieniem **wyłącznie** swoich testów, nie całego zestawu. Cały zestaw idzie raz, w grupie 11.
- **Istniejący projekt testowy.** `XafXPODynAssem.Module.Tests` już jest, ma 61 zielonych testów i jest w `XafXPODynAssem.slnx`. Nie zakładać go od nowa. Sekcja „Podejście testowe" specyfikacji („zero testów w repo") jest w tym punkcie nieaktualna.
- **Nie sięgać po prototyp z gałęzi eksperymentalnej.** Został zarchiwizowany i skasowany; był sprzeczny ze specyfikacją (surowy kolor zamiast roli, brak walidatora, brak stanu reguły). Wzorce bierzemy z `Workflow.cs`, `CustomClass.cs` i `SchemaAIToolsProvider.cs` w tym repo.
- **Nie optymalizować przedwcześnie.** Pomiar rozstrzyga: przy tej skali reguł narzut mechanizmu jest niemierzalny. Jedyny twardy wymóg to pre-ładowanie w `OnActivated`.
- **Zaznaczać postęp.** Odhaczać kroki w tym pliku w miarę wykonania; ten plik jest źródłem prawdy przy wznowieniu pracy.
