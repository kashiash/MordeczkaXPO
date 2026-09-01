# Zasoby projektowe — wynik rozpoznania

## TL;DR
Projekt nie ma własnego systemu projektowego. `site.css` to 30 linii szablonu XAF (wysokość, logo, overlay błędu) — zero zmiennych CSS, zero tokenów, brak Tailwinda i biblioteki komponentów. Cały język wizualny pochodzi z DevExpress Blazor 26.1.4 i domyślnego motywu XAF. Makiety mają odwzorować układ XAF (lewa nawigacja z grupami, pasek akcji nad widokiem, `DxGrid`, DetailView z zakładkami), a nie wprowadzać własną estetykę.

## Kluczowe decyzje
- Wiązać makiety z układem XAF, nie z abstrakcyjnym motywem — nowe ekrany mają wyglądać jak `CustomClass_ListView` i `WorkflowDefinition_DetailView`, które już w aplikacji są.
- Grupa nawigacji dla reguł: **„Zarządzanie schematem"** — ta sama, w której siedzą `CustomClass` i `WorkflowDefinition` (`Workflow.cs:28`).
- Kolory ról w makiecie pokazywać jako próbki obok nazwy roli, nie jako wartości hex — bo w produkcie odcień rozwiązuje się dopiero przy renderowaniu, zależnie od skórki.

## Otwarte pytania / ryzyka
- Nie ustalono, czy motyw DevExpress jest przełączalny per użytkownik — od tego zależy, czy paleta ról musi być jedna dla obu motywów, czy dwie. Do sprawdzenia empirycznie przed implementacją palety.

---

## Rozpoznanie warstwami

### Warstwa 1 — standardy projektu
`.maister/docs/INDEX.md` **nie istnieje** w tym repozytorium. Brak plików standardów do zastosowania.

### Warstwa 2 — system projektowy w kodzie
| Rodzaj | Wynik |
|---|---|
| tokeny | brak |
| motyw | brak własnego; motyw DevExpress Blazor |
| tailwind / panda / uno | brak |
| zmienne CSS | **zero** — `grep -- '--[a-z-]*:' wwwroot/css/*.css` nic nie zwraca |
| biblioteka komponentów | DevExpress Blazor 26.1.4 (`DxGrid`, `DxTabs`, `DxComboBox`, `DxCheckBox`, `DxButton`) |
| ikony | zestaw DevExpress |

Jedyny arkusz to `wwwroot/css/site.css` (30 linii): wysokość `html/body/app`, maska SVG dla `.header-logo`, overlay `#blazor-error-ui`. Nic, do czego można się przywiązać.

### Warstwa 3 — dostępne umiejętności projektowe
| Rodzaj | Nazwa | Dlaczego |
|---|---|---|
| skill | `xaf-conditional-appearance` | Bezpośrednio o mechanizmie, który budujemy — słownik kryteriów i reguł wyglądu XAF |
| skill | `xaf-custom-editors` | Jeśli rola koloru dostanie własny edytor z podglądem próbki |
| skill | `frontend-design` | Ogólne wskazówki wizualne — pomocnicze, nie wiążące dla ekranu XAF |

Umiejętności `syncfusion-blazor-*` pominięte świadomie: projekt stoi na DevExpressie, mieszanie bibliotek byłoby błędem.

## Co z tego wiąże
Skoro nie ma czego dziedziczyć poza DevExpressem, wiążący jest **układ istniejących ekranów aplikacji**:
- lewa nawigacja z grupami, reguły w grupie „Zarządzanie schematem" obok klas i przepływów,
- pasek akcji nad widokiem (Nowy, Usuń, Odśwież + akcje własne: „Sprawdź reguły", „Klonuj"),
- `DxGrid` z wierszem filtra i kolumnami sortowalnymi,
- DetailView złożony z zakładek i grup układu, etykiety po polsku przez `[XafDisplayName]`,
- statyczne `[Appearance]` do wyróżnienia wierszy — dokładnie jak `CustomClass.cs:23-32` wyszarza encje wygraduowane.
