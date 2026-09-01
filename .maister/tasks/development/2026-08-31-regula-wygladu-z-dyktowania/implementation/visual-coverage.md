# Macierz pokrycia makiet

Źródło: [`../analysis/design-context/INDEX.md`](../analysis/design-context/INDEX.md) — 6 ekranów i 4 komponenty.
Plan: [`implementation-plan.md`](implementation-plan.md)

| ID ekranu/komponentu | Pokryte przez grupę zadań | Stan |
|---|---|---|
| `screen:lista-regul` | Grupa 6 (Ekran reguł) · Grupa 12 (Weryfikacja ręczna) | ✅ |
| `screen:regula-definicja` | Grupa 6 (Ekran reguł) · Grupa 12 (Weryfikacja ręczna) | ✅ |
| `screen:regula-wyglad` | Grupa 6 (Ekran reguł) · Grupa 12 (Weryfikacja ręczna) | ✅ |
| `screen:czat-utworzenie` | Grupa 7 (Narzędzia AI) · Grupa 12 (Weryfikacja ręczna) | ✅ |
| `screen:czat-blad` | Grupa 7 (Narzędzia AI) · Grupa 12 (Weryfikacja ręczna) | ✅ |
| `screen:efekt-faktury` | Grupa 5 (Kontrolery) · Grupa 12 (Weryfikacja ręczna) | ✅ |
| `component:odznaka-stanu` | Grupa 6 (Ekran reguł, krok 6.3 — trzy statyczne `[Appearance]`) | ✅ |
| `component:probka-roli` | Grupa 2 (Enumy i paleta ról) · Grupa 6 (Ekran reguł, zakładka „Wygląd") | ✅ |
| `component:panel-diagnozy` | Grupa 6 (Ekran reguł, krok 6.5) · Grupa 4 (Walidator — treść diagnozy) | ✅ |
| `component:wywolanie-narzedzia` | Grupa 7 (Narzędzia AI — konwencja `PROBLEM:` / `MISSING:`) | ✅ |

## Pozycje niepokryte

Brak. Wszystkie 10 pozycji z `INDEX.md` — 6 ekranów i 4 komponenty — ma co najmniej jedną grupę zadań odpowiedzialną za zgodność, a kryteria `acceptance` są rozpisane w polach `Visual References` grup 2, 5, 6, 7 i 12.

## Zależności i uwagi

- **`component:probka-roli` jest sprawdzalny dopiero po grupie 1.** Próbka pokazuje rolę rozwiązaną w bieżącej skórce, a odcienie dobiera grupa 2 na podstawie werdyktu rozpoznania empirycznego (`analysis/rozpoznanie-motyw-i-alfa.md`). Do czasu zamknięcia grupy 1 nie ma czym wypełnić próbki.
- **Dwa poziomy odpowiedzialności.** Grupy 2, 5, 6 i 7 odpowiadają za *zbudowanie* zgodne z makietą; grupa 12 za *potwierdzenie zgodności na uruchomionej aplikacji*, zrzutem ekranu. Podwójny wpis w tabeli nie jest duplikatem — to budowa i odbiór.
- **Rozbicie ekranu między grupy jest zamierzone.** `screen:regula-definicja` powstaje w grupie 6, ale treść panelu diagnozy pochodzi z walidatora (grupa 4). Makieta jest wiążąca dla obu.
- **Grupa 12 wypisuje odchylenia jako listę**, nie kwituje zdaniem „zgodne" — to warunek odbioru tej grupy.
