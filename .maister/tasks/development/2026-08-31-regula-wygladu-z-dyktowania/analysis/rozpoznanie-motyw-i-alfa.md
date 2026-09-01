# Rozpoznanie: motyw i kanał alfa

**Data:** 2026-08-31
**Grupa:** 1 (rozpoznanie empiryczne — bez kodu produkcyjnego)
**Środowisko dowodu:** `https://localhost:5001`, PostgreSQL `localhost:5432/XafXPODynAssem`, DevExpress 26.1.4, XAF Blazor Server, przeglądarka Chromium (Playwright)

---

## Werdykt w dwóch zdaniach

**Alfa przechodzi do CSS — paleta ma sześć wartości półprzezroczystych, nie dwanaście nieprzezroczystych.**
**Motyw da się odczytać z kontrolera w Blazorze, ale nie z projektu `Module` i nie da się z niego wyciągnąć „jasny/ciemny" wspieranym API — a skoro alfa przechodzi, paleta i tak ma jeden zestaw odcieni, nie dwa.**

---

## Pytanie 1 — czy aktywny motyw jest odczytywalny z kontrolera?

### Odpowiedź: TAK w hoście Blazor, ale z trzema zastrzeżeniami. Konsekwencja i tak jest jedna: **paleta ma jeden zestaw odcieni.**

Ścieżka istnieje i jest publiczna:

| Element | Widoczność | Co daje |
|---|---|---|
| `DevExpress.ExpressApp.Blazor.Services.IThemeService` | **public** | `CurrentTheme`, `CurrentFluentTheme`, `ThemesGroups`, zdarzenie `CurrentThemeChanged` |
| `DevExpress.ExpressApp.Blazor.Services.Theme` | **public** | `Caption` (np. `"Blazing Dark"`), `Url`, `Color`, `AccentColor` |
| `DevExpress.ExpressApp.Blazor.Services.IThemeStateService` | **public** | `CurrentClassicTheme`, `GetThemeByCaption(string)`, zdarzenie `ThemeRestoredOnCircuit` |
| `DevExpress.ExpressApp.Blazor.Services.IThemeInternalService` | **internal** | `IsDark`, `IsFluent` — **niedostępne dla nas** |

Czyli `Application.ServiceProvider.GetService<IThemeService>()?.CurrentTheme?.Caption` zwróciłoby w `ViewController` napis w rodzaju `"Blazing Dark"`.

Zastrzeżenia, każde wystarczające, żeby nie budować na tym palety:

1. **Projekt `Module` nie widzi tego typu.** `XafXPODynAssem.Module.csproj` nie ma `PackageReference` na `DevExpress.ExpressApp.Blazor` (sprawdzone — 22 pakiety, żaden blazorowy). Kontroler reguł ma według W-20 działać w Blazorze **i** WinForms z jednego pliku w `Module`. Dołożenie referencji blazorowej to złamanie tego wymagania; alternatywą byłaby refleksja po nazwie typu, czyli krucha magia.
2. **Nie ma wspieranego API „czy motyw jest ciemny".** `IsDark` siedzi wyłącznie na `IThemeInternalService`, a ten interfejs jest **internal** (potwierdzone refleksją: `public=False`). Zostałoby mapowanie po `Caption` — czyli własna tablica nazw, którą trzeba ręcznie utrzymywać. Grupa Fluent w `appsettings.json` ma 11 wariantów koloru, każdy z osobnym `ThemeMode` — tablica rozjeżdża się przy pierwszej zmianie konfiguracji.
3. **Motyw jest per przeglądarka, nie per użytkownik i nie globalnie.** Po przełączeniu na `Blazing Dark` pojawiło się ciasteczko `XAF_CurrentTheme = Blazing%20Dark`. Tabela `ModelDifference` została przy jednym wierszu, a XML jej aspektu nie zawiera ani słowa `Theme`, `Skin` czy `Color`. Nie ma więc czego czytać z modelu aplikacji — nawet gdyby chcieć iść tą drogą.

### Dowód

Ciasteczka **przed** przełączeniem motywu (odczyt z kontekstu przeglądarki):

```
XAF_SizeMode = Medium
.AspNetCore.Cookies = CfDJ8PiMFGGdSv9A…
.AspNetCore.Antiforgery.k9Inz5Rn3OQ = CfDJ8PiMFGGdSv9A…
```

Ciasteczka **po** przełączeniu na `Blazing Dark` — doszła jedna pozycja:

```
XAF_CurrentTheme = Blazing%20Dark
```

`localStorage` trzyma tylko `ApplicationStateRawData` — nic o motywie.

Arkusz stylów faktycznie podpięty po przełączeniu:

```
https://localhost:5001/_content/DevExpress.Blazor.Themes/blazing-dark.bs5.min.css?v26.1.4.0
```

Baza po przełączeniu (bez zmian względem stanu sprzed):

```
SELECT count(*) FROM "ModelDifference";   -->  1
grep -i theme  na  "ModelDifferenceAspect"."Xml"  -->  0 trafień
```

Widoczność typów — refleksja przez `MetadataLoadContext` na
`~/.nuget/packages/devexpress.expressapp.blazor/26.1.4/lib/net8.0/DevExpress.ExpressApp.Blazor.v26.1.dll`:

```
=== DevExpress.ExpressApp.Blazor.Services.IThemeService        | public=True  | interface=True
    Property Theme CurrentTheme
    Property DxThemeFluent CurrentFluentTheme
    Event    EventHandler CurrentThemeChanged
=== DevExpress.ExpressApp.Blazor.Services.Theme                | public=True
    Property String Caption
    Property String Url
=== DevExpress.ExpressApp.Blazor.Services.IThemeInternalService | public=False | interface=True
    Property Boolean IsDark        <-- internal, poza zasięgiem
```

Zasięg życia usługi — **wniosek z ograniczenia DI, nie odczyt rejestracji.** Odczytany z assembly jest konstruktor `ThemeStateService`: przyjmuje `IHttpContextAccessor`, `IXafJSRuntime`, `IOptionsSnapshot<ThemeOptions>`, `IThemeChangeService`. `IOptionsSnapshot<T>` jest w kontenerze zarejestrowany jako scoped i nie da się go wstrzyknąć do singletona — z czego wynika, że `ThemeStateService`, a za nim `ThemeService`, muszą być scoped, czyli **na obwód (circuit)**. Zgadza się to ze zdarzeniem `IThemeStateService.ThemeRestoredOnCircuit`, które nazywa obwód wprost, i z ciasteczkiem `XAF_CurrentTheme` (dowód poniżej). Samej linijki rejestracji w DI nie odczytałem — i nie jest do niczego potrzebna, bo werdykt opiera się na ciasteczku i na pustej tabeli `ModelDifference`.

---

## Pytanie 2 — czy `BackColor` przenosi kanał alfa do CSS?

### Odpowiedź: TAK. Konsekwencja: **paleta ma sześć wartości półprzezroczystych**, nie dwanaście nieprzezroczystych.

Ośmiocyfrowy zapis `#AARRGGBB` przechodzi całą drogę — od parsowania ciągu w atrybucie, przez `Color?`, po `background-color` wyliczony przez przeglądarkę. Alfa nie ginie po drodze i zachowuje się tak samo na motywie jasnym i ciemnym.

### Dowód

Sonda tymczasowa na `CustomClass` — **dwie** reguły na tym samym kryterium, różniące się wyłącznie kanałem alfa, celujące w dwie różne kolumny. Druga reguła (`#FF…`) jest kontrolą: dowodzi, że parser poprawnie czyta trzy ostatnie bajty jako RGB, więc ewentualna utrata alfy nie byłaby artefaktem parsowania.

```csharp
[Appearance("SondaAlfa",     TargetItems = nameof(ClassName),       Criteria = "Status = 0", Context = "ListView", BackColor = "#20DC3545")]
[Appearance("SondaBezAlfy",  TargetItems = nameof(NavigationGroup), Criteria = "Status = 0", Context = "ListView", BackColor = "#FFDC3545")]
```

Odczyt `getComputedStyle(td).backgroundColor` (wartość faktycznie zastosowana przez przeglądarkę, nie atrybut `style`), 9 wierszy ze `Status = 0`:

| Kolumna | Zadany `BackColor` | Motyw domyślny (`DevExpress Fluent`) | Motyw `Blazing Dark` |
|---|---|---|---|
| `Nazwa klasy` | `#20DC3545` | **`rgba(220, 53, 69, 0.13)`** | **`rgba(220, 53, 69, 0.13)`** |
| `Grupa nawigacji` | `#FFDC3545` | `rgb(220, 53, 69)` | `rgb(220, 53, 69)` |

`0x20 = 32`; `32/255 = 0,1255`, a przeglądarka raportuje zaokrąglone `0.13`. To jest dokładnie wartość przewidziana w kryterium odbioru (`rgba(220, 53, 69, 0.125)`).

Kontrola działa jak trzeba: przy `#FF` wychodzi `rgb(...)` bez alfy, przy `#20` wychodzi `rgba(...)` z alfą — ta sama trójka RGB `220, 53, 69` w obu przypadkach. Parser czyta osiem cyfr poprawnie, a różnica bierze się wyłącznie z kanału alfa.

Zrzuty ekranu (poza repozytorium, w katalogu roboczym sesji):
`…/scratchpad/lista-jasny.png`, `…/scratchpad/lista-ciemny.png`.

### Czytelność na obu motywach (krok 1.4)

Ta sama wartość `#20DC3545` jest czytelna na obu motywach — i to jest sedno wygranej.

- **Jasny:** bardzo blady róż, tekst czarny. Czytelny, ale odcień jest **na granicy zauważalności** — przy 12,5% krycia wyróżnienie łatwo przeoczyć.
- **Ciemny:** subtelny ciemnoczerwony nalot, tekst jasny. Czytelny i wyraźniejszy niż na jasnym.

Dla kontrastu kolumna nieprzezroczysta (`#FFDC3545`) wygląda na obu motywach jak solidny czerwony blok. Na ciemnym jest agresywna i przykuwa wzrok bardziej niż treść wiersza — dokładnie ta wada, której paleta półprzezroczysta pozwala uniknąć.

**Rekomendacja liczbowa dla grupy 2:** `0x20` (12,5%) to za mało na motywie jasnym. Sensowny punkt startowy to `0x33`–`0x40` (20–25%) — mocniej niż w sondzie, wciąż daleko od bloku koloru. Ostateczną wartość ustala grupa 2; ten dokument dostarcza tylko dowodu, że skala krycia w ogóle działa.

---

## Co z tego wynika dla grupy 2

1. **`AppearanceRolePalette` zwraca jeden zestaw sześciu par półprzezroczystych** (`Error`, `Warning`, `Success`, `Info`, `Muted`, `Highlight`), plus `None → (null, null)`. Bez rozgałęzienia jasny/ciemny.
2. **Paleta nie zna motywu i nie ma znać.** Zostaje statyczna, bezstanowa, bez zależności od DI — zgodnie ze specyfikacją (`Module/Appearance/AppearanceRolePalette.cs`). To zdejmuje problem `IThemeService` z projektu `Module` w całości i nie łamie parytetu Blazor/WinForms (W-20).
3. **Kolor czcionki najczęściej zostawiamy `null`**, żeby odziedziczyć kolor tekstu z motywu. Nakładka półprzezroczysta przepuszcza tło motywu, więc domyślny kolor tekstu pozostaje czytelny — dowód powyżej, obie skórki. To jednak nie kasuje reguły ze specyfikacji, że **nadpisanie administratora liczy się tylko w parze tło+czcionka**: przy nieprzezroczystym ARGB od administratora sam kolor tła bez czcionki nadal grozi nieczytelnym wierszem.
4. **Format zapisu `#AARRGGBB` jest potwierdzony w praktyce**, nie tylko w dokumentacji — parser XAF-a czyta osiem cyfr szesnastkowych z alfą na początku.

---

## Czego NIE ustalono

- **Kierunku rozstrzygania konfliktu priorytetów** (`Priority` 10 vs 20 na tym samym polu). To osobne otwarte pytanie ze specyfikacji, oznaczone `[SPRAWDZIĆ NA PROTOTYPIE]`, i nie należało do tej grupy. Sonda używała dwóch reguł na **rozłącznych** kolumnach właśnie po to, żeby nie mieszać obu pytań.
- **Zachowania alfy w hoście WinForms.** Sprawdzone wyłącznie w Blazorze. WinForms renderuje przez `DXFont`/`Color`, a nie przez CSS, i tam półprzezroczyste tło może zachować się inaczej. Jeśli parytet WinForms ma znaczenie praktyczny, wymaga osobnego sprawdzenia.

---

## Uwagi

- **`ModelDifferenceAspect` zmienił się w trakcie sesji** — wiersz kontekstu współdzielonego urósł z 5715 na 5654 znaków i zmienił md5. To niemal na pewno skutek uboczny przeglądania widoków przez Playwright (szerokości kolumn, stan gridu), a nie przełączenia motywu: grep po `theme`, `skin` i `color` w tym XML-u nie daje ani jednego trafienia, przed zmianą motywu ani po. Dla obu odpowiedzi bez znaczenia; notuję, bo wartość się zmieniła.
- **W repozytorium jest cudza praca w toku, dotykająca terenu grupy 2.** Niezatwierdzone: `AppearanceRuleData.cs`, `AppearanceRuleCommitController.cs`, `DataDrivenAppearanceController.cs`, `DataDrivenAppearanceAdapter.cs` (nieśledzone) oraz zmodyfikowane `Module.cs` i `SchemaAIToolsProvider.cs`. W bazie istnieje już tabela `AppearanceRuleData`. Specyfikacja mówi o `AppearanceRule.cs`, a na dysku leży `AppearanceRuleData.cs` — to pytanie o nazewnictwo i własność trzeba rozstrzygnąć **przed** pisaniem grupy 2, nie w trakcie. Nie ruszałem żadnego z tych plików.
- **To nie zanieczyściło wyniku sondy.** Aplikacja, na której czytałem kolory, miała te pliki skompilowane, ale zaobserwowane kolorowanie było w stosunku jeden do jednego z moimi dwiema regułami sondy i objęło dokładnie te dwie wskazane kolumny — żadna inna komórka nie była pokolorowana. Żadna reguła sterowana danymi nie brała udziału.

## Stan repozytorium po tej grupie

Sonda cofnięta. `git diff` na `XafXPODynAssem/XafXPODynAssem.Module/BusinessObjects/CustomClass.cs` jest pusty.
Jedynym trwałym produktem grupy 1 jest ten plik.
