using DevExpress.Data.Filtering;
using Xunit;

namespace XafXPODynAssem.Module.Tests
{
    /// <summary>
    /// Kryterium reguly wygladu przychodzi z czatu jako tekst. Zanim regula trafi do bazy,
    /// walidator musi wiedziec dwie rzeczy: czy tekst w ogole sie parsuje i jakie wlasciwosci
    /// przywoluje. Te testy opisuja zachowanie CriteriaOperator, na ktorym walidator stanie.
    /// </summary>
    public class CriteriaTests
    {
        [Theory(DisplayName = "Kryteria realnie uzywane w regulach parsuja sie")]
        [InlineData("[Zaplacona] = False")]
        [InlineData("[Kwota] > 10000")]
        [InlineData("[Kwota] > 1000 And [Kwota] <= 5000")]
        [InlineData("[TerminPlatnosci] < LocalDateTimeToday()")]
        [InlineData("Contains([Nazwa], 'VIP')")]
        [InlineData("IsNullOrEmpty([Email])")]
        [InlineData("Not IsNullOrEmpty([Uwagi])")]
        [InlineData("[Status] = 'Anulowane'")]
        public void RealisticCriteriaParse(string expression)
        {
            var parsed = CriteriaOperator.Parse(expression);
            Assert.NotNull(parsed);
        }

        [Fact(DisplayName = "Zapis bez nawiasow kwadratowych tez sie parsuje — model nie musi ich uzywac")]
        public void PropertyWithoutBracketsAlsoParses()
        {
            // Istotne dla narzedzia AI: model czesto pisze "Zaplacona = False".
            // Skladnia to dopuszcza, wiec walidator nie moze tego odrzucac.
            Assert.NotNull(CriteriaOperator.Parse("Zaplacona = False"));
            Assert.NotNull(CriteriaOperator.Parse("Kwota > 10000"));
        }

        [Theory(DisplayName = "Skladniowy bubel jest odrzucany wyjatkiem, nie cicho")]
        [InlineData("[Kwota] >")]
        [InlineData("[Kwota] >> 10")]
        [InlineData("And [Zaplacona]")]
        [InlineData("Contains([Nazwa]")]
        public void MalformedCriteriaThrow(string expression)
        {
            // Walidator ma ten wyjatek zlapac i oddac jego tresc uzytkownikowi,
            // zamiast zapisac regule, ktora nigdy nie zadziala.
            Assert.ThrowsAny<System.Exception>(() => CriteriaOperator.Parse(expression));
        }

        [Fact(DisplayName = "Puste kryterium daje null, nie wyjatek — to osobny przypadek do obsluzenia")]
        public void EmptyCriteriaYieldsNullRatherThanThrowing()
        {
            Assert.Null(CriteriaOperator.Parse(""));
            Assert.Null(CriteriaOperator.Parse("   "));
        }

        [Fact(DisplayName = "Nieistniejaca wlasciwosc parsuje sie bez bledu — samo parsowanie NIE wystarcza")]
        public void UnknownPropertyStillParses()
        {
            // To jest sedno walidatora regul: "[Kwotaa] > 10000" jest skladniowo poprawne.
            // Parser nie zna typu docelowego, wiec nie ma jak stwierdzic, ze pola nie ma.
            // Dlatego po parsowaniu MUSI nastapic sprawdzenie wlasciwosci w ITypeInfo —
            // inaczej bubel trafia do bazy i wywraca widok dopiero przy renderowaniu.
            var parsed = CriteriaOperator.Parse("[Kwotaa] > 10000");
            Assert.NotNull(parsed);
        }

        [Fact(DisplayName = "Wlasciwosci przywolane w kryterium da sie wyliczyc z drzewa")]
        public void ReferencedPropertiesCanBeCollectedFromTheTree()
        {
            var properties = CollectProperties(CriteriaOperator.Parse(
                "[Zaplacona] = False And [Kwota] > 1000 And Contains([Nazwa], 'VIP')"));

            Assert.Contains("Zaplacona", properties);
            Assert.Contains("Kwota", properties);
            Assert.Contains("Nazwa", properties);
            Assert.Equal(3, properties.Count);
        }

        [Fact(DisplayName = "Zbieranie wlasciwosci siega w glab zagniezdzonych funkcji")]
        public void PropertyCollectionReachesIntoNestedFunctions()
        {
            var properties = CollectProperties(CriteriaOperator.Parse(
                "Not IsNullOrEmpty([Kontrahent.Email])"));

            Assert.Contains("Kontrahent.Email", properties);
        }

        /// <summary>
        /// Obchodzi drzewo kryterium i zbiera nazwy przywolanych wlasciwosci.
        /// Ten sam ksztalt trafi do walidatora regul — tu sprawdzamy, ze podejscie dziala.
        /// </summary>
        private static HashSet<string> CollectProperties(CriteriaOperator criteria)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            Walk(criteria, found);
            return found;
        }

        private static void Walk(CriteriaOperator node, HashSet<string> found)
        {
            switch (node)
            {
                case null:
                    return;
                case OperandProperty property:
                    found.Add(property.PropertyName);
                    return;
                case UnaryOperator unary:
                    Walk(unary.Operand, found);
                    return;
                case BinaryOperator binary:
                    Walk(binary.LeftOperand, found);
                    Walk(binary.RightOperand, found);
                    return;
                case GroupOperator group:
                    foreach (var operand in group.Operands) Walk(operand, found);
                    return;
                case FunctionOperator function:
                    foreach (var operand in function.Operands) Walk(operand, found);
                    return;
                case InOperator inOperator:
                    Walk(inOperator.LeftOperand, found);
                    foreach (var operand in inOperator.Operands) Walk(operand, found);
                    return;
                case BetweenOperator between:
                    Walk(between.TestExpression, found);
                    Walk(between.BeginExpression, found);
                    Walk(between.EndExpression, found);
                    return;
            }
        }
    }
}
