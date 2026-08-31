using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Metadata;
using XafXPODynAssem.Module.BusinessObjects;
using XafXPODynAssem.Module.Services;
using Xunit;

namespace XafXPODynAssem.Module.Tests
{
    /// <summary>
    /// Graduacja zamienia encje tworzona w locie na zwykla klase C# do wklejenia w kod.
    /// Te testy pilnuja ksztaltu wygenerowanego zrodla — w szczegolnosci tego, ze zrodlo
    /// NIE deklaruje przestrzeni nazw, bo z tego wynika, ze pelna nazwa typu zmienia sie
    /// po graduacji i reguly wygladu wskazujace stara nazwe przestaja dzialac.
    /// </summary>
    public class GraduationTests : IDisposable
    {
        private readonly IDataLayer dataLayer;
        private readonly Session session;

        public GraduationTests()
        {
            dataLayer = new SimpleDataLayer(new ReflectionDictionary(),
                new InMemoryDataStore(AutoCreateOption.DatabaseAndSchema));
            session = new Session(dataLayer);
        }

        public void Dispose()
        {
            session.Dispose();
            dataLayer.Dispose();
        }

        private CustomClass BuildClass(string name = "Faktura")
        {
            return new CustomClass(session) { ClassName = name, NavigationGroup = "Dane" };
        }

        [Fact(DisplayName = "Wygenerowane zrodlo NIE deklaruje przestrzeni nazw — stad zmiana pelnej nazwy typu po graduacji")]
        public void GeneratedSourceDeclaresNoNamespace()
        {
            var source = GraduationService.Graduate(BuildClass());

            // To nie jest usterka do naprawienia w tym zadaniu, tylko udokumentowane
            // zachowanie, z ktorego wynika ryzyko dla regul wygladu: zrodlo wklejone do
            // folderu BusinessObjects dziedziczy jego przestrzen nazw, wiec typ przechodzi
            // z XafXPODynAssem.RuntimeEntities.X na XafXPODynAssem.Module.BusinessObjects.X.
            Assert.DoesNotContain("namespace ", source);
            Assert.Contains("Place this file in your BusinessObjects folder", source);
        }

        [Fact(DisplayName = "Graduacja przestawia status na Compiled")]
        public void GraduationSetsCompiledStatus()
        {
            var cc = BuildClass();
            Assert.Equal(CustomClassStatus.Runtime, cc.Status);

            GraduationService.Graduate(cc);

            // Status leci na Compiled natychmiast, zanim ktokolwiek wklei zrodlo
            // i przebuduje obraz — stad okno, w ktorym typ nie istnieje pod zadna nazwa.
            Assert.Equal(CustomClassStatus.Compiled, cc.Status);
        }

        [Fact(DisplayName = "Graduacja zapisuje wygenerowane zrodlo w encji")]
        public void GraduationStoresGeneratedSource()
        {
            var cc = BuildClass();
            var returned = GraduationService.Graduate(cc);

            Assert.False(string.IsNullOrWhiteSpace(cc.GraduatedSource));
            Assert.Equal(returned, cc.GraduatedSource);
        }

        [Fact(DisplayName = "Wygenerowana klasa dziedziczy po BaseObject i ma konstruktor z Session")]
        public void GeneratedClassDerivesFromBaseObjectWithSessionConstructor()
        {
            var source = GraduationService.Graduate(BuildClass("Zlecenie"));

            Assert.Contains("public class Zlecenie : BaseObject", source);
            Assert.Contains("public Zlecenie(Session session) : base(session) { }", source);
        }

        [Fact(DisplayName = "Grupa nawigacji trafia do wygenerowanego zrodla")]
        public void NavigationGroupIsEmitted()
        {
            var source = GraduationService.Graduate(BuildClass());

            Assert.Contains("[NavigationItem(\"Dane\")]", source);
            Assert.Contains("[DefaultClassOptions]", source);
        }

        [Fact(DisplayName = "Zrodlo zawiera sekcje-notatki — tu dojdzie notatka o regulach wygladu")]
        public void SourceContainsNoteSections()
        {
            var source = GraduationService.Graduate(BuildClass());

            // Konwencja, do ktorej dopisze sie "--- Appearance Note ---" z lista regul
            // przeniesionych na nowa nazwe typu.
            Assert.Contains("--- Entity Class ---", source);
            Assert.Contains("--- XPO Note ---", source);
        }
    }
}
