using XafXPODynAssem.Module.Validation;
using Xunit;

namespace XafXPODynAssem.Module.Tests
{
    /// <summary>
    /// Straznik schematu decyduje, czy metadana pola da sie pogodzic z kolumna, ktora
    /// juz istnieje w bazie. Testowane sa wylacznie czyste funkcje — te, ktore nie
    /// dotykaja polaczenia z Postgresem.
    /// </summary>
    public class SchemaGuardTests
    {
        [Fact(DisplayName = "Opis typu referencyjnego zawiera nazwe klasy docelowej")]
        public void ReferenceDescriptionIncludesTargetClass()
        {
            Assert.Equal("Referencja do Kontrahent", FieldTypeChangeGuard.Describe("Reference", "Kontrahent"));
        }

        [Fact(DisplayName = "Referencja bez klasy docelowej opisana jest ogolnie")]
        public void ReferenceWithoutTargetIsDescribedGenerically()
        {
            Assert.Equal("Referencja", FieldTypeChangeGuard.Describe("Reference", null));
            Assert.Equal("Referencja", FieldTypeChangeGuard.Describe("Reference", "   "));
        }

        [Fact(DisplayName = "Typ prosty opisany jest wlasna nazwa")]
        public void SimpleTypeIsDescribedByItsOwnName()
        {
            Assert.Equal("String", FieldTypeChangeGuard.Describe("String", null));
            Assert.Equal("Decimal", FieldTypeChangeGuard.Describe("Decimal", "ignorowane"));
        }

        [Fact(DisplayName = "Brak typu opisany jest jawnie, nie pustym napisem")]
        public void MissingTypeIsDescribedExplicitly()
        {
            Assert.Equal("(brak)", FieldTypeChangeGuard.Describe(null, null));
        }

        [Fact(DisplayName = "Nieznany typ metadanej uznajemy za zgodny — nie blokujemy tego, czego nie rozumiemy")]
        public void UnknownMetadataTypeIsTreatedAsCompatible()
        {
            Assert.True(FieldTypeChangeGuard.IsColumnCompatible("TypBezMapowania", "text"));
        }

        [Fact(DisplayName = "Brak typu po ktorejkolwiek stronie uznajemy za zgodny")]
        public void MissingTypeOnEitherSideIsTreatedAsCompatible()
        {
            Assert.True(FieldTypeChangeGuard.IsColumnCompatible(null, "text"));
            Assert.True(FieldTypeChangeGuard.IsColumnCompatible("String", null));
            Assert.True(FieldTypeChangeGuard.IsColumnCompatible("", ""));
        }

        [Fact(DisplayName = "Odmowa usuniecia pola nazywa klase, pole i wskazuje wyjscie zastepcze")]
        public void FieldRemovalRefusalNamesTheFieldAndOffersAnAlternative()
        {
            var refusal = FieldTypeChangeGuard.BuildFieldRemovalRefusal("Faktura", "Kwota");

            Assert.Contains("Faktura.Kwota", refusal);
            Assert.Contains("ODMOWA", refusal);
            // Zasada tej aplikacji: pol nie usuwamy, tylko je ukrywamy. Komunikat musi
            // podac droge wyjscia, bo trafia wprost do uzytkownika przez czat.
            Assert.Contains("IsVisibleInListView", refusal);
            Assert.Contains("IsVisibleInDetailView", refusal);
        }

        [Fact(DisplayName = "Ukrywanie pola zamiast usuwania jest sankcjonowana sciezka — regula wygladu musi to uwzglednic")]
        public void HidingIsTheSanctionedPathWhichAppearanceRulesMustAccountFor()
        {
            // Ten test dokumentuje zaleznosc miedzy strażnikiem a regulami wygladu:
            // skoro pola sie nie usuwa, tylko ukrywa, to straznik "czy wlasciwosc sie
            // rozwiazuje" NIE wykryje pola ukrytego — wlasciwosc dalej istnieje.
            // Dlatego walidator regul ma osobny stan ostrzegawczy dla tego przypadku.
            var refusal = FieldTypeChangeGuard.BuildFieldRemovalRefusal("Faktura", "Kwota");
            Assert.Contains("ukryj pole", refusal);
        }
    }
}
