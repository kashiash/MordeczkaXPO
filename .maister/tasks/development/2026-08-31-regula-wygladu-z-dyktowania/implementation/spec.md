# Specyfikacja: Reguły wyglądu z dyktowania (mordeczka)

**Data**: 2026-08-31
**Repo**: `/Users/jacek/Projects/Brekhof/XafXPODynAssem` (kod w `XafXPODynAssem/XafXPODynAssem.Module/...` — segment nazwy jest zdublowany)
**Wejście**: `analysis/codebase-analysis.md`, `analysis/gap-analysis.md`, `analysis/design-context/`, `orchestrator-state.yml`

---

## TL;DR

Do mordeczki dochodzi encja `AppearanceRule` (reguła wyglądu jako dane) i `ViewController`, który przy każdej aktywacji widoku czyta reguły z bazy i wstrzykuje je do `AppearanceController.CollectAppearanceRules` — bez bufora w pamięci procesu, więc trzy repliki propagują zmiany za darmo.
Regułę tworzy asystent AI dwoma narzędziami (`sprawdz_regule_wygladu` / `utworz_regule_wygladu`), a poprawia człowiek na własnym ekranie w grupie „Zarządzanie schematem".
Sednem jest jeden walidator używany w trzech miejscach: odmawia zapisu bubla, pomija niesprawną regułę zamiast wywalić widok i pokazuje na ekranie reguł, **że** jest zepsuta i **dlaczego**.
Kolor zapisujemy jako rolę semantyczną (Błąd / Ostrzeżenie / …), nie jako ARGB — odcień rozwiązuje się przy renderowaniu.
Wdrożenie: zgaszenie trójki replik, nowy obraz, `--updateDatabase` dla nowej tabeli, `trzy-repliki.sh`, test osobno na 8101/8102/8103.

## Kluczowe decyzje

- **Wariant A, bez odwołania** — `ViewController` + `AppearanceController.CollectAppearanceRules`, reguły pre-ładowane w `OnActivated`, sekwencja `ResetRulesCache()` → subskrypcja → `Refresh()`. Fork A/B jest rozstrzygnięty (`orchestrator-state.yml: architecture_decision`); obie analizy pisały „prototypować oba" przed tą decyzją.
- **Zero bufora w pamięci procesu.** Trzy repliki to trzy procesy — każdy globalny cache (statyczny event Brekhofa, `CustomApperanceStorage` HIS-a, `ConcurrentDictionary` DataDrive'a) propaguje zmianę tylko w obrębie repliki, na której zapisano. Odczyt z bazy przy aktywacji widoku jest tu tańszy niż jakikolwiek mechanizm sygnalizacji.
- **Reguły czytamy przez `INonSecuredObjectSpaceFactory`, nie przez `View.ObjectSpace`.** Zabezpieczona przestrzeń obiektów zwróciłaby użytkownikowi bez uprawnienia do `AppearanceRule` pustą listę i kolorowanie umarłoby po cichu tylko dla nie-adminów. Odczyt konfiguracji UI odłączamy od uprawnień do ekranu reguł. Skutek uboczny: kolorowanie nie zależy od żadnego wpisu w `Updater.cs`.
- **Encja implementuje `IAppearanceRuleProperties` bezpośrednio** (wzorzec Fleetmana w XPO), bez klasy-adaptera i bez klasy-migawki. Adapter Brekhofa to idiom EF Core, a migawka DataDrive'a rozwiązuje problem cache'u, którego tu nie mamy.
- **Kolor przez rolę semantyczną**, ARGB tylko jako furtka administratora, zawsze para tło+czcionka. Narzędzie AI ustawia wyłącznie rolę.
- **Stan i Diagnoza są utrwalone na encji** i przeliczane w `OnSaving()` — dzięki temu lista pokazuje zdrowie reguł bez przeliczania czegokolwiek, a walidator ma jedno wywołanie na każdej ścieżce zapisu.
- **Graduacja migruje `TargetTypeName` i ostrzega**, ale nie generuje atrybutów `[Appearance]` — tylko zakomentowaną sekcję `// --- Appearance Note ---` w wygenerowanym źródle.

## Otwarte pytania i ryzyka

- **[SPRAWDZIĆ PRZED PALETĄ]** Czy z kontrolera da się odczytać aktywny motyw DevExpress Blazor i czy motyw jest per użytkownik, czy globalny. Od tego zależy, czy paleta ról ma jeden zestaw odcieni, czy dwa.
- **[SPRAWDZIĆ PRZED PALETĄ]** Czy `IAppearance.BackColor` przenosi kanał alfa do CSS. Jeśli tak, półprzezroczyste nakładki (np. `#20DC3545`) rozwiązują problem skórki bez wiedzy o motywie i paleta upraszcza się do sześciu wartości.
- **[SPRAWDZIĆ NA PROTOTYPIE]** Kierunek rozstrzygania konfliktu w silniku DevExpress. Kontrakt tej specyfikacji: **wyższy `Priority` wygrywa**. Nasz kontroler sortuje rosnąco i podaje reguły w tej kolejności; `AppearanceController.CombineAppearanceResults` jest wewnętrzny i nie da się go potwierdzić z metadanych. Notatka porównawcza mówi to samo o DataDrive („`OrderBy` jest rosnące, a DevExpress rozstrzyga konflikt na korzyść wyższego priorytetu"), ale to relacja, nie dowód. Sprawdzić dwiema regułami o `Priority` 10 i 20 na tym samym polu.
- **`Updater.cs` na wdrożonej instancji jest martwy — zweryfikowane, nie hipoteza.** Paczka `/tmp/xpo-publish.tgz` (233 MB, 30.08) ma `AssemblyConfigurationAttribute = "Release"`, a `Updater.UpdateDatabaseAfterUpdateSchema` w `XafXPODynAssem.Module.dll` ma **7 bajtów IL** — czyli samo `base.…()` + `ret`. Cały blok `#if !RELEASE` jest wycięty przez kompilator; `CreateDefaultRole()` i `CreateAdminRole()` istnieją w assembly, ale nikt ich nie woła. Konsekwencja: jakikolwiek nowy wpis w `Updater.cs` **nie wykona się na wdrożonej instancji**. Dlatego projekt celowo od niego nie zależy (patrz „Uprawnienia").
- **Zapis narzędzia AI omija bezpieczeństwo XAF.** `INonSecuredObjectSpaceFactory` ignoruje uprawnienia do typów i składowych, więc dowolny rozmówca czatu może utworzyć regułę na dowolnej encji — także takiej, której nie widzi w interfejsie. To jest **świadome obejście zabezpieczeń**, przyjęte dla spójności z 19 istniejącymi narzędziami, a nie przeoczenie. Odwracalność zapewnia ekran reguł, nie bramka.
- **Bramka potwierdzenia jest wyłącznie promptowa.** Nic technicznie nie broni modelowi wywołać `utworz_regule_wygladu` bez wcześniejszego `sprawdz_regule_wygladu`; dlatego narzędzie zapisujące waliduje samo, wewnętrznie, przed każdym zapisem.
- **Zero testów w repo.** Nie ma projektu testowego i to zadanie go nie zakłada — weryfikacja jest w całości ręczna, wg listy w „Kryteria akceptacji".
- **Dwie polskie nazwy wśród 19 angielskich.** `sprawdz_regule_wygladu` / `utworz_regule_wygladu` siedzą w angielskim prompcie systemowym. Nazwy są ustalone (zadanie + makiety), notuję tylko jako drobną niespójność konwencji.
- **Sprzeczność w `deploy/bezprzerwy/README.md` jest pozorna.** Faza 1 raportowała, że zmienna `XAF_UPDATE_DB` nie istnieje. Istnieje: `XafXPODynAssem.Blazor.Server/BlazorApplication.cs:37` czyta ją w obsłudze `DatabaseVersionMismatch`, a `trzy-repliki.sh:62` ustawia ją na `1` dla każdej repliki. README ma rację; korekta do zapisania.

---

## Cel

Użytkownik dyktuje po polsku zdanie w rodzaju „zrób faktury niezapłacone na czerwono na listach", a aplikacja koloruje wiersze — bez rekompilacji, bez wdrożenia i bez restartu żadnej z trzech replik. Błędnie utworzoną regułę widać jako zepsutą i poprawia się ją na ekranie, nie SQL-em w bazie produkcyjnej.

## Historyjki użytkownika

1. Jako administrator schematu chcę podyktować w czacie warunek kolorowania, żeby nie prosić programisty o atrybut `[Appearance]` i przebudowę obrazu.
2. Jako administrator chcę, żeby asystent odmówił zapisu, gdy nazwę pole, którego nie ma — i żeby powiedział, jakie pola są dostępne, zamiast pytać mnie o coś, co ma w schemacie.
3. Jako administrator chcę listę wszystkich reguł ze stanem, żeby jednym rzutem oka zobaczyć, która nie działa i dlaczego.
4. Jako administrator chcę wyłączyć regułę suwakiem zamiast ją kasować, żeby dało się wrócić.
5. Jako użytkownik aplikacji chcę widzieć to samo kolorowanie niezależnie od tego, na którą replikę trafię.
6. Jako administrator chcę, żeby po graduacji encji reguły dalej działały, a jeśli czegoś nie da się naprawić automatycznie — żeby było to napisane wprost.

---

## Zakres

### Wchodzi

| # | Wymaganie | Priorytet |
|---|---|---|
| W-01 | Encja trwała `AppearanceRule` implementująca `IAppearanceRuleProperties`, zarejestrowana w `Module.cs` | krytyczny |
| W-02 | `AppearanceRuleViewController` wpinający reguły w `AppearanceController.CollectAppearanceRules` | krytyczny |
| W-03 | Walidator `AppearanceRuleValidator` — jeden, użyty w trzech miejscach | krytyczny |
| W-04 | Pola utrwalone `Health` / `Diagnosis` (etykiety „Stan" / „Diagnoza") przeliczane w `OnSaving()` | krytyczny |
| W-05 | Narzędzie AI `sprawdz_regule_wygladu` (tylko odczyt) | krytyczny |
| W-06 | Narzędzie AI `utworz_regule_wygladu` (waliduje i zapisuje) | krytyczny |
| W-07 | Sekcja `## Appearance Rules` w `SchemaDiscoveryService.GenerateSystemPrompt` | krytyczny |
| W-08 | Własny ekran reguł: lista + szczegóły w grupie „Zarządzanie schematem", przez `[DefaultClassOptions]` + `[NavigationItem]`, bez tykania `.xafml` | krytyczny |
| W-09 | Rola koloru (6 wartości) + paleta rozwiązująca rolę na `Color?` przy renderowaniu | wysoki |
| W-10 | Akcje „Sprawdź reguły" (lista) i „Sprawdź tę regułę" (szczegóły) | wysoki |
| W-11 | Migracja `TargetTypeName` przy graduacji + ostrzeżenie | wysoki |
| W-12 | Sekcja `// --- Appearance Note ---` w źródle generowanym przez `GraduationService` | średni |
| W-13 | Ostrzeżenie w `FieldTypeChangeGuard` przy ukrywaniu pola wskazywanego przez aktywną regułę | średni |
| W-14 | Przeliczenie reguł po `delete_entity` | średni |
| W-15 | Podpowiedź „Kolorowanie na listach" w `AIChatDefaults.PromptSuggestions` | niski |
| W-16 | Statyczny `[Appearance]` wyróżniający niesprawne reguły na ich własnej liście | niski |
| W-17 | Nadpisanie kolorem (ARGB) jako furtka administratora, poza kontraktem AI | niski |
| W-18 | Zawężenie reguły do jednego widoku (`ViewId`), poza kontraktem AI | niski |
| W-19 | Akcja „Klonuj" na liście reguł | niski |
| W-20 | Parytet Blazor + WinForms (bez dodatkowego kodu — kontroler jest wykrywany automatycznie w obu hostach) | wysoki |
| W-21 | Wdrożenie wariantem (b) i test kolorowania osobno na 8101/8102/8103 | krytyczny |

### Jawnie nie wchodzi

- **Kod dyktowania i rozpoznawania mowy.** „Podyktowane zdanie" to zwykły tekst wpisany do istniejącego czatu; dyktowanie robi system operacyjny. Ikona mikrofonu na makietach czatu jest ozdobą kontekstu, nie wymaganiem.
- **Dokładanie reguł do odcisku metadanych.** `Module.QueryMetadata` / `GetMetadataFingerprint` / `ComputeFingerprint` zostają przy `CustomClass` i `CustomField`. Każda inna decyzja restartuje trzy repliki przy każdym podyktowanym zdaniu.
- **Sterowanie `Enabled` (blokada edycji) z bazy.** Żadna z czterech aplikacji referencyjnych tego nie robi; `IAppearance.Enabled` zostaje zaślepką `null`. Osobne zadanie.
- **`AppearanceItemType` inny niż `"ViewItem"`.** Celowanie w przyciski akcji i elementy układu — zaślepka, osobne zadanie.
- **Obramowanie komórki (`BorderColor`).** Wymaga własnego evaluatora i `CustomizeElement` (wzorzec HIS-a); poza zakresem tego zadania.
- **Reguły generowane programowo (`AppearanceModel` HIS-a).** Trzeci archetyp, niepotrzebny do tego celu.
- **Utworzenie projektu testowego.** Weryfikacja jest ręczna.
- **Przepisanie `wdroz.sh` na ogólny mechanizm N replik.** Wdrożenie idzie wariantem (b), który używa sprawdzonego `trzy-repliki.sh`.
- **Wymiana blue/green na 8111–8113 bez przerwy.** Odrzucona na bramce fazy 2.
- **Obudowanie `INonSecuredObjectSpaceFactory`.** Dotyczyłoby wszystkich 19 narzędzi; osobne zadanie.
- **Transfer paczki 223 MB na LXC 200.** Wykonuje użytkownik (`deploy/README-wdrozenie.md` §1 — krok jawnie zablokowany dla agenta).

---

## Model danych

### Encja `AppearanceRule`

Plik: `/Users/jacek/Projects/Brekhof/XafXPODynAssem/XafXPODynAssem/XafXPODynAssem.Module/BusinessObjects/AppearanceRule.cs`
Klasa bazowa: `DevExpress.Persistent.BaseImpl.BaseObject` (XPO), konstruktor `(Session session)`, dostęp przez `SetPropertyValue(nameof(X), ref x, value)` — jak `WorkflowDefinition`.

Atrybuty klasy:

```
[DefaultClassOptions]
[NavigationItem("Zarządzanie schematem")]
[DefaultProperty(nameof(Name))]
[XafDisplayName("Reguła wyglądu")]
[Appearance("ReguleNiesprawna", TargetItems = "*", Criteria = "Health = 2", Context = "ListView", ...)]
[Appearance("ReguleOstrzezenie", TargetItems = "*", Criteria = "Health = 1", Context = "ListView", ...)]
[Appearance("ReguleWylaczona",   TargetItems = "*", Criteria = "IsEnabled = False", Context = "ListView", FontColor = "Gray", FontStyle = Italic)]
```

Trzy statyczne `[Appearance]` wzorowane na `CustomClass.cs:23-32` — to jedyne miejsce, gdzie reguła wyglądu wolno zapisać atrybutem, bo dotyczy klasy znanej w czasie kompilacji.

#### Pola utrwalone

| Właściwość C# | Typ XPO | `[XafDisplayName]` | Uwagi |
|---|---|---|---|
| `Name` | `string`, `[Size(255)]` | „Nazwa" | `[DefaultProperty]`, `[RuleRequiredField]` |
| `TargetTypeName` | `string`, `[Size(255)]` | „Encja docelowa" | **`FullName`, nigdy `AssemblyQualifiedName`** — assembly runtime jest generowane w pamięci i jego tożsamość zmienia się przy każdej rekompilacji |
| `Criteria` | `string`, `[Size(SizeAttribute.Unlimited)]` | „Kryterium" | `[CriteriaOptions(nameof(TargetType))]` + `[EditorAlias(EditorAliases.CriteriaPropertyEditor)]` — edytor filtra podpowiada wyłącznie pola wybranej encji |
| `TargetItems` | `string`, `[Size(SizeAttribute.Unlimited)]` | „Pola docelowe" | `;`-rozdzielona lista albo `*`; `[EditorAlias(EditorAliases.CheckedListBoxEditor)]` |
| `AppearanceContext` | `AppearanceContext` (enum DevExpressa) | „Kontekst" | **Nie deklarujemy własnego enuma.** `DevExpress.ExpressApp.ConditionalAppearance.AppearanceContext` = `ListView(0)` / `DetailView(1)` / `Any(2)` — zweryfikowane w `DevExpress.Persistent.Base.v26.1`. `ListView` jako `0` jest zarazem pożądaną wartością domyślną. Wzorzec DataDrive'a |
| `ViewId` | `string`, `[Size(255)]` | „Ogranicz do widoku" | pusty = dowolny widok tej encji; filtrowany przez nasz kontroler, nie przez DevExpress |
| `ColorRole` | `enum AppearanceColorRole` | „Rola koloru" | `None`/`Error`/`Warning`/`Success`/`Info`/`Muted`/`Highlight` |
| `BackColorOverride` | `string`, `[Size(16)]` | „Kolor tła (nadpisanie)" | tekst ARGB/hex, nie `int` — kolumna czytelna z SQL-a i zapisywalna bez typów `System.Drawing`; wzorzec HIS/DataDrive |
| `FontColorOverride` | `string`, `[Size(16)]` | „Kolor czcionki (nadpisanie)" | jw. |
| `RuleFontStyle` | `DXFontStyle?` | „Styl czcionki" | `DevExpress.Drawing.DXFontStyle`, **nie** `System.Drawing.FontStyle` |
| `ItemVisibility` | `ViewItemVisibility?` | „Widoczność" | `DevExpress.ExpressApp.Editors.ViewItemVisibility`: `Hide` / `ShowEmptySpace` / `Show` |
| `Priority` | `int` | „Priorytet" | domyślnie `10`; **realnie sortowane** — kopiujesz pole, kopiujesz `OrderBy` |
| `IsEnabled` | `bool` | „Włączona" | domyślnie `true`; wyłączenie zamiast kasowania |
| `Health` | `enum AppearanceRuleHealth` | **„Stan"** | `Healthy(0)` / `Warning(1)` / `Broken(2)` → „Sprawna" / „Ostrzeżenie" / „Niesprawna" |
| `Diagnosis` | `string`, `[Size(SizeAttribute.Unlimited)]` | **„Diagnoza"** | polski tekst z walidatora; pusty, gdy `Healthy` |
| `CheckedOn` | `DateTime?` | „Sprawdzono" | znacznik ostatniego przeliczenia, pokazywany w panelu diagnozy |

> **Nazewnictwo.** Identyfikatory C# są angielskie, etykiety polskie — konwencja repo (`Workflow.cs`, `CustomClass.cs`). Decyzja mówiąca o polach „Stan" i „Diagnoza" jest tu zrealizowana jako `Health` / `Diagnosis` z tymi właśnie etykietami. Nazwa `RuleFontStyle` zamiast `FontStyle` — bo `FontStyle` jest już zajęte przez jawną implementację `IAppearance`.

#### Właściwości nietrwałe

| Właściwość | Rola | Uwaga |
|---|---|---|
| `[NonPersistent] public Type TargetType { get; set; }` | **picker typu w interfejsie** oraz źródło dla `[CriteriaOptions]` i dla `DeclaringType` | `[TypeConverter(typeof(LocalizedClassInfoTypeConverter))]` + `[ImmediatePostData]`. Getter zwraca `ResolveTargetType()`. Setter zapisuje `TargetTypeName = value?.FullName ?? ""` i — jeśli typ się zmienił — **czyści `Criteria` i `TargetItems`** (wzorzec z Brekhofa) oraz woła `OnItemsChanged()`, żeby lista checkboxów się przeładowała |
| `public Type ResolveTargetType()` | leniwe rozwiązanie typu przez `typesInfo.FindTypeInfo(TargetTypeName)?.Type` | **wynik nigdy nie jest cache'owany** — po hot-loadzie nowego assembly stary `Type` byłby martwy; wzorzec `Workflow.cs:129`. **Nigdy `Type.GetType`** — typy runtime są kompilowane Roslynem w pamięci i `Type.GetType` ich nie widzi (na tym wykłada się martwa warstwa `Services/` u Brekhofa) |
| `[NonPersistent] public string TargetTypeShortName` | nazwa krótka do wyświetlenia | jak `WorkflowDefinition` |

**Nie przechowujemy typu jako `Type` z `[ValueConverter(typeof(TypeToStringConverter))]`** (wzorzec Fleetmana), mimo że w interfejsie wygląda tak samo. Kolumna zostaje stringiem z `FullName`, bo tylko rozwiązywanie przez `XafTypesInfo.FindTypeInfo` działa dla typów kompilowanych w locie. Precedens w tym repo: `Workflow.cs`.

#### `ICheckedListBoxItemsProvider` — wybór pól docelowych

Encja implementuje `DevExpress.Persistent.Base.ICheckedListBoxItemsProvider` (jeden `Dictionary<object, string> GetCheckedListBoxItems(string targetMemberName)` + `event EventHandler ItemsChanged`), tak jak `CustomApperance` we Fleetmanie i `AdditionalAppearanceRule` w DataDrive. Dla `nameof(TargetItems)` metoda buduje słownik z `XafTypesInfo.Instance.FindTypeInfo(TargetType).Members`, filtrując `memberInfo.IsVisible && !memberInfo.IsService`, z podpisami przez `CaptionHelper.GetMemberCaption(...)`. `*` („wszystkie") zawsze pierwsze. To realizuje wymóg z makiety: pola wybiera się checkboxami z `ITypeInfo`, nie wolnym tekstem.

### Implementacja `IAppearanceRuleProperties` — pełna mapa

Zweryfikowane bezpośrednio w assembly 26.1.4 (`DevExpress.Persistent.Base.v26.1` — deklaracja interfejsu; `DevExpress.ExpressApp.ConditionalAppearance.v26.1` — argumenty zdarzenia). Interfejs ma **dwanaście członków i tylko tyle**: `IAppearanceRuleProperties : IAppearance`, sześć z bazowego i sześć własnych. Nie ma członka `Id` (`Id` jest wyłącznie na `AppearanceAttribute`) ani `ResultType`.

Jedenaście członków ma `get; set;`, `DeclaringType` jest tylko do odczytu. **Wszystkie settery implementujemy jako puste** (`set { }`) — silnik nigdy nie ma prawa zapisać czegokolwiek do rekordu w bazie. Dwa członki wymagają **jawnej implementacji interfejsu**, bo ich nazwa koliduje z nazwą właściwości trwałej: `Color? IAppearance.BackColor` i `DXFontStyle? IAppearance.FontStyle`.

| Członek interfejsu | Typ | Skąd bierzemy | Zaślepka? |
|---|---|---|---|
| `IAppearance.Priority` | `int` | pole `Priority` | nie |
| `IAppearance.FontStyle` | `DXFontStyle?` | pole `RuleFontStyle` (jawna implementacja) | nie |
| `IAppearance.FontColor` | `Color?` | `AppearanceRolePalette` (patrz niżej) | nie |
| `IAppearance.BackColor` | `Color?` | `AppearanceRolePalette` (jawna implementacja) | nie |
| `IAppearance.Visibility` | `ViewItemVisibility?` | pole `ItemVisibility` | nie |
| `IAppearance.Enabled` | `bool?` | — | **tak, zawsze `null`** (poza zakresem) |
| `IAppearanceRuleProperties.TargetItems` | `string` | pole `TargetItems`, **puste → `"*"`** | normalizacja |
| `IAppearanceRuleProperties.Criteria` | `string` | pole `Criteria`, **puste → `"True"`** | normalizacja |
| `IAppearanceRuleProperties.Context` | `string` | `AppearanceContext.ToString()` | nie |
| `IAppearanceRuleProperties.AppearanceItemType` | `string` | `nameof(AppearanceItemType.ViewItem)` | **tak, zawsze `"ViewItem"`** (poza zakresem) |
| `IAppearanceRuleProperties.Method` | `string` | — | **tak, zawsze `string.Empty`** (nie `null`) — używamy kryteriów, nie metod |
| `IAppearanceRuleProperties.DeclaringType` | `Type` (tylko get) | `ResolveTargetType()` | nie |

Dwie normalizacje w tabeli nie są kosmetyką. Puste `Criteria` i puste `TargetItems` przekazane silnikowi wprost dają nieprzewidywalne zachowanie — Fleetman i DataDrive normalizują je oba (`"True"` / `"*"`), Brekhof nie i jego `??`-fallback jest martwy, bo pole nigdy nie jest `null`, tylko puste. `AppearanceItemType` bierzemy przez `nameof(...)` z enuma DevExpressa, nie jako gołego literału — jeśli DevExpress kiedyś zmieni nazwę, kompilator to złapie.

### Paleta ról

Klasa: `Module/Appearance/AppearanceRolePalette.cs` — statyczna, bez stanu, bez zależności od bazy.

```
public static (Color? Back, Color? Fore) Resolve(AppearanceColorRole role);
```

Rozstrzyganie koloru dla reguły, w tej kolejności:

1. **Nadpisanie administratora** — jeśli `BackColorOverride` **i** `FontColorOverride` są oba ustawione i oba parsują się poprawnie, wygrywają i rola jest pomijana.
2. **Rola** — w przeciwnym razie `AppearanceRolePalette.Resolve(ColorRole)`.
3. **Nic** — jeśli `ColorRole == None`, obie właściwości zwracają `null` (a nie `Color.Empty`; `Color.Empty.Name` to `"0"` i cicho przechodzi przez sprawdzenia na `null` — błąd DataDrive'a).

**Przypadek połowiczny jest rozstrzygnięty jawnie**: nadpisanie ustawione tylko z jednej strony jest **ignorowane w całości** i reguła spada na rolę. Walidator wystawia wtedy `Warning`. Powód jest praktyczny: samo tło bez koloru czcionki to najczęstsza przyczyna nieczytelnych wierszy po zmianie motywu.

#### Parsowanie koloru — dwie pułapki do ominięcia

Format zapisu: `#AARRGGBB`, osiem cyfr szesnastkowych, alfa pierwsza — jak w HIS i DataDrive.

1. **`int.Parse(..., NumberStyles.HexNumber)` przepełnia się dla alfy ≥ `0x80`.** Konwerter Fleetmana (`Utils/ColorValueConverter.cs`) robi dokładnie to i rzuca `OverflowException` na każdym w pełni nieprzezroczystym kolorze w rodzaju `#FF0000FF` — bez żadnego `try/catch` na ścieżce. Nasz parser używa `uint.Parse(..., NumberStyles.HexNumber)` i `Color.FromArgb(unchecked((int)value))`, całość w `try/catch` zwracającym `null`.
2. **Brak wartości musi wracać jako `null`, nie `Color.Empty`.** Konwerter Fleetmana zwraca `Color.Empty` dla `null`, więc `Color?` z pustej kolumny ma `HasValue == true` i silnik nigdy nie widzi „nie ruszaj tego". DataDrive ma ten sam błąd (`Color.Empty.Name` to `"0"`, więc przechodzi przez filtry na pusty łańcuch). U nas brak koloru to zawsze `null`.

Sześć ról i ich znaczenie (odcienie do doboru po sprawdzeniu dwóch punktów empirycznych z sekcji ryzyk):

| Rola | Etykieta | Znaczenie |
|---|---|---|
| `Error` | Błąd | wymaga działania: przeterminowane, niezapłacone, odrzucone |
| `Warning` | Ostrzeżenie | zbliża się termin, brakuje danych |
| `Success` | Sukces | zamknięte pozytywnie, rozliczone |
| `Info` | Informacja | neutralne wyróżnienie informacyjne |
| `Muted` | Wyciszone | mniej ważne, archiwalne, anulowane |
| `Highlight` | Wyróżnione | „patrz tutaj" bez oceny wartościującej |

---

## Kontrakt walidatora

Klasa: `Module/Validation/AppearanceRuleValidator.cs` — statyczna, **bez `IObjectSpace`, bez zapytań do bazy**. To warunek konieczny tezy „jeden walidator w trzech miejscach": miejsce drugie to ścieżka aktywacji widoku, na której zapytanie do bazy jest zakazane.

### Sygnatury

```
public enum AppearanceRuleHealth { Healthy = 0, Warning = 1, Broken = 2 }

public readonly record struct AppearanceRuleCheck(AppearanceRuleHealth Health, string Diagnosis);

public static AppearanceRuleCheck Check(
    string targetTypeName,
    string criteria,
    string targetItems,
    string backColorOverride,
    string fontColorOverride,
    ITypesInfo typesInfo);

// wygodna nakładka na encję — woła Check(...) z jej pól
public static AppearanceRuleCheck Check(AppearanceRule rule, ITypesInfo typesInfo);
```

`typesInfo` wstrzykiwany, nigdy pobierany w środku — dzięki temu kontroler podaje `ObjectSpace.TypesInfo` właściwe dla swojej repliki, a narzędzie AI `XafTypesInfo.Instance`.

### Kolejność sprawdzeń i treści komunikatów

Walidator zatrzymuje się na pierwszym `Broken`; ostrzeżenia zbiera i skleja `" "`.

| # | Warunek | Stan | Komunikat (polski, trafia do `Diagnosis`) |
|---|---|---|---|
| 1 | `TargetTypeName` pusty | `Broken` | „Reguła nie wskazuje encji docelowej." |
| 2 | `typesInfo.FindTypeInfo(TargetTypeName)` → `null` | `Broken` | „Encja **{TargetTypeName}** nie istnieje. Została usunięta albo wygraduowana i zmieniła nazwę. Wskaż istniejącą encję." |
| 3 | typ rozwiązuje się do `BaseObject`, `object` lub `XPBaseObject` | `Broken` | „Reguła celuje w klasę bazową **{nazwa}**, więc pokolorowałaby każdy widok w aplikacji. Wskaż konkretną encję." |
| 4 | `Criteria` pusty | `Broken` | „Reguła nie ma kryterium — nie wiadomo, które wiersze pokolorować." |
| 5 | `CriteriaOperator.Parse(criteria)` rzuca | `Broken` | „Kryterium nie daje się odczytać: {ex.Message}" |
| 6 | któryś węzeł `OperandProperty` nie istnieje w `ITypeInfo.Members` | `Broken` | „Kryterium odwołuje się do pola, którego nie ma. Encja **{typ}** nie ma właściwości **{pole}**." + jeśli jest kandydat w odległości Levenshteina ≤ 2: „ Czy chodziło o **{podpowiedź}**?" |
| 7 | nazwa z `TargetItems` (poza `*`) nie istnieje na typie | `Broken` | „Pole docelowe **{pole}** nie istnieje na encji **{typ}**." |
| 8 | pole z `TargetItems` istnieje, ale `IsVisibleInListView = false` (lub `IsVisibleInDetailView = false` przy kontekście szczegółów) | `Warning` | „Pole **{pole}** zostało ukryte w interfejsie, więc reguła nie ma czego pokolorować. Reguła jest poprawna — tylko bezskuteczna." |
| 9 | ustawione tylko jedno z dwóch nadpisań koloru | `Warning` | „Ustawiono tylko jeden z pary kolorów, więc nadpisanie zostało pominięte i obowiązuje rola. Ustaw oba albo żaden." |
| 10 | `ColorRole = None`, brak nadpisań, brak `RuleFontStyle` i `ItemVisibility` | `Warning` | „Reguła nie zmienia niczego: nie ma roli koloru, nadpisania ani stylu." |
| — | wszystko przeszło | `Healthy` | `Diagnosis` pusty |

**Chodzenie po drzewie kryterium (punkt 6) — precyzyjnie.** Obchodzimy drzewo `CriteriaOperator` i sprawdzamy **wyłącznie węzły `OperandProperty`**. Wywołania funkcji (`FunctionOperator`, np. `LocalDateTimeToday()`, `IsNullOrEmpty(...)`) i stałe pomijamy — inaczej `[TerminPlatnosci] < LocalDateTimeToday()` z makiety zostałoby odrzucone jako „brak pola LocalDateTimeToday". Ścieżki kropkowane (`Kontrahent.NazwaKlienta`) rozwijamy człon po członie przez `ITypeInfo.FindMember(...)`, przechodząc na `MemberTypeInfo` referencji.

### Trzy miejsca użycia

| Miejsce | Wywołuje | Zachowanie |
|---|---|---|
| Narzędzie AI, przed zapisem | `Check(...)` z `XafTypesInfo.Instance` | `Broken` → **odmowa zapisu** i tekst błędu do modelu. `Warning` → zapis przechodzi, ostrzeżenie w odpowiedzi |
| Kontroler, przy aktywacji widoku | `Check(rule, ObjectSpace.TypesInfo)` na każdej wczytanej regule | `Broken` → reguła **pomijana**, wpis do logu. `Warning` → reguła stosowana. **Kontroler nigdy nie zapisuje `Health` z powrotem** — ocenia w pamięci i idzie dalej |
| Ekran reguł | `Health` / `Diagnosis` odczytane z pól | Kolumna „Stan" na liście, panel diagnozy w szczegółach, wiersze niesprawne wyróżnione statycznym `[Appearance]` |

**`Health` w bazie to prawda o schemacie, nie o replice.** Reguła wskazująca encję, której dana replika jeszcze nie skompilowała, jest w bazie `Healthy`, a mimo to lokalnie nierozwiązywalna. Rozbieżność jest z założenia dopuszczalna: bramką jest własne sprawdzenie kontrolera przy każdej aktywacji widoku, a nie zapisany stan. Dlatego kontroler waliduje ponownie, zamiast zaufać kolumnie.

### Kiedy `Health` i `Diagnosis` są utrwalane

Cztery wyzwalacze, każdy przez `Check(...)`:

1. **`AppearanceRule.OnSaving()`** — jedno miejsce pokrywające wszystkie ścieżki zapisu (ekran, narzędzie AI, import). Ustawiamy tylko własne pola obiektu, nic poza nim; to **nie jest** wzorzec DataDrive'a (`OnSaving` → zapis do zewnętrznego cache'u), który przy wycofanej transakcji zostawia regułę-widmo.
2. **Graduacja encji** — po migracji `TargetTypeName` (patrz „Cykl życia").
3. **Usunięcie encji** (`delete_entity`) — przeliczenie reguł wskazujących skasowany typ.
4. **Akcja „Sprawdź reguły" / „Sprawdź tę regułę"** — ręczne przeliczenie na żądanie, z ustawieniem `CheckedOn`.

---

## Kontrakt narzędzi AI

Plik: `Services/SchemaAIToolsProvider.cs`. Dwie prywatne metody + dwa wpisy w `CreateTools()`, w nowej grupie za narzędziami workflow:

```
// Appearance rule tools
AIFunctionFactory.Create(ValidateAppearanceRule, "sprawdz_regule_wygladu"),
AIFunctionFactory.Create(CreateAppearanceRule,   "utworz_regule_wygladu"),
```

`SchemaAIToolsProvider` jest singletonem — **żadnej nowej rejestracji DI**. Parametry są wyłącznie prymitywne, teksty `[Description]` po angielsku (kontrakt dla modelu), teksty zwracane po polsku na ścieżkach odmowy. Całe ciało w `try/catch` zwracającym `$"Error ...: {ex.Message}"` — narzędzie nigdy nie rzuca przez granicę.

### `sprawdz_regule_wygladu` — tylko odczyt

```
[Description("Validate an appearance (row/field colouring) rule WITHOUT saving anything. Returns two kinds of "
           + "feedback you MUST treat differently: 'PROBLEM' means the user named something that does not exist — "
           + "correct it yourself using the entity's real field list. 'MISSING' means the user never said something — "
           + "ASK THE USER ONE SPECIFIC QUESTION about it and wait for the answer. Never invent a criterion and never "
           + "pick a colour role on the user's behalf. Call this before `utworz_regule_wygladu`.")]
private string ValidateAppearanceRule(
    [Description("Runtime entity class name the rule applies to, e.g. 'Faktura', 'Kontrahent'. Short name, not the full namespace.")] string entityName,
    [Description("DevExpress criteria expression deciding which records are painted, e.g. \"[Zaplacona] = False\". Only properties that exist on the entity are accepted.")] string criteria,
    [Description("Semantic colour role. One of: Error, Warning, Success, Info, Muted, Highlight. Leave empty if the user has not said which one — the tool will report it as MISSING.")] string colorRole = null,
    [Description("Semicolon-separated field names to paint, or '*' for the whole row. Defaults to '*'.")] string targetItems = "*",
    [Description("Where the rule applies: 'ListView' (lists, the usual case), 'DetailView' (record screens), or 'Any'. Defaults to 'ListView'.")] string context = "ListView")
```

Odpowiedź przy problemie — dokładnie w konwencji `validate_report_spec`:

```
PROBLEM: Entity 'Faktura' has no property 'Saldo'. Available numeric fields: Kwota, KwotaNetto, KwotaVat, DoZaplaty.
MISSING: colour role — the user did not say which one.

Ask the user about the MISSING item(s) — one clear question at a time. Do NOT guess and do NOT call `utworz_regule_wygladu` yet.
```

Odpowiedź przy komplecie:

```
Appearance rule is valid for entity 'Faktura'.
- Criteria: [Zaplacona] = False
- Target items: * (whole row)
- Colour role: Error
- Context: ListView
Call `utworz_regule_wygladu` with the same arguments to create it.
```

### `utworz_regule_wygladu` — waliduje i zapisuje

```
[Description("Create an appearance rule that colours records in lists or detail screens, based on a criteria "
           + "expression. The rule is DATA, not schema: it needs no Deploy and no restart — the user just refreshes "
           + "the page. Refuses to save when the entity, a field or the criteria expression is wrong. Sets the colour "
           + "by semantic ROLE only; raw colour values are an administrator-only override edited on the rule screen.")]
private string CreateAppearanceRule(
    [Description("Runtime entity class name the rule applies to, e.g. 'Faktura'.")] string entityName,
    [Description("DevExpress criteria expression deciding which records are painted, e.g. \"[Zaplacona] = False\".")] string criteria,
    [Description("Semantic colour role: Error, Warning, Success, Info, Muted or Highlight. Required — never guess it.")] string colorRole,
    [Description("Short human-readable rule name shown on the rules screen, e.g. 'Niezapłacone faktury'. In the user's language.")] string ruleName,
    [Description("Semicolon-separated field names to paint, or '*' for the whole row. Defaults to '*'.")] string targetItems = "*",
    [Description("Where the rule applies: 'ListView', 'DetailView' or 'Any'. Defaults to 'ListView'.")] string context = "ListView",
    [Description("Conflict priority. When two rules paint the same field, the higher priority wins. Defaults to 10.")] int priority = 10)
```

**Narzędzie nie przyjmuje `backColor`, `fontColor`, `viewId`, `fontStyle` ani `visibility`.** Kolor surowy i zawężenie do widoku to furtki administratora edytowane na ekranie reguł — model dostaje węższy kontrakt, żeby reguły z różnych rozmów wyglądały jak jeden system.

Ścieżki odmowy (polski, do czatu, żadnego zapisu):

| Sytuacja | Treść |
|---|---|
| encja nierozwiązana | „Nie znalazłem encji **{entityName}**. Dostępne encje: {lista}. Żadna reguła nie została zapisana." |
| pole w kryterium nie istnieje | „Encja **{typ}** nie ma właściwości **{pole}**. Dostępne pola: {lista}. Żadna reguła nie została zapisana." |
| kryterium nie parsuje | „Kryterium nie daje się odczytać: {ex.Message}. Żadna reguła nie została zapisana." |
| rola koloru pusta lub spoza listy | „Nie podano roli koloru. Do wyboru: Błąd, Ostrzeżenie, Sukces, Informacja, Wyciszone, Wyróżnione. Żadna reguła nie została zapisana." |
| pole z `targetItems` nie istnieje | „Pole docelowe **{pole}** nie istnieje na encji **{typ}**. Żadna reguła nie została zapisana." |
| encja nie jest wdrożona | „Encja **{typ}** nie jest jeszcze wdrożona. Kliknij Deploy Schema, potem poproś o regułę ponownie. Żadna reguła nie została zapisana." |

Komunikat sukcesu (markdown, jak pozostałe narzędzia zapisujące):

```
Utworzono regułę wyglądu **{ruleName}**.

| | |
|---|---|
| Encja | {typ} |
| Kryterium | `{criteria}` |
| Pola | {targetItems} |
| Rola koloru | {rola po polsku} |
| Kontekst | {kontekst po polsku} |
| Priorytet | {priority} |

Odśwież stronę (F5) i otwórz listę — **wdrożenie nie jest potrzebne**, reguła to dane, nie kod.
Regułę można wyłączyć albo poprawić w „Zarządzanie schematem → Reguły wyglądu".
```

Zapis: `CreateObjectSpaceForType(typeof(AppearanceRule))` → `CreateObject<AppearanceRule>()` → `CommitChanges()`, dokładnie jak `BuildReport`. **To jest zapis przez `INonSecuredObjectSpaceFactory`, czyli świadome obejście systemu bezpieczeństwa XAF** — spójne z pozostałymi 19 narzędziami, nazwane tu wprost.

---

## Sekcja promptu systemowego

Do wstawienia w `Services/SchemaDiscoveryService.GenerateSystemPrompt`, **między** sekcję `## Workflows (State Machines)` a `## Supported Field Types`, w tej samej konwencji `sb.AppendLine("...")`. Tekst dosłowny:

```
## Appearance Rules (colouring rows and fields)

- An appearance rule paints records in a list or on a detail screen when a criteria expression matches. Reach for these tools when the user talks about kolor / kolorowanie / podświetl / na czerwono / wyróżnij / wyszarz / oznacz, or dictates something like "zrób faktury niezapłacone na czerwono na listach".
- Tools: `sprawdz_regule_wygladu` (read-only check) and `utworz_regule_wygladu` (validates again, then saves).
- **NEVER guess the parts the user did not say.** Before calling `utworz_regule_wygladu` you must know all three: (1) which entity, (2) the criteria expression, (3) the colour role. If any is missing, ask ONE specific question about it and wait for the answer.
- Translate the user's words into a criteria expression yourself, but ONLY over properties that actually exist on the entity — call `describe_entity` or `sprawdz_regule_wygladu` to check. "Niezapłacone" may be `[Zaplacona] = False` on one entity and `[DoZaplaty] > 0` on another; when both are plausible, say which one you picked and why, and let the user correct you.
- If a tool answers PROBLEM because a property does not exist, do NOT ask the user which field to use — the answer lists the entity's real fields. Pick the closest one, propose it in one sentence, and wait for a yes.
- Colour is a semantic ROLE, never a hex value. The six roles are: Error (needs action — overdue, unpaid, rejected), Warning (deadline approaching, data missing), Success (settled, closed well), Info (neutral emphasis), Muted (archived, cancelled, less important), Highlight ("look here", no judgement). Never invent a role — ask. Raw colours exist, but only an administrator sets them on the rules screen, and you must not offer to do it.
- `targetItems` is `*` (the whole row) unless the user names specific fields. `context` is `ListView` unless the user is clearly talking about a record screen.
- When two rules paint the same field, the one with the higher `priority` wins. Say so if the user creates a second rule over the same field.
- Appearance rules are DATA, not schema: no Deploy, no restart, no rebuild. After creating one, tell the user to refresh the page (F5) and open the list. Also tell them the rule can be switched off or corrected under „Zarządzanie schematem → Reguły wyglądu" — that screen is how a mistake gets undone.
- A rule survives on its own: if the entity it points at is deleted or renamed, the rule stops colouring and is shown as „Niesprawna" on the rules screen instead of breaking the view.
```

Prompt jest odświeżany przed każdą turą (`AIChatService.AskAsync` → `RefreshSystemPrompt()`), więc sekcja działa od razu po wdrożeniu kodu. **To jedyna istniejąca bramka na człowieka** — czysto tekstowa.

---

## Zachowanie kontrolera

Plik: `Module/Controllers/AppearanceRuleViewController.cs`, klasa `ObjectViewController<ObjectView, object>`. Kontrolery w `Module/Controllers/*.cs` są wykrywane automatycznie przez skanowanie XAF — **żadnej rejestracji w `Startup.cs` po żadnej ze stron**, dlatego parytet Blazor/WinForms wychodzi za darmo.

### `OnActivated` — sekwencja bez odstępstw

```
1. base.OnActivated()
2. Strażniki wejściowe — każdy z nich kończy metodę bez efektu:
   – View is not ObjectView
   – View.ObjectSpace is NonPersistentObjectSpace      // dopasowanie WZORCEM, nie GetType() != typeof(...)
   – View.ObjectSpace.IsDisposed
   – View.ObjectTypeInfo?.Type == null
3. LoadRules()  — patrz niżej; CAŁOŚĆ w try/catch, wyjątek → pusta lista + log
4. Jeśli lista pusta → nie subskrybujemy niczego i kończymy (zero kosztu na widokach bez reguł)
5. appearanceController = Frame.GetController<AppearanceController>()
   – null → log i koniec
6. appearanceController.ResetRulesCache()
7. appearanceController.CollectAppearanceRules += OnCollectAppearanceRules
8. appearanceController.Refresh()
```

Kolejność kroków 6–8 jest **nienegocjowalna**. Pominięcie `ResetRulesCache()` sprawia, że zdarzenie w ogóle się nie odpala: DevExpress zacache'ował „brak reguł dla tego typu" z pierwszej, bezregułowej aktywacji widoku. Tę samą kolejność mają wszystkie cztery aplikacje referencyjne i oba niezależne kontrolery we Fleetmanie.

Strażnik `NonPersistentObjectSpace` sprawdzamy **dopasowaniem wzorca** (`is`), nie porównaniem `GetType() != typeof(NonPersistentObjectSpace)` jak Fleetman — porównanie dokładnego typu przepuszcza klasy pochodne.

**Świadome odstępstwo od Fleetmana**: Fleetman ładuje reguły leniwie, w środku handlera (`if (CustomApperances == null) { … zapytanie … }`). U nas zapytanie jest w `OnActivated` i **nigdy w handlerze** — `CollectAppearanceRules` odpala się raz na element interfejsu, więc zapytanie w środku byłoby katastrofą wydajnościową. To jest jeden z obowiązkowych warunków wariantu A.

### `LoadRules()` — jedyne miejsce, w którym rozmawiamy z bazą

```
1. Przestrzeń obiektów z INonSecuredObjectSpaceFactory (Application.ServiceProvider),
   zapamiętana w polu, zwalniana w OnDeactivated.
2. Zapytanie: IsEnabled = True             -- JEDYNY filtr w bazie; NIGDY po Health
3. W pamięci, dla każdej reguły:
   a. type = rule.ResolveTargetType();  type == null  → pomiń
   b. type.IsAssignableFrom(View.ObjectTypeInfo.Type) == false → pomiń
   c. !string.IsNullOrEmpty(rule.ViewId) && rule.ViewId != View.Id → pomiń
   d. AppearanceRuleValidator.Check(rule, ObjectSpace.TypesInfo).Health == Broken → pomiń + log
4. OrderBy(Priority).ThenBy(Name).ThenBy(Oid)   -- deterministycznie, bez wyjątku
5. Zapis do pola _rules
```

**`Health` i `Diagnosis` NIE MOGĄ być filtrem w zapytaniu.** To metadane do pokazania człowiekowi, nie bramka wykonawcza. Jedyną bramką jest żywe sprawdzenie w kroku 3d. Gdyby zapytanie odsiewało po `Health <> Broken`, reguła oznaczona jako niesprawna w oknie graduacji (typ jeszcze nie istnieje pod żadną nazwą) **nigdy nie zostałaby ponownie oceniona** po wklejeniu źródła i przebudowie — zostałaby wykluczona z zapytania, więc nic nie miałoby okazji przeliczyć jej stanu. Kolorowanie nie wróciłoby samo, wbrew kryterium A-23. Ta sama zasada broni każdego innego przypadku, w którym typ staje się rozwiązywalny bez udziału człowieka: restart repliki, wdrożenie, cofnięcie graduacji. Koszt rezygnacji z filtru jest pomijalny — reguł są dziesiątki, nie miliony.

**Dlaczego niezabezpieczona przestrzeń obiektów.** Reguła wyglądu to konfiguracja interfejsu, nie dane użytkownika. Czytanie jej przez `View.ObjectSpace` (zabezpieczoną) oznaczałoby, że użytkownik bez uprawnienia do odczytu `AppearanceRule` dostaje pustą listę i kolorowanie umiera dla niego po cichu. Dodatkowo nie zaśmiecamy śledzenia zmian widoku obcymi obiektami. Cena: odczyt konfiguracji UI omija uprawnienia — akceptowalna, bo nie wypływa z niej żadna dana biznesowa.

**Dopasowanie typu.** `Type.IsAssignableFrom` na rozwiązanym typie, nigdy `StartsWith` na nazwie (dopasowanie prefiksowe złapałoby `FakturaPozycja` regułą napisaną dla `Faktura` — polskie encje runtime dzielą prefiksy) i nigdy sama nazwa krótka bez przestrzeni nazw. Reguła celująca w klasę bazową jest odrzucana już przez walidator, więc `IsAssignableFrom` nie może przypadkiem złapać całej aplikacji.

### Handler `OnCollectAppearanceRules`

```
private void OnCollectAppearanceRules(object sender, CollectAppearanceRulesEventArgs e)
{
    try
    {
        if (_rules == null || _rules.Count == 0) return;
        e.AppearanceRules.AddRange(_rules);        // List<IAppearanceRuleProperties>
    }
    catch (Exception ex) { _log(ex); }            // nigdy nie wypuszczamy wyjątku
}
```

Dwie żelazne zasady:

- **Żadnego zapytania do bazy w handlerze.** `CollectAppearanceRules` odpala się raz na każdy element interfejsu; zapytanie w środku byłoby katastrofą wydajnościową.
- **`try/catch` obejmuje całe ciało.** Nieobsłużony wyjątek tutaj wywraca **cały widok**, nie jedną komórkę.

`CollectAppearanceRulesEventArgs` ma trzy składowe (zweryfikowane w 26.1.4): `List<IAppearanceRuleProperties> AppearanceRules`, `IViewInfo ViewInfo`, `string Name` (identyfikator elementu interfejsu). `ViewInfo` i `Name` **czytamy tylko do logu diagnostycznego** — filtrowanie po elementach robi za nas silnik na podstawie `TargetItems`; tak samo postępują Fleetman i DataDrive.

### `OnDeactivated`

```
1. odsubskrybowanie CollectAppearanceRules (jeśli subskrybowano)
2. _rules = null
3. zwolnienie własnej przestrzeni obiektów
4. base.OnDeactivated()
```

### Odświeżanie po zapisie reguły

Drugi, mały kontroler: `AppearanceRuleEditorController : ObjectViewController<ObjectView, AppearanceRule>` — na widokach samych reguł. Na `ObjectSpace.Committed` (**nigdy `OnSaving`**) resetuje cache reguł **własnej ramki** i woła `Refresh()`.

**Na widokach samych reguł działają dwa kontrolery naraz — i tak ma być.** `AppearanceRuleViewController` jest typu `ObjectViewController<ObjectView, object>`, więc aktywuje się także na liście i szczegółach `AppearanceRule`, obok `AppearanceRuleEditorController`. Po zatwierdzeniu zapisu oba wykonają `ResetRulesCache()` + `Refresh()` na tej samej ramce — dokładnie to podwojenie, które notatka porównawcza wytyka Brekhofowi. Tu jest nieszkodliwe (dwa razy to samo na jednej ramce, bez rozgłaszania) i **nie jest błędem do naprawienia** przez usunięcie którejś subskrypcji: zdejmując pierwszą, tracimy kolorowanie niesprawnych wierszy na liście reguł; zdejmując drugą, tracimy natychmiastowe odświeżenie po edycji.

Nie ma tu żadnego rozgłaszania. Statyczny event `RulesCommitted` (wzorzec Brekhofa) jest tu podwójnie bezużyteczny: trzy repliki to trzy procesy, więc do dwóch pozostałych nigdy nie dotrze, a w Blazor Server każdy użytkownik to osobny obwód — zapis jednej osoby odpalałby zapytanie do bazy u wszystkich pozostałych, na wątku zapisującego, poza dyspozytorami ich obwodów. Pozostali użytkownicy zobaczą regułę przy następnym otwarciu widoku (F5), i to jest zamierzone.

---

## Wygląd i układ

Makiety w `../analysis/design-context/` są **wiążącym wejściem**. Implementация UI musi się do nich odwoływać; planista dołączy `Visual References` do grup zadań interfejsowych. Wszystkie ekrany budujemy standardowymi środkami XAF (`[DefaultClassOptions]`, `[NavigationItem]`, `[XafDisplayName]`, grupy układu, zakładki) — **bez edycji `.xafml`**. Projekt nie ma własnego systemu projektowego: cały język wizualny pochodzi z DevExpress Blazor 26.1.4 (`analysis/design-context/design-resources.md`).

| ID makiety | Plik | Co z niej jest wiążące |
|---|---|---|
| `screen:lista-regul` | `../analysis/design-context/mockups/lista-regu-wygl-du.html` | Kolumny: Nazwa, Encja docelowa, Kryterium, Rola koloru (z próbką), Kontekst, Priorytet, Włączona, **Stan**. Wiersz filtra. Pasek akcji: Nowa / Usuń / Odśwież / **Sprawdź reguły** / **Klonuj**. Wiersze niesprawne na czerwono, z ostrzeżeniem na żółto, wyłączone wyszarzone kursywą — statyczny `[Appearance]`, jak `CustomClass.cs:23-32`. Podsumowanie pod tabelą: „N reguł · M niesprawnych · …" |
| `screen:regula-definicja` | `../analysis/design-context/mockups/regu-a-zak-adka-definicja.html` | Zakładka **Definicja**, grupa „Co reguła obejmuje": Nazwa, Encja docelowa (lista rozwijana), Kryterium (edytor filtra z podpowiedziami tylko z wybranej encji), Pola docelowe (**checkboxy z `ITypeInfo`**, `wszystkie (*)` pierwsze — nie wolne pole tekstowe), Kontekst, Ogranicz do widoku, Priorytet, Włączona. Panel diagnozy pod formularzem dla reguły niesprawnej, z powodem, podpowiedzią naprawy i znacznikiem „Wykryto przy zapisie · {data}". Akcje: „Sprawdź tę regułę", „Klonuj" |
| `screen:regula-wyglad` | `../analysis/design-context/mockups/regu-a-zak-adka-wygl-d.html` | Zakładka **Wygląd**: grupa „Rola koloru" (lista rozwijana + **podgląd** trzech wierszy w bieżącej skórce), grupa „Pozostałe efekty" (Styl czcionki jako 4 checkboxy, Widoczność), na samym dole grupa **„Nadpisanie kolorem — dla administratora, pomija rolę"** z notką „Ustawiając jedno, ustaw drugie" i „Asystent nie korzysta z tych pól" |
| `screen:czat-utworzenie` | `../analysis/design-context/mockups/czat-utworzenie-regu-y-z-dyktowania.html` | Przebieg: dyktowanie → wywołanie `sprawdz_regule_wygladu` widoczne w czacie → **jedno** dopytanie → podsumowanie tabelką „Tworzę?" → `utworz_regule_wygladu` → „wystarczy F5" + odnośniki do „Reguły wyglądu" i do listy |
| `screen:czat-blad` | `../analysis/design-context/mockups/czat-walidacja-odrzuca-bubla.html` | Odmowa wraca z **listą dostępnych pól**, model sam proponuje najbliższe znaczeniowo i pyta o potwierdzenie; osobno dopytuje o rolę koloru, wprost mówiąc, że nie zgaduje koloru |
| `screen:efekt-faktury` | `../analysis/design-context/mockups/efekt-lista-faktur.html` | Efekt końcowy — pokolorowane wiersze niezapłaconych na zwykłej liście faktur, bez żadnego dodatkowego elementu interfejsu |
| `component:odznaka-stanu` | (w `lista-regu-wygl-du.html`) | Odznaka: Sprawna / Ostrzeżenie / Niesprawna / Wyłączona |
| `component:probka-roli` | (w `regu-a-zak-adka-wygl-d.html`) | Próbka koloru **przy nazwie roli**, nie zapisany ARGB |
| `component:panel-diagnozy` | (w `regu-a-zak-adka-definicja.html`) | Powód niesprawności + podpowiedź naprawy |
| `component:wywolanie-narzedzia` | (w makietach czatu) | Blok pokazujący wywołanie narzędzia i wynik, także odmowę |

Grupa nawigacji: **„Zarządzanie schematem"**, obok „Klasy użytkownika" i „Przepływy (maszyny stanów)".

---

## Cykl życia

### 1. Graduacja encji Runtime → Compiled

`GraduationService.GenerateEntityClass` **nie emituje deklaracji `namespace`** (jedyny `namespace` w pliku to `XafXPODynAssem.Module.Services` w linii 4, przestrzeń samego serwisu), a wygenerowany komentarz mówi „Place this file in your BusinessObjects folder". Runtime `FullName` to `XafXPODynAssem.RuntimeEntities.X` (`RuntimeAssemblyBuilder.cs:22`). Po wklejeniu źródła do `BusinessObjects` `FullName` staje się `XafXPODynAssem.Module.BusinessObjects.X`. **Każda reguła cicho przestaje kolorować.** To fakt, nie hipoteza.

Zachowanie:

1. **Migracja** — przy przejściu `CustomClass.Status` na `Graduating`/`Compiled` przepisujemy `TargetTypeName` we wszystkich regułach z `XafXPODynAssem.RuntimeEntities.{X}` na `XafXPODynAssem.Module.BusinessObjects.{X}` i przeliczamy `Health`/`Diagnosis`.
2. **Ostrzeżenie** — `GraduationWarningDetailController` dopisuje do istniejącego komunikatu zdanie o regułach: ile reguł zmigrowano i że zaczną działać po wklejeniu źródła i przebudowie obrazu.
3. **Okno przejściowe** — od kliknięcia „Graduate" do wdrożenia nowego obrazu typ nie istnieje pod żadną nazwą. W tym oknie reguła ma `Health = Broken` z diagnozą „Encja … nie istnieje…" i jest pomijana przez kontroler. Bezpiecznie nieaktywna, nie wybuchowa.
4. **Notatka w generowanym źródle** — nowa sekcja obok istniejących `// --- XPO Note ---` i `// --- Web API Note ---`:

```
// --- Appearance Note ---
// {N} appearance rule(s) point at this entity. They live in the AppearanceRule table
// and keep working after graduation — their TargetTypeName was migrated to
// XafXPODynAssem.Module.BusinessObjects.{ClassName}.
// Do NOT convert them into [Appearance] attributes: an attribute freezes the colour
// and the rule would then need a rebuild to change.
// Review them under „Zarządzanie schematem → Reguły wyglądu".
```

Reguł **nie** generujemy jako atrybutów `[Appearance]`. Powody: reguła zmienia się dalej po ustabilizowaniu encji, a atrybut wymagałby przebudowy obrazu na każdą zmianę koloru — czyli dokładnie tego, czego ta funkcja ma nie wymagać; `[Appearance]` przyjmuje `BackColor` jako konkretny string, więc zamroziłby odcień i skasował korzyść z ról; reguł na jedną encję bywa wiele, od różnych osób, część zawężona przez `ViewId` — nie należą do klasy.

### 2. Usunięcie encji runtime

`delete_entity` (`SchemaAIToolsProvider.cs:53`) kasuje `CustomClass` wraz z polami; jedyna blokada to `Status == Compiled`. Encję da się skasować jednym zdaniem w tym samym czacie, w którym powstają reguły.

Zachowanie: po skasowaniu narzędzie przelicza reguły wskazujące ten typ (`Health = Broken`, diagnoza „Encja … nie istnieje…") i **dopisuje do swojej odpowiedzi**, ile reguł osierociło. Reguł nie kasujemy — użytkownik może chcieć wskazać inną encję. Zabezpieczenie działa nawet bez tego kroku: `ResolveTargetType()` zwraca `null`, kontroler pomija regułę, widok żyje.

### 3. Ukrycie pola wskazywanego przez regułę

W repo obowiązuje zasada „pól nie usuwamy — ukrywamy je" (`FieldTypeChangeGuard`, `CustomFieldDeleteGuardController`). `IsVisibleInListView = false` zostawia właściwość istniejącą, więc strażnik „czy właściwość się rozwiązuje" tego **nie złapie**.

Zachowanie: `FieldTypeChangeGuard` dostaje metodę `FindRulesTargetingField(className, fieldName)` (obok `IsFieldRemovalSafe`, linia 179) i przy ustawianiu `IsVisibleInListView = false` na polu wskazywanym przez aktywną regułę wystawia **ostrzeżenie, nie blokadę** — spójnie z istniejącym zachowaniem strażników. Walidator niezależnie oznacza taką regułę jako `Warning` z tekstem „Pole … zostało ukryte…".

### 4. Reguła na typie, którego dana replika jeszcze nie ma

Sytuacja normalna, nie awaryjna. Użytkownik tworzy encję na replice `red`; `ReplicaSyncService` restartuje `green` po ~54 s i `blue` po ~104 s. W tym oknie reguła siedzi w bazie i wskazuje typ, którego dwie repliki jeszcze nie skompilowały.

Zachowanie: `ResolveTargetType()` zwraca `null` → kontroler pomija regułę → widok działa bez kolorowania → po restarcie repliki kolorowanie pojawia się samo. **Żadnego kodu specjalnego to nie wymaga — pod warunkiem, że strażnik jest.** Bez strażnika dwie z trzech replik wywracają widoki na ~100 sekund po każdym utworzeniu encji.

Skutek uboczny, który trzeba znać: `Health` w bazie pokazuje wtedy `Sprawna`, choć lokalnie reguła nie działa. To jest zamierzone — `Health` opisuje schemat, a nie stan konkretnej repliki.

### 5. Zmiana nazwy pola lub encji

Ryzyko pomijalne. Narzędzia zmiany nazwy nie ma (`grep rename` w `SchemaAIToolsProvider.cs` — 0 trafień), a `FieldTypeChangeGuard` blokuje zmiany na polach wdrożonych. Nie planujemy pod to obsługi.

---

## Uprawnienia i `Updater.cs`

**Wniosek: żadna zmiana w `Updater.cs` nie jest wymagana i projekt celowo od niej nie zależy.**

Trzy fakty:

1. **Kolorowanie nie przechodzi przez system bezpieczeństwa.** Kontroler czyta reguły przez `INonSecuredObjectSpaceFactory`, więc działa dla każdego zalogowanego użytkownika niezależnie od uprawnień do typu `AppearanceRule`.
2. **Ekran reguł jest dostępny dla administratora bez żadnego wpisu.** Rola `Administrators` ma `IsAdministrative = true` (`Updater.cs`, `CreateAdminRole()`), a taka rola omija sprawdzanie uprawnień. Skoro ludzie logują się na wdrożonej instancji, rola tam jest. Tak samo są dziś dostępne `CustomClass` i `WorkflowDefinition` z tej samej grupy nawigacji — bez ani jednego wpisu uprawnień.
3. **Blok w `Updater.cs` na wdrożonej instancji nie istnieje — zweryfikowane w paczce, nie założone.** `XafXPODynAssem.Module.dll` z `/tmp/xpo-publish.tgz` ma `AssemblyConfigurationAttribute = "Release"`, a `UpdateDatabaseAfterUpdateSchema` ma **7 bajtów IL** (samo `base.…()` + `ret`). Cały `#if !RELEASE` jest wycięty; `CreateDefaultRole()` i `CreateAdminRole()` są w assembly, ale nikt ich nie woła. Empirycznie potwierdzono też, że SDK .NET definiuje symbol `RELEASE` przy `-c Release` — więc to nie jest przypadek jednej paczki, tylko reguła.

**Jeśli mimo to rola `Default` ma dostać ekran reguł** (nie jest to wymagane), wpisy muszą stanąć **poza** `#if !RELEASE` — nowa metoda wołana bezwarunkowo z `UpdateDatabaseAfterUpdateSchema`, przed dyrektywą:

```
role.AddTypePermissionsRecursively<AppearanceRule>(SecurityOperations.Read, SecurityPermissionState.Allow);
role.AddNavigationPermission(@"Application/NavigationItems/Items/Default/Items/AppearanceRule_ListView", SecurityPermissionState.Allow);
```

Nadania muszą stać poza blokiem `if (role == null)` — komentarz w `CreateDefaultRole()` wprost o tym ostrzega: wszystko wewnątrz `if` wykonuje się tylko przy pierwszym tworzeniu roli, więc na istniejącej bazie nowe uprawnienia nigdy by nie weszły.

Rejestracja typu — jedna linia w `Module.cs`, tuż za trzema typami Workflow (linie 95–104):

```
AdditionalExportedTypes.Add(typeof(BusinessObjects.AppearanceRule));
```

Bez niej XPO nie założy tabeli i typ nie pojawi się w nawigacji. **`QueryMetadata` / `GetMetadataFingerprint` / `ComputeFingerprint` zostają nietknięte** — dopisanie tam reguł restartowałoby wszystkie trzy repliki przy każdym podyktowanym zdaniu.

---

## Wdrożenie

Wariant **(b)**: zgasić trójkę replik, wdrożyć od nowa, przyjąć jawną przerwę. Nic nowego do napisania — `trzy-repliki.sh` jest sprawdzony.

| # | Krok | Uwaga |
|---|---|---|
| 1 | Transfer paczki na LXC 200 | **wykonuje użytkownik** — `deploy/README-wdrozenie.md` §1 opisuje ten krok jako zablokowany dla agenta; paczka 223 MB |
| 2 | `docker compose down` / zatrzymanie trzech kontenerów `mordeczka-*` | jawna przerwa |
| 3 | Nowy obraz z nową encją | jeden obraz dla wszystkich trzech replik — repliki na różnych obrazach zbudowałyby z jednej bazy różne modele |
| 4 | `--updateDatabase --forceUpdate --silent` dla nowej tabeli | osobny przebieg, przed startem replik; zmiana jest addytywna |
| 5 | `trzy-repliki.sh <obraz>` | starty po kolei, wspólny wolumen `mordeczka-keys` |
| 6 | Test kolorowania **osobno na 8101, 8102 i 8103** | patrz kryteria akceptacji |

Wariant (b) ma jeszcze jedną zaletę, o której nie było mowy przy decyzji: znika pytanie „czy dwie wersje aplikacji piszą jednocześnie do jednego schematu", bo w żadnym momencie nie chodzą równolegle.

Uwaga do `wdroz.sh`: skrypt odmawia startu, gdy `upstream.conf` opisuje więcej niż jeden `server` (`wdroz.sh:49-50`). W wariancie (b) go nie używamy.

---

## Analiza możliwości ponownego użycia

Punktem wyjścia jest werdykt „wersji wspólnej" z `/Users/jacek/Projects/Brekhof/notatki/ConditionalAppearance-porownanie.md`: żadna z czterech implementacji nie jest wzorcem do skopiowania w całości.

### Co bierzemy z tego repo

| Element | Skąd | Co dokładnie |
|---|---|---|
| Encja „dane zamiast atrybutów" | `BusinessObjects/Workflow.cs` | `TargetTypeName` jako string, `ResolveTargetType()` bez cache'owania (linia 129), strażnik aktywności („bez tego kazdy widok w aplikacji dostalby NullReferenceException", ~140), `[DefaultClassOptions]` + `[NavigationItem("Zarządzanie schematem")]` (27–28), polskie `[XafDisplayName]` |
| Wyróżnianie wierszy statycznym atrybutem | `BusinessObjects/CustomClass.cs:23-32` | dwa `[Appearance]` z `Criteria = "Status = 2"` i `FontStyle = DXFontStyle.Italic` — kopiujemy kształt dla wierszy niesprawnych reguł |
| Kształt narzędzia AI | `Services/SchemaAIToolsProvider.cs` | para `ValidateReportSpec`/`BuildReport` (1156, 1207) jako wzorzec dla `sprawdz_regule_wygladu`/`utworz_regule_wygladu`: `[Description]` na metodzie i każdym parametrze, konwencja `PROBLEM:`/`MISSING:`, walidacja powtórzona wewnątrz narzędzia zapisującego, `try/catch` zwracający tekst |
| Zapis z narzędzia | `SchemaAIToolsProvider.CreateObjectSpaceForType` (87–123) | `ScopedObjectSpace` + `INonSecuredObjectSpaceFactory` + `CommitChanges()` |
| Podpięcie pod kontroler DevExpressa | `Controllers/WorkflowCommitController.cs` | subskrypcja w `OnActivated`, odsubskrybowanie w `OnDeactivated` |
| Strażnik z `try/catch` wokół całej logiki | `Controllers/CustomFieldDeleteGuardController.cs` | kształt kontrolera-strażnika i sprzątanie w `OnDeactivated` |
| Sekcja promptu | `Services/SchemaDiscoveryService.GenerateSystemPrompt` | konwencja sekcji `## Workflows` / `## Reports` — jawne reguły w języku naturalnym, obowiązek dopytania, zdanie „no Deploy needed" |
| Ostrzeżenie przy graduacji | `Controllers/GraduationWarningController.cs` | `Application.ShowViewStrategy.ShowMessage(..., InformationType.Warning)` |
| Sekcja-notatka w generowanym źródle | `Services/GraduationService.GenerateSource` (24–57) | konwencja `// --- XPO Note ---` / `// --- Web API Note ---` |
| Strażnik pól | `Validation/FieldTypeChangeGuard.cs:179` | miejsce na `FindRulesTargetingField` |
| Podpowiedź w czacie | `Services/AIChatDefaults.PromptSuggestions` | rekord `PromptSuggestionItem(Title, Text, Prompt)` |
| Silnik wyglądu | `Module.cs:107`, `Blazor.Server/Startup.cs:53`, `Win/Startup.cs:30` | `ConditionalAppearanceModule` i `.AddConditionalAppearance()` **są już podpięte w obu hostach** — dokładamy źródło reguł, nie silnik |

### Co bierzemy z aplikacji referencyjnych

| Element | Skąd | Dlaczego stamtąd |
|---|---|---|
| Encja implementująca `IAppearanceRuleProperties` **bez adaptera** | **Fleetman** (`Fleetman.Module/Controllers/CustomApperanceViewControler.cs` + encja `CustomApperance`) | Jedyna wersja XPO. Adapter Brekhofa to idiom EF Core; w XPO obiekt trwały implementuje interfejs wprost — o klasę mniej |
| Odczyt z bazy przy aktywacji widoku, bez globalnego cache | **Fleetman** | Cache to pole instancji kontrolera, zerowane w `OnActivated`/`OnDeactivated`. Jedyna z czterech wersji, która propaguje się między replikami sama, za darmo |
| Ochrona przed `NonPersistentObjectSpace` | **Fleetman** | Jedyna wersja, która ją ma |
| `Priority` + flaga włączenia, **oba realnie użyte w zapytaniu** | **Fleetman** po commicie `b4cce974` (2026-08-30) | Jedyna wersja, w której oba pola są prawdziwe i faktycznie sortowane/filtrowane |
| Picker typu i edytor kryterium | **Fleetman** (`CustomApperance.cs`) / **Brekhof** | `[TypeConverter(typeof(LocalizedClassInfoTypeConverter))]` + `[ImmediatePostData]` na właściwości typu `Type`; `[CriteriaOptions(nameof(TargetType))]` + `[EditorAlias(EditorAliases.CriteriaPropertyEditor)]` na kryterium |
| Czyszczenie kryterium i pól przy zmianie typu docelowego | **Brekhof** (setter `DataType`) | Kryterium napisane dla poprzedniej encji po zmianie typu jest zawsze bublem — lepiej je wyczyścić niż zostawić do wykrycia przez walidator |
| `ICheckedListBoxItemsProvider` + `[EditorAlias(EditorAliases.CheckedListBoxEditor)]` | **Fleetman** i **DataDrive** (obie mają identycznie) | Konkretny mechanizm za wymaganiem „checkboxy, nie wolny tekst": `GetCheckedListBoxItems(string)` buduje słownik z `ITypeInfo.Members`, a `ItemsChanged` przeładowuje listę po zmianie typu |
| Normalizacja pustych wartości przed oddaniem silnikowi | **DataDrive** (`ToSnapshot()`) | Puste kryterium → `"True"`, puste pola → `"*"`. Brekhof ma tu martwy `??`-fallback (pole nigdy nie jest `null`, tylko puste) |
| Jawna implementacja interfejsu dla kolidujących nazw | **Fleetman** (`Color? IAppearance.BackColor`, `DXFontStyle? IAppearance.FontStyle`) | Jedyny sposób, żeby właściwość trwała i członek interfejsu mogły nazywać się tak samo; setter połyka zapisy silnika |
| Puste settery na wszystkich członkach interfejsu | **Fleetman** / **DataDrive** | Silnik nie ma prawa zapisać niczego do rekordu w bazie |
| Rozwiązywanie typu przez `XafTypesInfo.FindTypeInfo(FullName)?.Type` | **DataDrive** | Jedyne, które działa dla typów z innych assembly — i jedyne, które w ogóle zadziała dla typów kompilowanych Roslynem w locie |
| Zawężenie do widoku (`ViewId`) | **DataDrive** / **HIS** | Ta sama reguła na liście i w szczegółach to rzadko to samo, czego się chce |
| Sortowanie `OrderBy(Priority)` na ścieżce odczytu | **DataDrive** / **Fleetman** | Kopiujesz `Priority` — kopiujesz `OrderBy` |
| Kolor jako **tekst**, nie `int` | **HIS** / **DataDrive** | Kolumna czytelna z SQL-a i zapisywalna bez typów `System.Drawing` — istotne, gdy regułę tworzy narzędzie AI |
| Wybór pól docelowych **checkboxami z `ITypeInfo`** | **HIS** (`ICheckedListBoxItemsProvider`) | Checkboxy zamiast wolnego tekstu; `*` zawsze pierwsze |
| Akcja „Klonuj" na liście reguł | **HIS** / **DataDrive** | Kilka linii, duży zysk przy serii podobnych reguł |
| Ręczna akcja przeliczenia (u nas „Sprawdź reguły") | **HIS** (akcja „Odśwież cache aplikacji") | Tani wzorzec operacyjny: mówi operatorowi wprost, że coś może się rozjechać, i daje przycisk zamiast restartu |
| `IAppearanceRuleProperties` jako **ogólny kontrakt wejściowy** | **HIS** (`AppearanceModel` — lekki POCO bez trwałości) | Uzasadnia, że nasza encja może być jednocześnie rekordem i nośnikiem dla silnika |
| Inwalidacja przez `ObjectSpace.Committed` | **żadna z czterech** — piszemy sami | DataDrive pisze do cache w `OnSaving`, więc wycofana transakcja zostawia regułę-widmo; Brekhof rozgłasza statycznym eventem po całym procesie |

### Czego świadomie NIE kopiujemy

| Anty-wzorzec | Skąd | Dlaczego |
|---|---|---|
| `StartsWith` do dopasowania typu | Brekhof | Łapie typy-rodzeństwo, nie tylko pochodne. Polskie encje runtime dzielą prefiksy: reguła dla `Faktura` złapałaby `FakturaPozycja` |
| Dopasowanie po samej nazwie klasy bez przestrzeni nazw | HIS | Kolizja nazw w dwóch przestrzeniach myli filtr, a typy pochodne nie łapią się wcale — funkcjonalnie gorzej niż ręczny hack |
| Zaszyty w kodzie wyjątek na jedną klasę | Fleetman (`Faktura`) | Nie skaluje się; zastępujemy `IsAssignableFrom` na rozwiązanym `Type` |
| Statyczny event `RulesCommitted` | Brekhof | Trzy repliki to trzy procesy — event nigdy tam nie dotrze. Dodatkowo w Blazor Server rozgłasza się po wszystkich obwodach, poza ich dyspozytorami |
| Zapis do cache w `OnSaving` | DataDrive | Wycofana transakcja zostawia regułę-widmo, aktywną do restartu procesu |
| `Priority` obecne, ale nieużywane w sortowaniu | HIS | Najgorszy rodzaj pułapki — kopiujący przenosi pole, które wygląda na działające |
| Budowanie evaluatora w pętli renderowania | DataDrive (`FitAppearance` per wiersz) | Kryterium parsuje raz silnik DevExpressa; my nie ewaluujemy niczego per wiersz |
| `Color.Empty` zamiast `null` dla braku koloru | DataDrive **i** Fleetman | `Color.Empty.Name` to `"0"` i cicho przechodzi przez sprawdzenia na `null`; konwerter Fleetmana zwraca `Color.Empty` dla `null` z bazy, więc silnik nigdy nie widzi „nie ruszaj tego" |
| `int.Parse(..., NumberStyles.HexNumber)` na ośmiu cyfrach ARGB | Fleetman (`ColorValueConverter`) i DataDrive (`ParseColor`) | Przepełnia `int` dla każdej alfy ≥ `0x80`, czyli dla każdego nieprzezroczystego koloru, i rzuca z gettera właściwości — bez `try/catch` na ścieżce. Używamy `uint.Parse` + `unchecked((int)…)` w `try/catch` |
| Leniwe ładowanie reguł wewnątrz handlera `CollectAppearanceRules` | Fleetman | Handler odpala się raz na element interfejsu; zapytanie do bazy w środku to katastrofa wydajnościowa. Ładujemy w `OnActivated` |
| `catch (Exception) { return new List<>(); }` wokół całego ładowania | Brekhof | Jedna zła reguła wyłącza **wszystkie** reguły dla widoku. U nas `try/catch` jest per reguła: zła jest pomijana, reszta działa |
| `ObjectSpace.GetType() != typeof(NonPersistentObjectSpace)` | Fleetman | Porównanie dokładnego typu przepuszcza klasy pochodne — używamy dopasowania wzorca `is` |
| Zaszyty w kodzie zakres per pracownik (`Pracownicy`) | Fleetman | Przydatna funkcja u nich, u nas niepotrzebna komplikacja — reguła jest globalna albo zawężona przez `ViewId` |
| Martwa warstwa `Services/` z `Type.GetType` bez fallbacku | Brekhof | Zielone testy na ścieżce, która na realnych danych zwróciłaby zero reguł — bo encja celowo zapisuje samo `FullName` |
| Brak flagi włącz/wyłącz | HIS | Jedyny sposób dezaktywacji to skasowanie reguły |
| Cache jako zwykła `List<T>` bez zabezpieczenia wątkowego | HIS | Nie mamy globalnego cache'u w ogóle, więc problem nie powstaje |
| Kopiowanie kodu z repo Brekhofa | Brekhof | **Repo nie ma pliku licencji — formalnie nie wolno przenosić z niego kodu.** Bierzemy stamtąd wyłącznie wnioski |

### Co piszemy sami, bo nikt tego nie ma

- **Walidator z diagnozą utrwaloną na encji.** We wszystkich czterech aplikacjach niesprawna reguła przestaje działać w ciszy. Żadna nie waliduje kryterium przy zapisie — błędne wyrażenie ujawnia się dopiero przy renderowaniu widoku.
- **Obsługa reguły wskazującej nieistniejącą już właściwość.** Ani Brekhof, ani DataDrive nie mają jej wcale: `grep` za `FindMember` w kodzie wyglądu obu repozytoriów daje **zero trafień**. Obie pilnują tylko momentu tworzenia reguły w interfejsie (lista pól z `ITypeInfo`), nic nie chroni reguły, która przeżyła swoje pole.
- **Chodzenie po drzewie kryterium po węzłach `OperandProperty`** i sprawdzanie właściwości w `ITypeInfo.Members`, z pominięciem `FunctionOperator`.
- **Rola koloru zamiast ARGB** wraz z paletą rozwiązywaną przy renderowaniu.
- **Narzędzia AI tworzące reguły** — żadna z czterech aplikacji nie ma AI w tej ścieżce.
- **Migracja `TargetTypeName` przy graduacji** — problem specyficzny dla mordeczki (encje kompilowane w locie).

---

## Kryteria akceptacji

### Propagacja na trzy repliki — kryterium rozstrzygające

| # | Kryterium | Jak sprawdzić |
|---|---|---|
| A-01 | Reguła utworzona z czatu na jednej replice koloruje na wszystkich trzech, **bez restartu** | Utwórz regułę na `http://…:8101`, odśwież F5, otwórz listę faktur kolejno na **8101, 8102 i 8103**. Kolorowanie musi być identyczne na każdym porcie |
| A-02 | Reguła wstawiona **wprost do PostgreSQL**, z pominięciem aplikacji, koloruje na replice, która nie obsłużyła zapisu i nie była restartowana | `INSERT` do tabeli `AppearanceRule` → świeża sesja przeglądarki na 8102 → koloruje. To jest test, którego nie da się oszukać |
| A-03 | Utworzenie reguły **nie restartuje** żadnej repliki | `docker ps --filter 'label=aplikacja=mordeczka'` przed i po — kolumna `Status` (uptime) bez zmian dla wszystkich trzech |

### Ścieżka AI

| # | Kryterium | Jak sprawdzić |
|---|---|---|
| A-04 | Zdanie „zrób faktury niezapłacone na czerwono na listach" prowadzi do reguły z kryterium nad istniejącym polem | Czat; asystent dopytuje o zakres (cały wiersz vs kolumny) i o rolę, jeśli jej nie podano |
| A-05 | Nazwa nieistniejącego pola **nie tworzy reguły** i wraca z listą dostępnych pól | „pokoloruj faktury gdzie saldo jest dodatnie" → odmowa z listą pól liczbowych, zero rekordów w tabeli |
| A-06 | Asystent **nie zgaduje roli koloru** | Zdanie bez koloru → jedno pytanie o rolę z sześcioma opcjami, bez zapisu |
| A-07 | Kryterium z funkcją przechodzi walidację | `[TerminPlatnosci] < LocalDateTimeToday()` zapisuje się bez błędu — walidator nie traktuje `LocalDateTimeToday` jak właściwości |
| A-08 | Asystent kończy zdaniem o F5, nie o wdrożeniu | Treść odpowiedzi zawiera „wdrożenie nie jest potrzebne" |

### Odporność

| # | Kryterium | Jak sprawdzić |
|---|---|---|
| A-09 | Reguła wskazująca skasowaną encję **nie wywraca żadnego widoku** | Utwórz regułę → `delete_entity` na tej encji → przeklikaj wszystkie widoki list. Zero błędów, reguła na swojej liście jako „Niesprawna" z diagnozą |
| A-10 | Reguła z kryterium nad nieistniejącym polem, wstawiona **wprost do bazy**, jest pomijana, a widok działa | `INSERT` z `Criteria = "[Kwotaa] > 10"` → lista faktur otwiera się normalnie, bez kolorowania, wpis w logu |
| A-11 | Reguła na typie, którego replika jeszcze nie skompilowała, nie wywraca widoków | Utwórz encję + regułę na 8101, natychmiast otwórz widoki na 8103 (przed jej restartem). Zero błędów |
| A-12 | Wyjątek wewnątrz handlera nie wywraca widoku | Przegląd kodu: `try/catch` obejmuje całe ciało `OnCollectAppearanceRules` |
| A-13 | Reguła celująca w klasę bazową jest odrzucana | Próba `TargetTypeName = "DevExpress.Persistent.BaseImpl.BaseObject"` → `Broken` z diagnozą o klasie bazowej |

### Interfejs

| # | Kryterium | Jak sprawdzić |
|---|---|---|
| A-14 | „Reguły wyglądu" widoczne w grupie „Zarządzanie schematem" bez edycji `.xafml` | `git status` po wdrożeniu — żaden `.xafml` nie zmieniony; pozycja jest w nawigacji |
| A-15 | Kolumna „Stan" na liście, wiersze niesprawne wyróżnione | Zgodność ze `screen:lista-regul` |
| A-16 | Panel diagnozy pokazuje powód i podpowiedź | Zgodność ze `screen:regula-definicja`; reguła z literówką w polu pokazuje „Czy chodziło o …?" |
| A-17 | Zakładka „Wygląd" ma rolę z próbką, a nadpisanie kolorem na dole, opisane jako awaryjne | Zgodność ze `screen:regula-wyglad` |
| A-18 | Pola docelowe wybiera się checkboxami z listy pól encji, nie wolnym tekstem | Lista budowana z `ITypeInfo` wybranej encji, `wszystkie (*)` pierwsze |
| A-19 | Wyłączenie reguły (`IsEnabled = false`) natychmiast zdejmuje kolorowanie po F5 | Bez restartu, na wszystkich trzech replikach |
| A-20 | Akcja „Sprawdź reguły" przelicza stany całej listy i ustawia „Sprawdzono" | — |
| A-21 | Reguły działają w kliencie WinForms | Uruchom `XafXPODynAssem.Win`, otwórz tę samą listę — kolorowanie identyczne |

### Priorytet i graduacja

| # | Kryterium | Jak sprawdzić |
|---|---|---|
| A-22 | Przy dwóch regułach na tym samym polu wygrywa ta o wyższym `Priority`, deterministycznie | Reguły `Priority = 10` (zielona) i `Priority = 20` (czerwona) na tym samym warunku → wiersz czerwony, powtarzalnie po odświeżeniu |
| A-23 | Graduacja encji migruje `TargetTypeName` i mówi, ile reguł ruszyła | Komunikat ostrzegawczy zawiera liczbę; po wklejeniu źródła i przebudowie kolorowanie wraca |
| A-24 | Wygenerowane źródło zawiera sekcję `// --- Appearance Note ---` | Podgląd źródła po graduacji encji, na którą wskazuje ≥1 reguła |
| A-25 | Ukrycie pola wskazywanego przez regułę daje ostrzeżenie, nie blokadę | `IsVisibleInListView = false` → komunikat; reguła dostaje `Warning`, nie `Broken` |

### Higiena wdrożeniowa

| # | Kryterium | Jak sprawdzić |
|---|---|---|
| A-26 | Odcisk metadanych nietknięty | `git diff` na `Module.cs` — żadnej zmiany w `QueryMetadata` / `GetMetadataFingerprint` / `ComputeFingerprint` |
| A-27 | Tabela `AppearanceRule` powstaje krokiem `--updateDatabase`, przed startem replik | Log przebiegu; `\d "AppearanceRule"` w psql |
| A-28 | Kolorowanie działa dla użytkownika **bez** uprawnień do `AppearanceRule` | Zaloguj się jako nie-administrator: kolorowanie widoczne. **To jest ta połowa, którą projekt gwarantuje** |
| A-29 | *(obserwacja, nie gwarancja)* Pozycja „Reguły wyglądu" niewidoczna w nawigacji dla nie-administratora | Zależy od polityki roli, nie od tego projektu. `Blazor.Server/Startup.cs` używa `UseIntegratedMode` z `PermissionPolicyRole` i **nie ustawia jawnie żadnej polityki uprawnień** — obowiązuje domyślna polityka roli. Odnotować wynik, nie traktować jako warunku odbioru |

---

## Wskazówki wdrożeniowe

### Podejście testowe

W repo **nie ma projektu testowego** i to zadanie go nie tworzy. Weryfikacja jest ręczna, wg tabel powyżej. Gdyby jednak plan implementacji wydzielił grupy zadań, obowiązuje zasada **2–8 skupionych testów na grupę**, a weryfikacja uruchamia wyłącznie testy nowe, nie cały zestaw. Naturalny podział grup:

| Grupa | Zakres | 2–8 sprawdzeń |
|---|---|---|
| G1 — encja i rejestracja | `AppearanceRule`, enumy, `Module.cs`, tabela | A-14, A-26, A-27 |
| G2 — walidator | `AppearanceRuleValidator`, wszystkie 10 reguł | A-07, A-13, A-25 |
| G3 — kontroler | `AppearanceRuleViewController`, paleta | A-01…A-03, A-09…A-12, A-21, A-22, A-28 |
| G4 — narzędzia AI i prompt | dwa narzędzia, sekcja promptu, podpowiedź | A-04…A-08 |
| G5 — ekran reguł | lista, szczegóły, akcje, statyczne `[Appearance]` | A-15…A-20 |
| G6 — cykl życia | graduacja, `delete_entity`, `FieldTypeChangeGuard` | A-23, A-24, A-25 |
| G7 — wdrożenie | wariant (b), test na 8101/8102/8103 | A-01, A-02, A-03 |

### Zgodność ze standardami

`.maister/docs/INDEX.md` w tym repozytorium **nie istnieje** — nie ma plików standardów do zastosowania. Obowiązują konwencje odczytane z kodu:

- Identyfikatory C# angielskie, napisy dla użytkownika polskie przez `[XafDisplayName]`; brak warstwy `.resx`.
- Teksty `[Description]` narzędzi AI **angielskie** (kontrakt dla modelu), stringi zwracane na ścieżkach odmowy **polskie**.
- ORM: XPO. Konstruktor `(Session session)`, `SetPropertyValue(nameof(X), ref x, value)`.
- Kontrolery w `Module/Controllers/*.cs`, wykrywane automatycznie — bez ręcznej rejestracji.
- Narzędzia AI: prywatne metody z `[Description]`, parametry wyłącznie prymitywne, błędy jako tekst, nigdy wyjątek przez granicę.
- Rejestracja typów trwałych przez `AdditionalExportedTypes.Add` w konstruktorze modułu.
- `FontStyle` to `DevExpress.Drawing.DXFontStyle`, nie `System.Drawing.FontStyle`.
- Brak koloru zwracamy jako `null`, nigdy `Color.Empty`.

### Korekty do `CLAUDE.md` (z fazy 1, plus jedna nowa)

- Baza to **PostgreSQL** (`XpoProvider=Postgres`), nie SQL Server localdb.
- Target **net8.0**, DevExpress **26.1.4**, nie 25.2.
- **Zmienna `XAF_UPDATE_DB` jednak istnieje** — `Blazor.Server/BlazorApplication.cs:37` czyta ją w obsłudze `DatabaseVersionMismatch`, a `deploy/bezprzerwy/trzy-repliki.sh:62` ustawia ją na `1` dla każdej repliki. Notatka fazy 1 mówiąca, że zmiennej nie ma, jest błędna, a `deploy/bezprzerwy/README.md` ma rację.
