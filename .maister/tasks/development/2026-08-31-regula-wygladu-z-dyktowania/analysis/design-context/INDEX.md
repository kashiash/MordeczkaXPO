# Spis ekranów i komponentów

Makiety wygenerowane w fazie 4. Galeria na żywo: `http://localhost:3847` (dopóki sesja trwa). Pliki na dysku w `analysis/design-context/mockups/`.

| ID | Typ | Źródło | Opis |
|----|-----|--------|------|
| screen:lista-regul | screen | analysis/design-context/mockups/lista-regu-wygl-du.html | Lista reguł wyglądu — kolumny ze stanem, wiersze niesprawne wyróżnione, akcje „Sprawdź reguły" i „Klonuj" |
| screen:regula-definicja | screen | analysis/design-context/mockups/regu-a-zak-adka-definicja.html | Szczegóły reguły, zakładka „Definicja" + panel diagnozy dla reguły niesprawnej |
| screen:regula-wyglad | screen | analysis/design-context/mockups/regu-a-zak-adka-wygl-d.html | Szczegóły reguły, zakładka „Wygląd" — rola koloru z podglądem, nadpisanie kolorem jako wyjście awaryjne |
| screen:czat-utworzenie | screen | analysis/design-context/mockups/czat-utworzenie-regu-y-z-dyktowania.html | Przepływ w czacie: dyktowanie → dopytanie o jedną rzecz → potwierdzenie → zapis → „wystarczy F5" |
| screen:czat-blad | screen | analysis/design-context/mockups/czat-walidacja-odrzuca-bubla.html | Walidacja odrzuca regułę z nieistniejącym polem; komunikat wraca do modelu z listą pól |
| screen:efekt-faktury | screen | analysis/design-context/mockups/efekt-lista-faktur.html | Efekt końcowy — lista faktur z pokolorowanymi wierszami niezapłaconych |
| component:odznaka-stanu | component | analysis/design-context/mockups/lista-regu-wygl-du.html | Odznaka stanu reguły: Sprawna / Ostrzeżenie / Niesprawna / Wyłączona |
| component:probka-roli | component | analysis/design-context/mockups/regu-a-zak-adka-wygl-d.html | Próbka koloru przy nazwie roli — pokazuje rolę, nie zapisany ARGB |
| component:panel-diagnozy | component | analysis/design-context/mockups/regu-a-zak-adka-definicja.html | Panel z powodem niesprawności i podpowiedzią naprawy |
| component:wywolanie-narzedzia | component | analysis/design-context/mockups/czat-utworzenie-regu-y-z-dyktowania.html | Blok pokazujący w czacie wywołanie narzędzia i jego wynik (także odmowę) |

## Co z tych makiet jest wiążące dla implementacji

- **Stan reguły jest kolumną na liście**, nie ukrytą właściwością. Reguła niesprawna ma być widoczna bez klikania.
- **Rola koloru zamiast wartości szesnastkowej** — w interfejsie użytkownik wybiera z sześciu ról, a próbka pokazuje, jak to wygląda w bieżącej skórce.
- **Nadpisanie kolorem jest zepchnięte na dół zakładki i opisane jako wyjście awaryjne.** Narzędzie AI nie ma tych pól w kontrakcie.
- **Asystent dopytuje o jedną rzecz naraz** i nigdy nie zgaduje kryterium ani roli koloru.
- **Odmowa zapisu wraca z listą dostępnych pól**, żeby model poprawił się sam, zamiast pytać użytkownika o coś, co jest w schemacie.
- **Grupa nawigacji: „Zarządzanie schematem"** — obok Klas użytkownika i Przepływów.
